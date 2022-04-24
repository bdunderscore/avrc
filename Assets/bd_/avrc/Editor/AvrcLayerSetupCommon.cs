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
        protected const string IS_LOCAL = "IsLocal";

        protected readonly AvrcAnimations Animations;
        protected readonly AnimatorController AnimatorController;

        protected readonly VRCAvatarDescriptor Avatar;
        protected readonly AvrcBindingConfiguration Binding;
        protected readonly AvrcLinkSpec LinkSpec;
        protected readonly AvrcNames Names;
        protected readonly AvrcObjects Objects;
        private readonly ImmutableDictionary<string, SignalMapping> parameterBindings;
        protected readonly SignalEncoding SignalEncoding;

        private readonly ImmutableHashSet<string> syncedParameters;
        protected readonly float Timeout;

        protected AvrcLayerSetupCommon(VRCAvatarDescriptor avatarDescriptor, AvrcBindingConfiguration binding)
        {
            Avatar = avatarDescriptor;
            Binding = binding;
            Names = new AvrcNames(binding);
            LinkSpec = binding.linkSpec;
            SignalEncoding = new SignalEncoding(LinkSpec, binding.role, Names.LayerPrefix);
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
            parameterBindings = Binding.signalMappings.Select(m =>
                new KeyValuePair<string, SignalMapping>(m.avrcSignalName, m)
            ).ToImmutableDictionary();
        }

        protected SignalMapping GetParamBinding(AvrcSignal signal)
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

        private void ValidateParameter(ref AnimatorControllerParameter param, AnimatorControllerParameterType ty,
            bool? defaultBool, int? defaultInt)
        {
            if (param.type != ty)
                throw new ArgumentException(
                    $"Animator controller already has a signal named ${param.name} but with the wrong type");

            if (defaultBool != null) param.defaultBool = defaultBool.Value;
            if (defaultInt != null) param.defaultInt = defaultInt.Value;
        }

        protected void AddParameter(string name, AnimatorControllerParameterType ty, bool? defaultBool = null,
            int? defaultInt = null)
        {
            for (var i = 0; i < AnimatorController.parameters.Length; i++)
            {
                if (AnimatorController.parameters[i].name == name)
                {
                    var param = AnimatorController.parameters[i];
                    ValidateParameter(ref param, ty, defaultBool, defaultInt);
                    AnimatorController.parameters[i] = param;
                    return;
                }
            }

            var parameters =
                new AnimatorControllerParameter[AnimatorController.parameters.Length + 1];
            Array.Copy(AnimatorController.parameters, parameters, AnimatorController.parameters.Length);

            parameters[parameters.Length - 1] = new AnimatorControllerParameter
            {
                name = name,
                type = ty,
                defaultBool = defaultBool ?? false,
                defaultInt = defaultInt ?? 0
            };

            AnimatorController.parameters = parameters;
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
            AddParameter(Names.ParamSecretActive, AnimatorControllerParameterType.Bool);

            foreach (var collider in SignalEncoding.AllContacts)
                if (!collider.IsSender)
                {
                    //AddParameter(collider.Parameter, AnimatorControllerParameterType.Bool, defaultBool: false);
                    AddParameter(collider.Parameter, AnimatorControllerParameterType.Float, false);
                    AddParameter(collider.SenseParameter, AnimatorControllerParameterType.Int, defaultInt: 255);
                }

            var init = rootStateMachine.AddState("Init", pos(1, 3.5f));
            init.motion = Animations.Named(
                animPrefix + "Init",
                () => Animations.PresenceClip(AvrcAnimations.LocalState.Unknown, SignalEncoding)
            );
            init.behaviours = new StateMachineBehaviour[]
            {
                ParameterDriver(localOnly: false,
                    (Names.PubParamEitherLocal, 0),
                    (Names.PubParamPeerLocal, 0),
                    (Names.PubParamPeerPresent, 0),
                    (Names.ParamSecretActive, 0)
                )
            };

            var ownerLocal = rootStateMachine.AddState("OwnerLocal", pos(1, 0));
            ownerLocal.motion = Animations.Named(
                animPrefix + "OwnerLocal",
                () => Animations.PresenceClip(AvrcAnimations.LocalState.OwnerLocal, SignalEncoding)
            );
            var t = AddInstantTransition(init, ownerLocal);
            AddIsLocalCondition(t);
            ownerLocal.behaviours = new StateMachineBehaviour[]
            {
                ParameterDriver(localOnly: false,
                    (Names.PubParamEitherLocal, 1),
                    (Names.PubParamPeerLocal, 0),
                    (Names.PubParamPeerPresent, 0),
                    (Names.ParamSecretActive, 0)
                )
            };

            var ownerLocalTxPresent = rootStateMachine.AddState("OwnerLocalTxPresent", pos(2, -1));
            ownerLocalTxPresent.motion = ownerLocal.motion;
            ownerLocalTxPresent.behaviours = new StateMachineBehaviour[]
            {
                ParameterDriver(localOnly: false,
                    (Names.PubParamEitherLocal, 1),
                    (Names.PubParamPeerLocal, 0),
                    (Names.PubParamPeerPresent, 1),
                    (Names.ParamSecretActive, 1)
                )
            };
            t = AddInstantTransition(ownerLocal, ownerLocalTxPresent);
            SignalEncoding.TheirPilotLocal.Target.AddPresenceCondition(t);

            CreateTimeoutStates(rootStateMachine, ownerLocalTxPresent, ownerLocal,
                SignalEncoding.TheirPilotLocal.Target.AddPresenceCondition,
                pos(2, 1));

            var peerLocalCheck0 = rootStateMachine.AddState("PeerLocalCheck0", pos(2, 3));
            var peerLocalCheck1 = rootStateMachine.AddState("PeerLocalCheck1", pos(3, 3));
            peerLocalCheck0.motion = peerLocalCheck1.motion = init.motion;
            // 1s is too short for the slow probe we use prior to connection establishment, so wait 2s here.
            peerLocalCheck0.speed = peerLocalCheck1.speed = ((AnimationClip) init.motion).length / 2.0f;

            t = AddInstantTransition(init, peerLocalCheck0);
            SignalEncoding.TheirPilotLocal.AddCondition(t);
            t.AddCondition(AnimatorConditionMode.IfNot, 0, IS_LOCAL);
            // Verify that we transition true -> false -> true before accepting the peer-is-local condition
            t = AddInstantTransition(peerLocalCheck0, peerLocalCheck1);
            t.AddCondition(AnimatorConditionMode.Less, 0.5f, SignalEncoding.TheirPilotLocal.Target.Parameter);

            var peerLocal = rootStateMachine.AddState("PeerLocal", pos(4, 3));
            peerLocal.motion = Animations.Named(
                animPrefix + "PeerLocal",
                () => Animations.PresenceClip(AvrcAnimations.LocalState.PeerLocal, SignalEncoding)
            );
            t = AddInstantTransition(peerLocalCheck1, peerLocal);
            SignalEncoding.TheirPilotLocal.AddCondition(t);

            // Timeout transitions for peerLocalCheck states
            t = peerLocalCheck0.AddTransition(init);
            t.exitTime = 1.5f;
            t.hasExitTime = true;
            t.duration = 0;

            t = peerLocalCheck1.AddTransition(init);
            t.exitTime = 2.5f;
            t.hasExitTime = true;
            t.duration = 0;

            peerLocal.behaviours = new StateMachineBehaviour[]
            {
                ParameterDriver(localOnly: false,
                    (Names.PubParamEitherLocal, 1),
                    (Names.PubParamPeerLocal, 1),
                    (Names.PubParamPeerPresent, 1),
                    (Names.ParamSecretActive, 1)
                )
            };

            CreateTimeoutStates(rootStateMachine, peerLocal, init,
                SignalEncoding.TheirPilotLocal.AddCondition,
                pos(2, 2)
            );

            var peerPresent = rootStateMachine.AddState("PeerPresent", pos(2, 4));
            peerPresent.motion = init.motion;
            t = AddInstantTransition(init, peerPresent);
            SignalEncoding.TheirPilotNotLocal.AddCondition(t);
            t.AddCondition(AnimatorConditionMode.IfNot, 0, IS_LOCAL);

            peerPresent.behaviours = new StateMachineBehaviour[]
            {
                ParameterDriver(localOnly: false,
                    (Names.PubParamEitherLocal, 0),
                    (Names.PubParamPeerLocal, 0),
                    (Names.PubParamPeerPresent, 1),
                    (Names.ParamSecretActive, 0)
                )
            };

            t = AddInstantTransition(peerPresent, peerLocalCheck0);
            SignalEncoding.TheirPilotLocal.AddCondition(t);

            CreateTimeoutStates(rootStateMachine, peerPresent, init, SignalEncoding.TheirPilotNotLocal.AddCondition,
                pos(2, 5));

            return rootStateMachine;
        }

        /// <summary>
        ///     Generates a layer which will probe for received values.
        /// </summary>
        /// <returns></returns>
        protected AnimatorStateMachine ProbeLayer()
        {
            var stateMachine = new AnimatorStateMachine();

            stateMachine.entryPosition = pos(-1, 0);
            stateMachine.anyStatePosition = pos(-1, 2);
            stateMachine.exitPosition = pos(-1, -1);

            var standbyPresent = stateMachine.AddState("Standby:Present:Prep", pos(-1.5f, -0.5f));
            standbyPresent.motion = Animations.Named("Standby:Present", () =>
            {
                var _clip = new AnimationClip();
                SignalEncoding.TheirPilotNotLocal.AddClip(Names, _clip, 0.5f);
                return _clip;
            });
            standbyPresent.behaviours = new[] {ParameterDriver(false, SignalEncoding.TheirPilotNotLocal.DelayDriver)};

            var standbyPresentCheck = stateMachine.AddState("Standby:Present:Check", pos(-1.5f, 0.5f));
            standbyPresentCheck.motion = standbyPresent.motion;
            standbyPresentCheck.behaviours = new[]
                {ParameterDriver(false, SignalEncoding.TheirPilotNotLocal.ProbeDriver)};

            var t = standbyPresent.AddTransition(standbyPresentCheck);
            t.exitTime = 1;
            t.hasExitTime = true;
            t.hasFixedDuration = false;

            var standbyRest = stateMachine.AddState("Standby:Rest", pos(-1.5f, 1.5f));
            standbyRest.motion = Animations.Named("Standby:Rest", () =>
            {
                var _clip = new AnimationClip();
                SignalEncoding.TheirPilotRest.AddClip(Names, _clip, 0.5f);
                return _clip;
            });
            standbyRest.behaviours = new[] {ParameterDriver(false, SignalEncoding.TheirPilotRest.DelayDriver)};

            t = standbyPresentCheck.AddTransition(standbyRest);
            t.exitTime = 1;
            t.hasExitTime = true;
            t.hasFixedDuration = false;

            var standbyLocal = stateMachine.AddState("Standby:Local:Prep", pos(-0.5f, -0.5f));
            standbyLocal.motion = Animations.Named("Standby:Local", () =>
            {
                var _clip = new AnimationClip();
                SignalEncoding.TheirPilotLocal.AddClip(Names, _clip, 0.5f);
                return _clip;
            });
            standbyLocal.behaviours = new[] {ParameterDriver(false, SignalEncoding.TheirPilotLocal.DelayDriver)};

            var standbyLocalCheck = stateMachine.AddState("Standby:Local:Check", pos(-0.5f, 0.5f));
            standbyLocalCheck.motion = standbyLocal.motion;
            standbyLocalCheck.behaviours = new[] {ParameterDriver(false, SignalEncoding.TheirPilotLocal.ProbeDriver)};

            t = standbyLocal.AddTransition(standbyLocalCheck);
            t.exitTime = 1;
            t.hasExitTime = true;
            t.hasFixedDuration = false;

            t = AddInstantTransition(standbyLocalCheck, standbyPresent);
            //t.AddCondition(AnimatorConditionMode.IfNot, 0, SignalEncoding.TheirPilotLocal.Target.Parameter);
            t.AddCondition(AnimatorConditionMode.IfNot, 0, Names.PubParamEitherLocal);

            t = AddInstantTransition(standbyRest, standbyLocal);
            //t.AddCondition(AnimatorConditionMode.IfNot, 0, SignalEncoding.TheirPilotNotLocal.Target.Parameter);
            t.AddCondition(AnimatorConditionMode.IfNot, 0, Names.PubParamEitherLocal);

            var shutdownState = stateMachine.AddState("Shutdown", pos(-1, -1));
            t = AddInstantTransition(shutdownState, standbyPresent);
            t.AddCondition(AnimatorConditionMode.If, 0, IS_LOCAL);
            t = AddInstantTransition(shutdownState, standbyPresent);
            t.AddCondition(AnimatorConditionMode.IfNot, 0, IS_LOCAL);

            var resetBehaviour = SignalEncoding.ProbePhases[0].BuildProbeDriver(false);
            shutdownState.behaviours = new StateMachineBehaviour[] {resetBehaviour};

            var nStates = SignalEncoding.ProbePhases.Count * 3;
            float radius = 25 * nStates;
            var cpos = Vector3.up * radius;
            var rot = Quaternion.Euler(0, 0, 360.0f / nStates);

            var allStates = new List<AnimatorState>();
            var probeStates = new List<AnimatorState>();
            AnimatorState lastState = null;
            foreach (var probePhase in SignalEncoding.ProbePhases)
            {
                var i = probeStates.Count;

                var curStateProbe = stateMachine.AddState("Probe: " + i, cpos);
                cpos = rot * cpos;
                curStateProbe.behaviours = new StateMachineBehaviour[] {probePhase.BuildProbeDriver(false)};
                curStateProbe.motion = Animations.Named("Probe: " + i, () => probePhase.ProbeClip(Names));
                allStates.Add(curStateProbe);

                var curStateSample = stateMachine.AddState("Sample: " + i, cpos);
                cpos = rot * cpos;
                curStateSample.motion = curStateProbe.motion;
                curStateSample.behaviours = new StateMachineBehaviour[] {probePhase.BuildProbeDriver(true)};
                allStates.Add(curStateSample);

                var curStateSample2 = stateMachine.AddState("Sample2: " + i, cpos);
                cpos = rot * cpos;
                curStateSample2.motion = curStateProbe.motion;
                allStates.Add(curStateSample2);

                t = curStateProbe.AddTransition(curStateSample);
                t.exitTime = 1;
                t.hasExitTime = true;
                t.hasFixedDuration = false;

                t = AddInstantTransition(curStateSample, curStateSample2);
                t.AddCondition(AnimatorConditionMode.If, 0, Names.PubParamEitherLocal);
                t = AddInstantTransition(curStateSample, curStateSample2);
                t.AddCondition(AnimatorConditionMode.IfNot, 0, Names.PubParamEitherLocal);

                probeStates.Add(curStateProbe);

                if (lastState == null)
                {
                    // Add entry transitions
                    t = AddInstantTransition(standbyLocalCheck, curStateProbe);
                    t.AddCondition(AnimatorConditionMode.If, 0, Names.PubParamEitherLocal);

                    t = AddInstantTransition(standbyPresentCheck, curStateProbe);
                    t.AddCondition(AnimatorConditionMode.If, 0, Names.PubParamEitherLocal);
                }
                else
                {
                    // Always-true condition
                    t = AddInstantTransition(lastState, curStateProbe);
                    t.AddCondition(AnimatorConditionMode.If, 0, Names.PubParamEitherLocal);
                    t = AddInstantTransition(lastState, curStateProbe);
                    t.AddCondition(AnimatorConditionMode.IfNot, 0, Names.PubParamEitherLocal);
                }

                lastState = curStateSample2;
            }

            // Final state exit and loop
            t = AddInstantTransition(lastState, probeStates.First());
            t.AddCondition(AnimatorConditionMode.If, 0, Names.PubParamEitherLocal);

            t = AddInstantTransition(lastState, shutdownState);
            t.AddCondition(AnimatorConditionMode.IfNot, 0, Names.PubParamEitherLocal);

            return stateMachine;
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
            AddParameter(IS_LOCAL, AnimatorControllerParameterType.Bool);
            transition.AddCondition(isLocal ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 1, IS_LOCAL);
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