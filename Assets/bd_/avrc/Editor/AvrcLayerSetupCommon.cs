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
        
        protected readonly AvrcParameters Parameters;
        protected readonly AnimatorController AnimatorController;
        protected readonly AvrcNames Names;
        protected readonly AvrcAnimations Animations;
        protected readonly AvrcObjects Objects;

        protected AvrcLayerSetupCommon(VRCAvatarDescriptor avatarDescriptor, AvrcParameters parameters, AvrcNames names)
        {
            Names = names;
            Animations = new AvrcAnimations(parameters, Names);
            Objects = new AvrcObjects(parameters, Names);
            
            this.Parameters = parameters;
            foreach (var c in avatarDescriptor.baseAnimationLayers)
            {
                if (c.type == VRCAvatarDescriptor.AnimLayerType.FX)
                {
                    AnimatorController = (AnimatorController)c.animatorController;
                    break;
                }
            }

            if (AnimatorController == null)
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
            foreach (var param in AnimatorController.parameters)
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
            
            AnimatorController.AddParameter(name, ty);
        }

        protected void CreateParameterLayer(AvrcParameters.AvrcParameter parameter)
        {
            AnimatorStateMachine animatorStateMachine = null;
            switch (parameter.type)
            {
                case AvrcParameters.AvrcParameterType.Bool:
                    animatorStateMachine = BoolParamLayer(parameter);
                    break;
                case AvrcParameters.AvrcParameterType.Int:
                    animatorStateMachine = IntParamLayer(parameter);
                    break;
                case AvrcParameters.AvrcParameterType.Float:
                    animatorStateMachine = FloatParamLayer(parameter);
                    break;
                case AvrcParameters.AvrcParameterType.IsLocal:
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
                AddOrReplaceLayer(Names.ParameterLayerName(parameter), animatorStateMachine);
            }
            else
            {
                RemoveLayer(Names.ParameterLayerName(parameter));
            }
        }

        protected void RemoveLayer(string layerName)
        {
            var newLayers = AnimatorController.layers
                .Where(layer => !layer.name.Equals(layerName))
                .ToArray();

            AnimatorController.layers = newLayers;
        }

        protected void AddOrReplaceLayer(string layerName, AnimatorStateMachine animatorStateMachine)
        {
            animatorStateMachine.name = layerName;
            
            bool newLayer = true;
            var layers = AnimatorController.layers;
            foreach (var t in layers)
            {
                if (t.name == layerName)
                {
                    if (t.stateMachine != null)
                    {
                        AssetDatabase.RemoveObjectFromAsset(t.stateMachine);
                    }

                    t.stateMachine = animatorStateMachine;
                    AnimatorController.layers = layers;
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

                AnimatorController.AddLayer(layer);
            }

            if (AssetDatabase.GetAssetPath(AnimatorController) != "")
            {
                var assets = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(AnimatorController));

                foreach (var asset in assets)
                {
                    if (asset is AnimatorStateMachine && asset.name.Equals(layerName))
                    {
                        AssetDatabase.RemoveObjectFromAsset(asset);
                    }
                }
                
                AssetDatabase.AddObjectToAsset(animatorStateMachine, AssetDatabase.GetAssetPath(AnimatorController));
            }
            
            // animatorStateMachine.hideFlags = HideFlags.HideInHierarchy;
        }

        protected abstract AnimatorStateMachine IsLocalParamLayer(AvrcParameters.AvrcParameter parameter);
        protected abstract AnimatorStateMachine BoolParamLayer(AvrcParameters.AvrcParameter parameter);
        protected abstract AnimatorStateMachine FloatParamLayer(AvrcParameters.AvrcParameter parameter);
        protected abstract AnimatorStateMachine IntParamLayer(AvrcParameters.AvrcParameter parameter);

        protected abstract AnimatorStateMachine BidiIntParamLayer(AvrcParameters.AvrcParameter parameter);

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
            
            AssetDatabase.AddObjectToAsset(driver, AnimatorController);

            return driver;
        }
        
        protected delegate AnimationClip ClipCreator(AvrcAnimations.LocalState local);

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
            init.motion = Animations.Named(
                animPrefix + "Init",
                () => clipCreator(AvrcAnimations.LocalState.Unknown)
            );

            var ownerLocal = rootStateMachine.AddState("OwnerLocal");
            ownerLocal.motion = Animations.Named(
                animPrefix + "OwnerLocal",
                () => clipCreator(AvrcAnimations.LocalState.OwnerLocal)
            );
            var t = AddInstantTransition(init, ownerLocal);
            AddIsLocalCondition(t);
            // OwnerLocal is a terminal state.
            
            var peerLocal = rootStateMachine.AddState("PeerLocal");
            peerLocal.motion = Animations.Named(
                animPrefix + "PeerLocal",
                () => clipCreator(AvrcAnimations.LocalState.PeerLocal)
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