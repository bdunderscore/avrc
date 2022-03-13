using System;
using System.Collections.Generic;
using System.Linq;
using Codice.Client.BaseCommands.Differences;
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

        protected readonly VRCAvatarDescriptor Avatar;
        protected readonly AvrcParameters Parameters;
        protected readonly AnimatorController AnimatorController;
        protected readonly AvrcBindingConfiguration Binding;
        protected readonly AvrcNames Names;
        protected readonly AvrcAnimations Animations;
        protected readonly AvrcObjects Objects;
        protected readonly float Timeout;

        protected AvrcLayerSetupCommon(VRCAvatarDescriptor avatarDescriptor, AvrcBindingConfiguration binding)
        {
            Avatar = avatarDescriptor;
            Binding = binding;
            Names = new AvrcNames(binding);
            Parameters = binding.parameters;
            Timeout = Mathf.Max(1.0f, binding.timeoutSeconds);
            
            Animations = new AvrcAnimations(Parameters, Names);
            Objects = new AvrcObjects(Parameters, Names);
            
            foreach (var c in avatarDescriptor.baseAnimationLayers)
            {
                if (c.type == VRCAvatarDescriptor.AnimLayerType.FX)
                {
                    AnimatorController = (AnimatorController) c.animatorController;
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
            switch (parameter.type)
            {
                case AvrcParameters.AvrcParameterType.Bool:
                case AvrcParameters.AvrcParameterType.IsLocal:
                    AddParameter(Names.UserParameter(parameter), AnimatorControllerParameterType.Bool);
                    break;
                case AvrcParameters.AvrcParameterType.Int:
                    AddParameter(Names.UserParameter(parameter), AnimatorControllerParameterType.Int);
                    break;
                case AvrcParameters.AvrcParameterType.Float:
                    AddParameter(Names.UserParameter(parameter), AnimatorControllerParameterType.Float);
                    break;
            }

            AnimatorStateMachine animatorStateMachine = null;
            switch (parameter.type)
            {
                case AvrcParameters.AvrcParameterType.Bool:
                {
                    EqualsCondition eq = (transition, index) =>
                    {
                        transition.AddCondition(
                            index != 0 ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
                            0, Names.UserParameter(parameter)
                        );
                    };
                    NotEqualsCondition neq = (transition, index) => eq(transition, index != 0 ? 0 : 1);
                    DriveParameter drive = (driver, index) =>
                    {
                        driver.parameters.Add(new VRC_AvatarParameterDriver.Parameter()
                        {
                            name = Names.UserParameter(parameter),
                            type = VRC_AvatarParameterDriver.ChangeType.Set,
                            value = index
                        });
                    };

                    if (parameter.syncDirection == AvrcParameters.SyncDirection.OneWay)
                    {
                        animatorStateMachine = OneWayParamLayer(parameter, 2, eq, neq, drive);
                    }
                    else
                    {
                        animatorStateMachine = TwoWayParamLayer(parameter, 2, eq, neq, drive);
                    }

                    break;
                }
                case AvrcParameters.AvrcParameterType.Int:
                {
                    var values = parameter.maxVal - parameter.minVal + 1;

                    EqualsCondition eq = (transition, index) =>
                    {
                        transition.AddCondition(
                            AnimatorConditionMode.Equals,
                            index + parameter.minVal,
                            Names.UserParameter(parameter)
                        );
                    };
                    NotEqualsCondition neq = (transition, index) =>
                    {
                        transition.AddCondition(
                            AnimatorConditionMode.NotEqual,
                            index + parameter.minVal,
                            Names.UserParameter(parameter)
                        );
                    };
                    DriveParameter drive = (driver, index) =>
                    {
                        driver.parameters.Add(new VRC_AvatarParameterDriver.Parameter()
                        {
                            name = Names.UserParameter(parameter),
                            type = VRC_AvatarParameterDriver.ChangeType.Set,
                            value = index + parameter.minVal
                        });
                    };

                    if (parameter.syncDirection == AvrcParameters.SyncDirection.OneWay)
                    {
                        animatorStateMachine = OneWayParamLayer(parameter, values, eq, neq, drive);
                    }
                    else
                    {
                        animatorStateMachine = TwoWayParamLayer(parameter, values, eq, neq, drive);
                    }

                    break;
                }
                case AvrcParameters.AvrcParameterType.Float:
                    animatorStateMachine = FloatParamLayer(parameter);
                    break;
                case AvrcParameters.AvrcParameterType.IsLocal:
                    animatorStateMachine = IsLocalParamLayer(parameter);
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

        protected delegate void EqualsCondition(AnimatorStateTransition transition, int index);

        protected delegate void NotEqualsCondition(AnimatorStateTransition transition, int index);

        protected delegate void DriveParameter(VRCAvatarParameterDriver driver, int index);

        protected abstract AnimatorStateMachine OneWayParamLayer(
            AvrcParameters.AvrcParameter parameter,
            int values,
            EqualsCondition equalsCondition,
            NotEqualsCondition notEqualsCondition,
            DriveParameter driveParameter
        );

        protected abstract AnimatorStateMachine TwoWayParamLayer(
            AvrcParameters.AvrcParameter parameter,
            int values,
            EqualsCondition equalsCondition,
            NotEqualsCondition notEqualsCondition,
            DriveParameter driveParameter
        );

        protected abstract AnimatorStateMachine IsLocalParamLayer(AvrcParameters.AvrcParameter parameter);
        protected abstract AnimatorStateMachine FloatParamLayer(AvrcParameters.AvrcParameter parameter);

        protected VRCAvatarParameterDriver ParameterDriver(bool localOnly = true)
        {
            var driver = ScriptableObject.CreateInstance<VRCAvatarParameterDriver>();
            driver.name = $"AVRC_Driver";
            driver.localOnly = localOnly;
            driver.parameters = new List<VRC_AvatarParameterDriver.Parameter>();

            AssetDatabase.AddObjectToAsset(driver, AnimatorController);

            return driver;
        }

        protected VRCAvatarParameterDriver ParameterDriver(string paramName, int value, bool localOnly = true)
        {
            return ParameterDriver(localOnly, (paramName, value));
        }

        protected VRCAvatarParameterDriver ParameterDriver(bool localOnly = true, params (string, int)[] values)
        {
            var driver = ParameterDriver(localOnly);
            foreach (var (name, val) in values)
            {
                driver.parameters.Add(
                    new VRC_AvatarParameterDriver.Parameter()
                    {
                        chance = 1,
                        name = name,
                        value = val,
                        type = VRC_AvatarParameterDriver.ChangeType.Set,
                    }
                );
            }

            return driver;
        }

        protected delegate AnimationClip ClipCreator(AvrcAnimations.LocalState local);

        /// <summary>
        /// This state machine enables the main constraint, and adjusts bounds when it is running on the peer's
        /// client. It also sets the common presence variables.
        /// </summary>
        /// <param name="animPrefix"></param>
        /// <param name="recvPeerLocal"></param>
        /// <param name="clipCreator"></param>
        /// <returns></returns>
        protected AnimatorStateMachine CommonSetupLayer(
            string animPrefix,
            string recvPeerLocal,
            string recvPeerPresent,
            ClipCreator clipCreator)
        {
            AnimatorStateMachine rootStateMachine = new AnimatorStateMachine();

            AddParameter(Names.PubParamEitherLocal, AnimatorControllerParameterType.Bool);
            AddParameter(Names.PubParamPeerLocal, AnimatorControllerParameterType.Bool);
            AddParameter(Names.PubParamPeerPresent, AnimatorControllerParameterType.Bool);

            var init = rootStateMachine.AddState("Init");
            init.motion = Animations.Named(
                animPrefix + "Init",
                () => clipCreator(AvrcAnimations.LocalState.Unknown)
            );
            init.behaviours = new StateMachineBehaviour[]
            {
                ParameterDriver(localOnly: false,
                    (Names.PubParamEitherLocal, 0),
                    (Names.PubParamPeerLocal, 0),
                    (Names.PubParamPeerPresent, 0)
                )
            };

            var ownerLocal = rootStateMachine.AddState("OwnerLocal");
            ownerLocal.motion = Animations.Named(
                animPrefix + "OwnerLocal",
                () => clipCreator(AvrcAnimations.LocalState.OwnerLocal)
            );
            var t = AddInstantTransition(init, ownerLocal);
            AddIsLocalCondition(t);
            ownerLocal.behaviours = new StateMachineBehaviour[]
            {
                ParameterDriver(localOnly: false,
                    (Names.PubParamEitherLocal, 1),
                    (Names.PubParamPeerLocal, 0),
                    (Names.PubParamPeerPresent, 0)
                )
            };

            var ownerLocalTxPresent = rootStateMachine.AddState("OwnerLocalTxPresent");
            ownerLocalTxPresent.motion = ownerLocal.motion;
            ownerLocalTxPresent.behaviours = new StateMachineBehaviour[]
            {
                ParameterDriver(localOnly: false,
                    (Names.PubParamEitherLocal, 1),
                    (Names.PubParamPeerLocal, 0),
                    (Names.PubParamPeerPresent, 1)
                )
            };
            t = AddInstantTransition(ownerLocal, ownerLocalTxPresent);
            t.AddCondition(AnimatorConditionMode.Greater, 0.5f, recvPeerPresent);

            CreateTimeoutStates(rootStateMachine, ownerLocalTxPresent, ownerLocal, recvPeerPresent);

            var peerLocal = rootStateMachine.AddState("PeerLocal");
            peerLocal.motion = Animations.Named(
                animPrefix + "PeerLocal",
                () => clipCreator(AvrcAnimations.LocalState.PeerLocal)
            );
            t = AddInstantTransition(init, peerLocal);
            AddParameter(recvPeerLocal, AnimatorControllerParameterType.Float);
            t.AddCondition(AnimatorConditionMode.Greater, 0.5f, recvPeerLocal);
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsLocal");
            
            peerLocal.behaviours = new StateMachineBehaviour[]
            {
                ParameterDriver(localOnly: false,
                    (Names.PubParamEitherLocal, 1),
                    (Names.PubParamPeerLocal, 1),
                    (Names.PubParamPeerPresent, 1)
                )
            };

            CreateTimeoutStates(rootStateMachine, peerLocal, init, recvPeerLocal);

            var peerPresent = rootStateMachine.AddState("PeerPresent");
            peerPresent.motion = init.motion;
            t = AddInstantTransition(init, peerPresent);
            t.AddCondition(AnimatorConditionMode.Greater, 0.5f, recvPeerPresent);
            t.AddCondition(AnimatorConditionMode.Less, 0.5f, recvPeerLocal);
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsLocal");
            
            peerPresent.behaviours = new StateMachineBehaviour[]
            {
                ParameterDriver(localOnly: false,
                    (Names.PubParamEitherLocal, 0),
                    (Names.PubParamPeerLocal, 0),
                    (Names.PubParamPeerPresent, 1)
                )
            };

            t = AddInstantTransition(peerPresent, peerLocal);
            t.AddCondition(AnimatorConditionMode.Greater, 0.5f, recvPeerLocal);
            
            CreateTimeoutStates(rootStateMachine, peerPresent, init, recvPeerPresent);

            return rootStateMachine;
        }

        private void CreateTimeoutStates(AnimatorStateMachine rootStateMachine,
            AnimatorState present,
            AnimatorState afterTimeout,
            string param)
        {
            AnimatorStateTransition t;
            var ownerLocalTxTimeout = rootStateMachine.AddState(present.name + "Timeout");
            ownerLocalTxTimeout.motion = afterTimeout.motion;
            t = AddInstantTransition(present, ownerLocalTxTimeout);
            t.exitTime = 0.5f;
            t.hasExitTime = true;

            t = AddInstantTransition(ownerLocalTxTimeout, present);
            t.AddCondition(AnimatorConditionMode.Greater, 0.5f, param);

            t = AddInstantTransition(ownerLocalTxTimeout, afterTimeout);
            t.exitTime = Timeout - 0.5f;
            t.hasExitTime = true;
        }

        protected static AnimatorStateTransition AddInstantAnyTransition(AnimatorStateMachine sourceState,
            AnimatorState destinationState)
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