using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace net.fushizen.avrc
{
    [SuppressMessage("ReSharper", "HeapView.ObjectAllocation.Evident")]
    [SuppressMessage("ReSharper", "HeapView.BoxingAllocation")]
    internal class AvrcRxStateMachines : AvrcLayerSetupCommon
    {
        private AvrcRxStateMachines(VRCAvatarDescriptor avatarDescriptor, AvrcBindingConfiguration binding)
            : base(avatarDescriptor, binding)
        {
        }

        public static void SetupRx(VRCAvatarDescriptor avatarDescriptor, AvrcBindingConfiguration binding)
        {
            new AvrcRxStateMachines(avatarDescriptor, binding).Setup();
        }

        private void Setup()
        {
            CreateGlobalDefaultsLayer();

            // Enable the constraint that places our receiver triggers at the correct location
            AddOrReplaceLayer(Names.LayerSetup, ReceiverSetupLayer());
            AddOrReplaceLayer(Names.LayerProbe, ProbeLayer());
            // Set up a mesh to expand our bounding box locally for the transmitter
            // AddOrReplaceLayer("_AVRC_" + Names.Prefix + "_RXBounds", BoundsSetupStateMachine());

            foreach (var param in LinkSpec.signals)
            {
                CreateParameterLayer(param);
            }

            AvrcAnimatorUtils.GarbageCollectAnimatorAsset(AnimatorController);
            EditorUtility.SetDirty(AnimatorController);
        }

        private AnimatorStateMachine ReceiverSetupLayer()
        {
            return CommonSetupLayer(
                Names.Prefix + "_EnableRX_"
            );
        }

        protected override AnimatorStateMachine TwoWayParamLayer(AvrcSignal signal, int values,
            EqualsCondition equalsCondition,
            NotEqualsCondition notEqualsCondition, DriveParameter driveParameter)
        {
            return FiniteParamLayer(true, signal, values, equalsCondition, notEqualsCondition, driveParameter);
        }

        protected override AnimatorStateMachine OneWayParamLayer(
            AvrcSignal signal,
            int values,
            EqualsCondition equalsCondition,
            NotEqualsCondition notEqualsCondition,
            DriveParameter driveParameter
        )
        {
            return FiniteParamLayer(false, signal, values, equalsCondition, notEqualsCondition, driveParameter);
        }

        AnimatorStateMachine FiniteParamLayer(
            bool twoWay,
            AvrcSignal signal,
            int values,
            EqualsCondition equalsCondition,
            NotEqualsCondition notEqualsCondition,
            DriveParameter driveParameter
        )
        {
            var binding = GetParamBinding(signal);
            var presenceSignal = binding.isSecret ? Names.PubParamEitherLocal : Names.PubParamPeerPresent;
            var driverIsLocal = HasSyncedParameter(Names.SignalMap[signal.name]);

            var stateMachine = new AnimatorStateMachine();

            var localDriven = new AnimatorState[values];
            var remoteDriven = new AnimatorState[localDriven.Length];

            var conditionParamName = Names.InternalParameter(signal);
            AddParameter(conditionParamName, AnimatorControllerParameterType.Float);

            var startup = stateMachine.AddState("Startup", pos(0, 0));

            AnimatorState idleState;
            // TODO: When signal is synced, only activate for local
            var subStateMachine = createIdleStateMachine(
                signal,
                Binding.signalMappings.Find(mapping => mapping.avrcSignalName == signal.name),
                out idleState,
                state =>
                {
                    var t = AddInstantTransition(state, startup);
                    t.AddCondition(AnimatorConditionMode.If, 0, presenceSignal);
                },
                t => { t.AddCondition(AnimatorConditionMode.IfNot, 0, Names.PubParamPeerPresent); }
            );
            stateMachine.stateMachines = new[]
            {
                new ChildAnimatorStateMachine
                {
                    position = pos(0, 1),
                    stateMachine = subStateMachine
                }
            }.ToArray();

            var transition = AddInstantTransition(startup, idleState);
            transition.AddCondition(AnimatorConditionMode.IfNot, 0, presenceSignal);

            var drivenByTx = stateMachine.AddState("DrivenByTx", pos(2, 0));
            drivenByTx.motion = AvrcAssets.EmptyClip();

            transition = AddInstantTransition(drivenByTx, startup);
            transition.AddCondition(AnimatorConditionMode.IfNot, 0, presenceSignal);

            for (var i = 0; i < localDriven.Length; i++)
            {
                localDriven[i] = stateMachine.AddState($"LD_{signal.name}_{i}",
                    pos(1, i - localDriven.Length / 2.0f));
                remoteDriven[i] = stateMachine.AddState($"RD_{signal.name}_{i}",
                    pos(3, i + 0.5f - localDriven.Length / 2.0f));

                var driver = ParameterDriver(driverIsLocal);
                remoteDriven[i].behaviours = new StateMachineBehaviour[] {driver};
                driveParameter(driver, i);

                localDriven[i].motion = AvrcAssets.EmptyClip();

                if (twoWay)
                {
                    localDriven[i].motion = Animations.Named(
                        $"{Names.Prefix}_{signal.name}_{i}_ACK",
                        () => SignalEncoding.SignalDrivers[signal.AckSignalName][i].Clip(Names)
                    );
                }

                remoteDriven[i].motion = localDriven[i].motion;

                // TODO: Set hysteresis hold?

                // Transition from start state based on local state
                transition = AddInstantTransition(startup, localDriven[i]);
                equalsCondition(transition, i);
                transition.AddCondition(AnimatorConditionMode.If, 0, presenceSignal);

                // Transition back to start state on local change (if not two-way)
                transition = AddInstantTransition(localDriven[i], startup);
                notEqualsCondition(transition, i);

                // Transition to receive when remote side drives a new value.
                transition = AddInstantTransition(localDriven[i], drivenByTx);
                SignalEncoding.SignalDrivers[signal.SignalName][i].AddOtherValueCondition(transition);
                transition.AddCondition(AnimatorConditionMode.If, 0, presenceSignal);

                // Transition back from TX driven state after receive
                transition = AddInstantTransition(drivenByTx, remoteDriven[i]);
                SignalEncoding.SignalDrivers[signal.SignalName][i].AddCondition(transition);

                // And back to local driven once we update the signal. This needs to be an always-true transition.
                transition = AddInstantTransition(remoteDriven[i], localDriven[i]);
                transition.AddCondition(AnimatorConditionMode.If, 0, presenceSignal);
                transition = AddInstantTransition(remoteDriven[i], localDriven[i]);
                transition.AddCondition(AnimatorConditionMode.IfNot, 0, presenceSignal);

                // Deactivation transition
                transition = AddInstantTransition(localDriven[i], startup);
                transition.AddCondition(AnimatorConditionMode.IfNot, 0, presenceSignal);
            }

            return stateMachine;
        }

        private AnimatorStateMachine createIdleStateMachine(
            AvrcSignal signal,
            SignalMapping mapping,
            out AnimatorState entryState,
            AddExitTransition addExitTransition,
            AddContinueCondition addContinueCondition
        )
        {
            var dstName = Names.SignalMap[mapping.avrcSignalName];
            var localOnly = IsParameterSynced(dstName);

            switch (mapping.noSignalMode)
            {
                case NoSignalMode.Hold:
                case NoSignalMode.Reset:
                {
                    var stateMachine = new AnimatorStateMachine();
                    stateMachine.name = "Reset " + dstName;
                    entryState = stateMachine.AddState("Entry", new Vector3(0, 0, 0));
                    entryState.motion = AvrcAssets.EmptyClip();
                    entryState.writeDefaultValues = false;

                    if (mapping.noSignalMode == NoSignalMode.Reset)
                        entryState.behaviours = new StateMachineBehaviour[]
                        {
                            ParameterDriver(localOnly, (dstName, mapping.defaultValue))
                        };

                    addExitTransition(entryState);
                    return stateMachine;
                }
                case NoSignalMode.Forward:
                {
                    switch (signal.type)
                    {
                        case AvrcSignalType.Bool:
                            return createBoolCopyStateMachine(
                                localOnly,
                                mapping.forwardParameter,
                                dstName,
                                out entryState,
                                addExitTransition,
                                addContinueCondition
                            );
                        case AvrcSignalType.Int:
                            return createIntCopyStateMachine(
                                localOnly,
                                mapping.forwardParameter,
                                dstName,
                                out entryState,
                                addExitTransition,
                                addContinueCondition
                            );
                    }

                    break;
                }
            }

            throw new Exception("Unknown NoSignalMode or signal type");
        }

        private bool IsParameterSynced(string name)
        {
            var av3params = Avatar.expressionParameters;
            if (av3params == null) return false;

            return av3params.parameters.Any(p => p.name == name);
        }

        private AnimatorStateMachine createBoolCopyStateMachine(
            bool localOnly,
            string srcParam,
            string dstParam,
            out AnimatorState entryState,
            AddExitTransition addExitTransition,
            AddContinueCondition addContinueCondition
        )
        {
            var stateMachine = new AnimatorStateMachine();
            stateMachine.name = $"Copy {srcParam} -> {dstParam}";

            entryState = stateMachine.AddState("Entry", new Vector3(0, 0, 0));
            entryState.motion = AvrcAssets.EmptyClip();
            entryState.writeDefaultValues = false;
            addExitTransition(entryState);

            var stTrue = stateMachine.AddState("True", new Vector3(200, -40, 0));
            stTrue.motion = AvrcAssets.EmptyClip();
            stTrue.writeDefaultValues = false;
            addExitTransition(stTrue);

            var stFalse = stateMachine.AddState("True", new Vector3(200, 40, 0));
            stFalse.motion = AvrcAssets.EmptyClip();
            stFalse.writeDefaultValues = false;
            addExitTransition(stFalse);

            var t = entryState.AddTransition(stTrue);
            addContinueCondition(t);
            t.AddCondition(AnimatorConditionMode.If, 0, srcParam);

            t = stFalse.AddTransition(stTrue);
            addContinueCondition(t);
            t.AddCondition(AnimatorConditionMode.If, 0, srcParam);

            t = entryState.AddTransition(stFalse);
            addContinueCondition(t);
            t.AddCondition(AnimatorConditionMode.IfNot, 0, srcParam);

            t = stTrue.AddTransition(stFalse);
            addContinueCondition(t);
            t.AddCondition(AnimatorConditionMode.IfNot, 0, srcParam);

            stTrue.behaviours = new StateMachineBehaviour[] {ParameterDriver(localOnly, (dstParam, 1))};
            stFalse.behaviours = new StateMachineBehaviour[] {ParameterDriver(localOnly, (dstParam, 0))};

            return stateMachine;
        }

        private AnimatorStateMachine createIntCopyStateMachine(
            bool localOnly,
            string srcParam,
            string dstParam,
            out AnimatorState entryState,
            AddExitTransition addExitTransition,
            AddContinueCondition addContinueCondition
        )
        {
            var stateMachine = new AnimatorStateMachine();
            stateMachine.name = $"Copy {srcParam} -> {dstParam}";

            entryState = stateMachine.AddState("Entry", new Vector3(0, 0, 0));
            entryState.motion = AvrcAssets.EmptyClip();
            entryState.writeDefaultValues = false;

            addExitTransition(entryState);

            // Branch based on top nibble
            float yspacing = -40;
            float xspacing = 400;
            var y = yspacing * -0x80;
            var x = xspacing;

            var hiNibble = new AnimatorState[0x10];

            for (var i = 0; i < 0x10; i++)
            {
                var lo = (i << 4) - 1;
                var hi = (i + 1) << 4;

                var state = stateMachine.AddState($"HiNibble 0x{i:X}", new Vector3(x, y, 0));
                hiNibble[i] = state;
                y += 0x10 * yspacing;
                state.motion = AvrcAssets.EmptyClip();
                state.writeDefaultValues = false;

                var t = AddInstantTransition(entryState, state);
                if (i != 0) t.AddCondition(AnimatorConditionMode.Greater, lo, srcParam);
                if (i != 0xf) t.AddCondition(AnimatorConditionMode.Less, hi, srcParam);
                addContinueCondition(t);

                // Transition out of the HiNibble state when out of range, to avoid getting stuck if we have a race
                t = AddInstantTransition(state, entryState);
                t.AddCondition(AnimatorConditionMode.Less, lo + 1, srcParam);
                t = AddInstantTransition(state, entryState);
                t.AddCondition(AnimatorConditionMode.Greater, hi - 1, srcParam);
            }

            x += xspacing;
            y = yspacing * -0x80;
            for (var i = 0; i < 0x100; i++)
            {
                var state = stateMachine.AddState($"LoNibble 0x{i:X02}", new Vector3(x, y, 0));

                y += yspacing;
                state.motion = AvrcAssets.EmptyClip();
                state.writeDefaultValues = false;

                var t = AddInstantTransition(hiNibble[i >> 4], state);
                t.AddCondition(AnimatorConditionMode.Equals, i, srcParam);

                t = AddInstantTransition(state, entryState);
                t.AddCondition(AnimatorConditionMode.NotEqual, i, srcParam);

                state.behaviours = new StateMachineBehaviour[] {ParameterDriver(localOnly, (dstParam, i))};

                addExitTransition(state);
            }

            return stateMachine;
        }

        private delegate void AddExitTransition(AnimatorState state);

        private delegate void AddContinueCondition(AnimatorStateTransition transition);
    }
}