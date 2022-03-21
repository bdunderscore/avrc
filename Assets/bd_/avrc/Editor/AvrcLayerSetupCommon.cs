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
        protected const float EPSILON = 0.001f;

        protected readonly AvrcAnimations Animations;
        protected readonly AnimatorController AnimatorController;

        protected readonly VRCAvatarDescriptor Avatar;
        protected readonly AvrcBindingConfiguration Binding;
        protected readonly AvrcNames Names;
        protected readonly AvrcObjects Objects;
        protected readonly AvrcParameters Parameters;
        protected readonly float Timeout;

        protected AvrcLayerSetupCommon(VRCAvatarDescriptor avatarDescriptor, AvrcBindingConfiguration binding)
        {
            Avatar = avatarDescriptor;
            Binding = binding;
            Names = new AvrcNames(binding);
            Parameters = binding.parameters;
            Timeout = Mathf.Max(1.0f, binding.timeoutSeconds);

            Animations = new AvrcAnimations(Parameters, Names);
            Objects = new AvrcObjects(Parameters, Names, Binding.role);

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

        protected void AddPilotCondition(AnimatorStateTransition t, bool present = true)
        {
            foreach (var pilot in Names.SignalPilots(Binding.role.Other()))
            {
                AddParameter(pilot.ParamName, AnimatorControllerParameterType.Float);
                t.AddCondition(present ? AnimatorConditionMode.Greater : AnimatorConditionMode.Less, 0.5f,
                    pilot.ParamName);
            }
        }

        protected void AddSignalCondition(AnimatorStateTransition t, AvrcParameters.AvrcParameter parameter, int index,
            bool ack)
        {
            foreach (var bit in Names.SignalParam(parameter, ack))
            {
                var mode = (index & 1) == 1 ? AnimatorConditionMode.Greater : AnimatorConditionMode.Less;
                AddParameter(bit.ParamName, AnimatorControllerParameterType.Float);
                t.AddCondition(mode, 0.5f, bit.ParamName);
                index >>= 1;
            }
        }

        protected void AddAntiSignalConditions(AvrcParameters.AvrcParameter parameter, int index, bool ack,
            TransitionProvider transitionProvider)
        {
            foreach (var bit in Names.SignalParam(parameter, ack))
            {
                var mode = (index & 1) != 1 ? AnimatorConditionMode.Greater : AnimatorConditionMode.Less;
                var t = transitionProvider();
                t.AddCondition(mode, 0.5f, bit.ParamName);
                index >>= 1;
            }
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
                    AddParameter(Names.UserParameter(parameter), AnimatorControllerParameterType.Bool);
                    break;
                case AvrcParameters.AvrcParameterType.Int:
                    AddParameter(Names.UserParameter(parameter), AnimatorControllerParameterType.Int);
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
            AvrcLayerMarker.MarkLayer(animatorStateMachine, Parameters);

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

        /// <summary>
        /// This state machine enables the main constraint, and adjusts bounds when it is running on the peer's
        /// client. It also sets the common presence variables.
        /// </summary>
        /// <param name="animPrefix"></param>
        /// <param name="recvPeerLocal"></param>
        /// <param name="clipCreator"></param>
        /// <returns></returns>
        protected AnimatorStateMachine CommonSetupLayer(
            string animPrefix
        )
        {
            AnimatorStateMachine rootStateMachine = new AnimatorStateMachine();

            AddParameter(Names.PubParamEitherLocal, AnimatorControllerParameterType.Bool);
            AddParameter(Names.PubParamPeerLocal, AnimatorControllerParameterType.Bool);
            AddParameter(Names.PubParamPeerPresent, AnimatorControllerParameterType.Bool);

            var init = rootStateMachine.AddState("Init");
            init.motion = Animations.Named(
                animPrefix + "Init",
                () => Animations.PresenceClip(AvrcAnimations.LocalState.Unknown, Binding.role)
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
                () => Animations.PresenceClip(AvrcAnimations.LocalState.OwnerLocal, Binding.role)
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
            AddPilotCondition(t);

            CreateTimeoutStates(rootStateMachine, ownerLocalTxPresent, ownerLocal, t_ => AddPilotCondition(t_));

            var peerLocal = rootStateMachine.AddState("PeerLocal");
            peerLocal.motion = Animations.Named(
                animPrefix + "PeerLocal",
                () => Animations.PresenceClip(AvrcAnimations.LocalState.PeerLocal, Binding.role)
            );
            t = AddInstantTransition(init, peerLocal);
            AddParameter(Names.SignalLocal(Binding.role.Other()).ParamName, AnimatorControllerParameterType.Float);
            t.AddCondition(AnimatorConditionMode.Greater, 0.5f, Names.SignalLocal(Binding.role.Other()).ParamName);
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsLocal");

            peerLocal.behaviours = new StateMachineBehaviour[]
            {
                ParameterDriver(localOnly: false,
                    (Names.PubParamEitherLocal, 1),
                    (Names.PubParamPeerLocal, 1),
                    (Names.PubParamPeerPresent, 1)
                )
            };

            CreateTimeoutStates(rootStateMachine, peerLocal, init,
                t_ => t_.AddCondition(
                    AnimatorConditionMode.Greater,
                    0.5f,
                    Names.SignalLocal(Binding.role.Other()).ParamName
                ));

            var peerPresent = rootStateMachine.AddState("PeerPresent");
            peerPresent.motion = init.motion;
            t = AddInstantTransition(init, peerPresent);
            AddPilotCondition(t);
            t.AddCondition(AnimatorConditionMode.Less, 0.5f, Names.SignalLocal(Binding.role.Other()).ParamName);
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
            t.AddCondition(AnimatorConditionMode.Greater, 0.5f, Names.SignalLocal(Binding.role.Other()).ParamName);

            CreateTimeoutStates(rootStateMachine, peerPresent, init, t_ => AddPilotCondition(t_));

            return rootStateMachine;
        }

        private void CreateTimeoutStates(AnimatorStateMachine rootStateMachine,
            AnimatorState present,
            AnimatorState afterTimeout,
            AddTransition param)
        {
            AnimatorStateTransition t;
            var timeout = rootStateMachine.AddState(present.name + "Timeout");
            timeout.motion = afterTimeout.motion;
            t = AddInstantTransition(present, timeout);
            t.exitTime = 0.5f;
            t.hasExitTime = true;
            timeout.motion = present.motion;

            t = AddInstantTransition(timeout, present);
            param(t);

            t = AddInstantTransition(timeout, afterTimeout);
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

        protected void AddIsLocalCondition(AnimatorStateTransition transition, bool isLocal = true)
        {
            AddParameter("IsLocal", AnimatorControllerParameterType.Bool);
            transition.AddCondition(isLocal ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 1, "IsLocal");
        }

        protected Vector3 pos(float x, float y)
        {
            return new Vector3((x + 1) * 400, y * -80, 0);
        }

        private delegate void AddTransition(AnimatorStateTransition transition);

        protected delegate AnimatorStateTransition TransitionProvider();

        protected delegate void EqualsCondition(AnimatorStateTransition transition, int index);

        protected delegate void NotEqualsCondition(AnimatorStateTransition transition, int index);

        protected delegate void DriveParameter(VRCAvatarParameterDriver driver, int index);
    }
}