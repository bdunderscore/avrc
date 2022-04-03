using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
        protected readonly AvrcLinkSpec LinkSpec;
        protected readonly AvrcNames Names;
        protected readonly AvrcObjects Objects;
        private readonly ImmutableDictionary<string, ParameterMapping> parameterBindings;

        private readonly ImmutableHashSet<string> syncedParameters;
        protected readonly float Timeout;

        protected AvrcLayerSetupCommon(VRCAvatarDescriptor avatarDescriptor, AvrcBindingConfiguration binding)
        {
            Avatar = avatarDescriptor;
            Binding = binding;
            Names = new AvrcNames(binding);
            LinkSpec = binding.linkSpec;
            Timeout = Mathf.Max(1.0f, binding.timeoutSeconds);

            Animations = new AvrcAnimations(LinkSpec, Names);
            Objects = new AvrcObjects(LinkSpec, Names, Binding.role);

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

            syncedParameters = avatarDescriptor.expressionParameters.parameters.Select(p => p.name)
                .ToImmutableHashSet();
            parameterBindings = Binding.parameterMappings.Select(m =>
                new KeyValuePair<string, ParameterMapping>(m.avrcParameterName, m)
            ).ToImmutableDictionary();
        }

        protected ParameterMapping GetParamBinding(AvrcSignal signal)
        {
            return parameterBindings[signal.name];
        }

        protected bool HasSyncedParameter(string name)
        {
            return syncedParameters.Contains(name);
        }

        protected void CreateGlobalDefaultsLayer()
        {
            var defaultsStateMachine = new AnimatorStateMachine();
            var state = defaultsStateMachine.AddState("Defaults");
            state.motion = Animations.Named("AVRC_Defaults",
                () => Animations.GlobalDefaultsClip()
            );

            AddOrReplaceLayer("_AVRC_Defaults", defaultsStateMachine, GlobalLayerType.GlobalDefaults);

            int firstAvrcLayerIndex = -1, globalDefaultsIndex = -1;
            var layers = AnimatorController.layers;
            for (var i = 0; i < layers.Length; i++)
            {
                GlobalLayerType layerType;
                if (AvrcLayerMarker.IsAvrcLayer(layers[i], out layerType))
                {
                    if (firstAvrcLayerIndex == -1) firstAvrcLayerIndex = i;
                    if (layerType == GlobalLayerType.GlobalDefaults) globalDefaultsIndex = i;
                }
            }

            if (firstAvrcLayerIndex < globalDefaultsIndex)
            {
                var defaultsLayer = layers[globalDefaultsIndex];
                Array.Copy(
                    layers, firstAvrcLayerIndex,
                    layers, firstAvrcLayerIndex + 1,
                    globalDefaultsIndex - firstAvrcLayerIndex
                );
                layers[firstAvrcLayerIndex] = defaultsLayer;
                AnimatorController.layers = layers;
            }
        }

        protected void AddPilotCondition(AnimatorStateTransition t, bool present = true)
        {
            foreach (var pilot in Names.PilotContacts(Binding.role.Other()))
            {
                AddParameter(pilot.ParamName, AnimatorControllerParameterType.Float);
                t.AddCondition(present ? AnimatorConditionMode.Greater : AnimatorConditionMode.Less, 0.5f,
                    pilot.ParamName);
            }
        }

        protected void AddSignalCondition(AnimatorStateTransition t, AvrcSignal signal, int index,
            bool ack)
        {
            foreach (var bit in Names.SignalContacts(signal, ack))
            {
                var mode = (index & 1) == 1 ? AnimatorConditionMode.Greater : AnimatorConditionMode.Less;
                AddParameter(bit.ParamName, AnimatorControllerParameterType.Float);
                t.AddCondition(mode, 0.5f, bit.ParamName);
                index >>= 1;
            }
        }

        protected void AddAntiSignalConditions(AvrcSignal signal, int index, bool ack,
            TransitionProvider transitionProvider)
        {
            foreach (var bit in Names.SignalContacts(signal, ack))
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
                            $"Animator controller already has a signal named ${name} but with the wrong type");
                    }

                    return;
                }
            }

            AnimatorController.AddParameter(name, ty);
        }

        protected void CreateParameterLayer(AvrcSignal signal)
        {
            switch (signal.type)
            {
                case AvrcSignalType.Bool:
                    AddParameter(Names.SignalToParam(signal), AnimatorControllerParameterType.Bool);
                    break;
                case AvrcSignalType.Int:
                    AddParameter(Names.SignalToParam(signal), AnimatorControllerParameterType.Int);
                    break;
            }

            AnimatorStateMachine animatorStateMachine = null;
            switch (signal.type)
            {
                case AvrcSignalType.Bool:
                {
                    EqualsCondition eq = (transition, index) =>
                    {
                        transition.AddCondition(
                            index != 0 ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
                            0, Names.SignalToParam(signal)
                        );
                    };
                    NotEqualsCondition neq = (transition, index) => eq(transition, index != 0 ? 0 : 1);
                    DriveParameter drive = (driver, index) =>
                    {
                        driver.parameters.Add(new VRC_AvatarParameterDriver.Parameter()
                        {
                            name = Names.SignalToParam(signal),
                            type = VRC_AvatarParameterDriver.ChangeType.Set,
                            value = index
                        });
                    };

                    if (signal.syncDirection == SyncDirection.OneWay)
                    {
                        animatorStateMachine = OneWayParamLayer(signal, 2, eq, neq, drive);
                    }
                    else
                    {
                        animatorStateMachine = TwoWayParamLayer(signal, 2, eq, neq, drive);
                    }

                    break;
                }
                case AvrcSignalType.Int:
                {
                    var values = signal.maxVal - signal.minVal + 1;

                    EqualsCondition eq = (transition, index) =>
                    {
                        transition.AddCondition(
                            AnimatorConditionMode.Equals,
                            index + signal.minVal,
                            Names.SignalToParam(signal)
                        );
                    };
                    NotEqualsCondition neq = (transition, index) =>
                    {
                        transition.AddCondition(
                            AnimatorConditionMode.NotEqual,
                            index + signal.minVal,
                            Names.SignalToParam(signal)
                        );
                    };
                    DriveParameter drive = (driver, index) =>
                    {
                        driver.parameters.Add(new VRC_AvatarParameterDriver.Parameter()
                        {
                            name = Names.SignalToParam(signal),
                            type = VRC_AvatarParameterDriver.ChangeType.Set,
                            value = index + signal.minVal
                        });
                    };

                    if (signal.syncDirection == SyncDirection.OneWay)
                    {
                        animatorStateMachine = OneWayParamLayer(signal, values, eq, neq, drive);
                    }
                    else
                    {
                        animatorStateMachine = TwoWayParamLayer(signal, values, eq, neq, drive);
                    }

                    break;
                }

                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (animatorStateMachine != null)
            {
                AddOrReplaceLayer(Names.ParameterLayerName(signal), animatorStateMachine);
            }
            else
            {
                RemoveLayer(Names.ParameterLayerName(signal));
            }
        }

        protected void RemoveLayer(string layerName)
        {
            var newLayers = AnimatorController.layers
                .Where(layer => !layer.name.Equals(layerName))
                .ToArray();

            AnimatorController.layers = newLayers;
        }

        protected void AddOrReplaceLayer(
            string layerName,
            AnimatorStateMachine animatorStateMachine,
            GlobalLayerType globalLayerType = GlobalLayerType.NotGlobalLayer)
        {
            AuditWriteDefaults(animatorStateMachine);

            animatorStateMachine.name = layerName;
            AvrcLayerMarker.MarkLayer(
                animatorStateMachine,
                globalLayerType == GlobalLayerType.NotGlobalLayer ? LinkSpec : null,
                globalLayerType
            );

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

        private void AuditWriteDefaults(AnimatorStateMachine animatorStateMachine)
        {
            var queue = new Queue<AnimatorStateMachine>(new[] {animatorStateMachine});

            while (queue.Count > 0)
            {
                var nextSM = queue.Dequeue();
                foreach (var childStateMachine in nextSM.stateMachines) queue.Enqueue(childStateMachine.stateMachine);

                foreach (var state in nextSM.states)
                    state.state.writeDefaultValues = Binding.writeDefaults == WriteDefaultsState.YesWriteDefaults;
            }
        }

        protected abstract AnimatorStateMachine OneWayParamLayer(
            AvrcSignal signal,
            int values,
            EqualsCondition equalsCondition,
            NotEqualsCondition notEqualsCondition,
            DriveParameter driveParameter
        );

        protected abstract AnimatorStateMachine TwoWayParamLayer(
            AvrcSignal signal,
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

            var init = rootStateMachine.AddState("Init", pos(1, 3.5f));
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

            var ownerLocal = rootStateMachine.AddState("OwnerLocal", pos(1, 0));
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

            var ownerLocalTxPresent = rootStateMachine.AddState("OwnerLocalTxPresent", pos(2, -1));
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

            CreateTimeoutStates(rootStateMachine, ownerLocalTxPresent, ownerLocal, t_ => AddPilotCondition(t_),
                pos(2, 1));

            var peerLocal = rootStateMachine.AddState("PeerLocal", pos(2, 3));
            peerLocal.motion = Animations.Named(
                animPrefix + "PeerLocal",
                () => Animations.PresenceClip(AvrcAnimations.LocalState.PeerLocal, Binding.role)
            );
            t = AddInstantTransition(init, peerLocal);
            AddParameter(Names.LocalContacts(Binding.role.Other()).ParamName, AnimatorControllerParameterType.Float);
            t.AddCondition(AnimatorConditionMode.Greater, 0.5f, Names.LocalContacts(Binding.role.Other()).ParamName);
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
                    Names.LocalContacts(Binding.role.Other()).ParamName
                ),
                pos(2, 2)
            );

            var peerPresent = rootStateMachine.AddState("PeerPresent", pos(2, 4));
            peerPresent.motion = init.motion;
            t = AddInstantTransition(init, peerPresent);
            AddPilotCondition(t);
            t.AddCondition(AnimatorConditionMode.Less, 0.5f, Names.LocalContacts(Binding.role.Other()).ParamName);
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
            t.AddCondition(AnimatorConditionMode.Greater, 0.5f, Names.LocalContacts(Binding.role.Other()).ParamName);

            CreateTimeoutStates(rootStateMachine, peerPresent, init, t_ => AddPilotCondition(t_), pos(2, 5));

            return rootStateMachine;
        }

        private void CreateTimeoutStates(AnimatorStateMachine rootStateMachine,
            AnimatorState present,
            AnimatorState afterTimeout,
            AddTransition param,
            Vector2? pos = null
        )
        {
            AnimatorStateTransition t;
            AnimatorState timeout;

            if (pos != null)
                timeout = rootStateMachine.AddState(present.name + "Timeout", pos.Value);
            else
                timeout = rootStateMachine.AddState(present.name + "Timeout");

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