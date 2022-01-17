using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace net.fushizen.avrc
{
    internal abstract class AvrcLayerSetupCommon
    {
        protected const float EPSILON = 0.01f;
        //protected const float PROXIMITY_EPSILON = 1 - EPSILON;
        protected const float MinPresenceTestValue = AvrcObjects.PresenceTestValue - EPSILON;
        protected const float MaxPresenceTestValue = AvrcObjects.PresenceTestValue + EPSILON;
        
        protected readonly AvrcParameters m_parameters;
        protected readonly AnimatorController m_animatorController;
        protected readonly AvrcParameters.AvrcNames Names;

        protected AvrcLayerSetupCommon(VRCAvatarDescriptor avatarDescriptor, AvrcParameters parameters)
        {
            Names = parameters.Names;
            
            this.m_parameters = parameters;
            foreach (var c in avatarDescriptor.baseAnimationLayers)
            {
                if (c.type == VRCAvatarDescriptor.AnimLayerType.FX)
                {
                    m_animatorController = (AnimatorController)c.animatorController;
                    break;
                }
            }

            if (m_animatorController == null)
            {
                throw new ArgumentException("FX layer is required");
            }
        }

        protected void AddPresenceCondition(AnimatorStateTransition t, string varName)
        {
            AddParameter(varName, AnimatorControllerParameterType.Float);
            t.AddCondition(AnimatorConditionMode.Less, MaxPresenceTestValue, varName);
            t.AddCondition(AnimatorConditionMode.Greater, MinPresenceTestValue, varName);
        }
        
        protected void AddParameter(string name, AnimatorControllerParameterType ty)
        {
            foreach (var param in m_animatorController.parameters)
            {
                if (param.name == name)
                {
                    if (param.type != ty)
                    {
                        throw new ArgumentException(
                            $"Animator controller already has a parameter named ${name} but with the wrong type");
                    }

                    return;
                }
            }
            
            m_animatorController.AddParameter(name, ty);
        }

        protected void CreateParameterLayer(AvrcParameters.AvrcParameter parameter)
        {
            AnimatorStateMachine animatorStateMachine = null;
            switch (parameter.type)
            {
                case AvrcParameters.AvrcParameterType.Bool:
                case AvrcParameters.AvrcParameterType.AvrcLock:
                    animatorStateMachine = BoolParamLayer(parameter);
                    break;
                case AvrcParameters.AvrcParameterType.Int:
                    animatorStateMachine = IntParamLayer(parameter);
                    break;
                case AvrcParameters.AvrcParameterType.Float:
                    animatorStateMachine = FloatParamLayer(parameter);
                    break;
                case AvrcParameters.AvrcParameterType.AvrcIsLocal:
                    animatorStateMachine = IsLocalParamLayer(parameter);
                    break;
                case AvrcParameters.AvrcParameterType.BidiInt:
                    animatorStateMachine = BidiIntParamLayer(parameter);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (animatorStateMachine != null)
            {
                AddOrReplaceLayer(m_parameters.Names.ParameterLayerName(parameter), animatorStateMachine);
            }
            else
            {
                RemoveLayer(m_parameters.Names.ParameterLayerName(parameter));
            }
        }

        protected void RemoveLayer(string layerName)
        {
            var newLayers = m_animatorController.layers
                .Where(layer => !layer.name.Equals(layerName))
                .ToArray();

            m_animatorController.layers = newLayers;
        }

        protected void AddOrReplaceLayer(string layerName, AnimatorStateMachine animatorStateMachine)
        {
            animatorStateMachine.name = layerName;
            
            bool newLayer = true;
            var layers = m_animatorController.layers;
            foreach (var t in layers)
            {
                if (t.name == layerName)
                {
                    if (t.stateMachine != null)
                    {
                        AssetDatabase.RemoveObjectFromAsset(t.stateMachine);
                    }

                    t.stateMachine = animatorStateMachine;
                    m_animatorController.layers = layers;
                    newLayer = false;
                    break;
                }
            }

            if (newLayer)
            {
                var layer = new AnimatorControllerLayer()
                {
                    name = layerName,
                    defaultWeight = 1,
                    stateMachine = animatorStateMachine
                };

                m_animatorController.AddLayer(layer);
            }

            if (AssetDatabase.GetAssetPath(m_animatorController) != "")
            {
                var assets = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(m_animatorController));

                foreach (var asset in assets)
                {
                    if (asset is AnimatorStateMachine && asset.name.Equals(layerName))
                    {
                        AssetDatabase.RemoveObjectFromAsset(asset);
                    }
                }
                
                AssetDatabase.AddObjectToAsset(animatorStateMachine, AssetDatabase.GetAssetPath(m_animatorController));
            }
            
            // animatorStateMachine.hideFlags = HideFlags.HideInHierarchy;
        }

        protected abstract AnimatorStateMachine IsLocalParamLayer(AvrcParameters.AvrcParameter parameter);
        protected abstract AnimatorStateMachine BoolParamLayer(AvrcParameters.AvrcParameter parameter);
        protected abstract AnimatorStateMachine FloatParamLayer(AvrcParameters.AvrcParameter parameter);
        protected abstract AnimatorStateMachine IntParamLayer(AvrcParameters.AvrcParameter parameter);

        protected abstract AnimatorStateMachine BidiIntParamLayer(AvrcParameters.AvrcParameter parameter);

        protected void GarbageCollectAnimatorAsset()
        {
            var path = AssetDatabase.GetAssetPath(m_animatorController);
            if (path == null) return;
            
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);

            // Check for multiple Animators first
            foreach (var maybeController in assets)
            {
                if (maybeController is AnimatorController && maybeController != m_animatorController)
                {
                    Debug.LogError("Multiple animation controllers found in asset "
                                   + AssetDatabase.GetAssetPath(m_animatorController)
                                   + "; not garbage collecting.");
                    
                }
            }

            var referencedAssets = new HashSet<UnityEngine.Object>();

            var visitQueue = new Queue<SerializedObject>();
            visitQueue.Enqueue(new SerializedObject(m_animatorController));
            referencedAssets.Add(m_animatorController);

            while (visitQueue.Count > 0)
            {
                var serializedObject = visitQueue.Dequeue();
                
                var iterator = serializedObject.GetIterator();
                while (iterator.Next(true))
                {
                    ProcessProperty(iterator);
                }
            }
            
            void ProcessProperty(SerializedProperty serializedProperty)
            {
                if (serializedProperty.isArray)
                {
                    for (int i = 0; i < serializedProperty.arraySize; i++)
                    {
                        ProcessProperty(serializedProperty.GetArrayElementAtIndex(i));
                    }
                }
                else
                {
                    if (serializedProperty.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        var obj = serializedProperty.objectReferenceValue;
                        if (obj != null && referencedAssets.Add(obj))
                        {
                            visitQueue.Enqueue(new SerializedObject(obj));
                        }
                    }
                }
            }

            foreach (var asset in assets)
            {
                if (asset != null && !referencedAssets.Contains(asset))
                {
                    AssetDatabase.RemoveObjectFromAsset(asset);
                }
            }

            foreach (var referenced in referencedAssets)
            {
                if (AssetDatabase.GetAssetPath(referenced) == "")
                {
                    referenced.hideFlags = HideFlags.HideInHierarchy;
                    AssetDatabase.AddObjectToAsset(referenced, m_animatorController);
                }
            }
        }

        protected VRCAvatarParameterDriver ParameterDriver(string paramName, int value, bool localOnly = true)
        {
            var driver = ScriptableObject.CreateInstance<VRCAvatarParameterDriver>();
            driver.name = $"Driver_{paramName}_{value}";
            driver.localOnly = localOnly;
            driver.parameters = new List<VRC_AvatarParameterDriver.Parameter>()
            {
                new VRC_AvatarParameterDriver.Parameter()
                {
                    chance = 1,
                    name = paramName,
                    value = value,
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                }
            };
            
            AssetDatabase.AddObjectToAsset(driver, m_animatorController);

            return driver;
        }
        
        protected delegate AnimationClip ClipCreator(AvrcParameters parameters, AvrcAnimations.LocalState local);

        /// <summary>
        /// This state machine enables the main constraint, and adjusts bounds when it is running on the peer's
        /// client.
        /// </summary>
        /// <param name="animPrefix"></param>
        /// <param name="recvPeerLocal"></param>
        /// <param name="clipCreator"></param>
        /// <returns></returns>
        protected AnimatorStateMachine CommonSetupLayer(string animPrefix, string recvPeerLocal, ClipCreator clipCreator)
        {
            AnimatorStateMachine rootStateMachine = new AnimatorStateMachine();

            var init = rootStateMachine.AddState("Init");
            init.motion = AvrcAnimations.Named(
                animPrefix + "Init",
                () => clipCreator(m_parameters, AvrcAnimations.LocalState.Unknown)
            );

            var ownerLocal = rootStateMachine.AddState("OwnerLocal");
            ownerLocal.motion = AvrcAnimations.Named(
                animPrefix + "OwnerLocal",
                () => clipCreator(m_parameters, AvrcAnimations.LocalState.OwnerLocal)
            );
            var t = AddInstantTransition(init, ownerLocal);
            AddIsLocalCondition(t);
            // OwnerLocal is a terminal state.
            
            var peerLocal = rootStateMachine.AddState("PeerLocal");
            peerLocal.motion = AvrcAnimations.Named(
                animPrefix + "PeerLocal",
                () => clipCreator(m_parameters, AvrcAnimations.LocalState.PeerLocal)
            );
            t = AddInstantTransition(init, peerLocal);
            AddParameter(recvPeerLocal, AnimatorControllerParameterType.Float);
            t.AddCondition(AnimatorConditionMode.Greater, 0.5f, recvPeerLocal);
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsLocal");

            var peerLocalTimer = rootStateMachine.AddState("PeerLocalTimeout");
            peerLocalTimer.motion = peerLocal.motion;

            t = AddInstantTransition(peerLocal, peerLocalTimer);
            t.hasExitTime = true;
            t.exitTime = 10;

            t = AddInstantTransition(peerLocalTimer, peerLocal);
            t.AddCondition(AnimatorConditionMode.Greater, 0.5f, recvPeerLocal);

            t = AddInstantTransition(peerLocalTimer, init);
            t.hasExitTime = true;
            t.exitTime = 10;

            return rootStateMachine;
        }

        protected static AnimatorStateTransition AddInstantAnyTransition(AnimatorStateMachine sourceState, AnimatorState destinationState)
        {
            AnimatorStateTransition transition = sourceState.AddAnyStateTransition(destinationState);
            transition.exitTime = 0;
            transition.hasExitTime = false;
            transition.duration = 0;
            transition.canTransitionToSelf = false;
            return transition;
        }

        protected static AnimatorStateTransition AddInstantTransition(AnimatorState startState, AnimatorState state)
        {
            var transition = startState.AddTransition(state);
            transition.exitTime = 0;
            transition.hasExitTime = false;
            transition.duration = 0;
            return transition;
        }

        protected void AddIsLocalCondition(AnimatorStateTransition transition)
        {
            AddParameter("IsLocal", AnimatorControllerParameterType.Bool);
            transition.AddCondition(AnimatorConditionMode.If, 1, "IsLocal");
        }
    }
}