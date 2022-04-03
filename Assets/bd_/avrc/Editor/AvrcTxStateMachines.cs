using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;


// TODO: Need to have the owner of the TX avatar be authoritative over the transmission state machine.
// Otherwise we end up with the TX avatar RX clone (TX->RX avatar) ending transmission (or triggering a false
// transmission) before the true transmitter has applied the signal driver states.
namespace net.fushizen.avrc
{
    [SuppressMessage("ReSharper", "HeapView.ObjectAllocation.Evident")]
    internal class AvrcTxStateMachines : AvrcLayerSetupCommon
    {
        private AvrcTxStateMachines(VRCAvatarDescriptor avatarDescriptor, AvrcBindingConfiguration binding)
            : base(avatarDescriptor, binding)
        {
        }

        public static void SetupTx(VRCAvatarDescriptor avatarDescriptor, AvrcBindingConfiguration binding)
        {
            new AvrcTxStateMachines(avatarDescriptor, binding).Setup();
        }

        private void Setup()
        {
            AddBidirectionalTransferParameters();

            CreateGlobalDefaultsLayer();
            AddOrReplaceLayer(Names.LayerSetup, TransmitterSetupLayer());

            foreach (var param in LinkSpec.signals)
            {
                CreateParameterLayer(param);
            }

            AvrcAnimatorUtils.GarbageCollectAnimatorAsset(AnimatorController);
            EditorUtility.SetDirty(AnimatorController);
        }

        private AnimatorStateMachine TransmitterSetupLayer()
        {
            return CommonSetupLayer(
                Names.Prefix + "_EnableTX_"
            );
        }

        private void AddBidirectionalTransferParameters()
        {
            HashSet<string> knownParameters = new HashSet<string>();
            if (Avatar.expressionParameters == null)
            {
                throw new Exception("No expression parameters found");
            }

            foreach (var param in Avatar.expressionParameters.parameters)
            {
                knownParameters.Add(param.name);
            }

            int remaining = VRCExpressionParameters.MAX_PARAMETER_COST -
                            Avatar.expressionParameters.CalcTotalCost();
            List<VRCExpressionParameters.Parameter> parameters
                = new List<VRCExpressionParameters.Parameter>(Avatar.expressionParameters.parameters);

            foreach (var param in LinkSpec.signals)
            {
                if (param.syncDirection != SyncDirection.TwoWay) continue;
                var parameterName = param.TxSignalFlag(Names);

                if (knownParameters.Contains(parameterName)) continue;
                if (remaining <= 0) throw new Exception("Too many synced parameters");

                parameters.Add(new VRCExpressionParameters.Parameter()
                {
                    name = parameterName,
                    defaultValue = 0,
                    saved = false,
                    valueType = VRCExpressionParameters.ValueType.Bool
                });
            }

            Avatar.expressionParameters.parameters = parameters.ToArray();
        }

        protected override AnimatorStateMachine OneWayParamLayer(
            AvrcSignal signal,
            int values,
            EqualsCondition equalsCondition,
            NotEqualsCondition notEqualsCondition,
            DriveParameter driveParameter
        )
        {
            AnimatorStateMachine rootStateMachine = new AnimatorStateMachine();

            AnimatorState[] states = new AnimatorState[values];
            float perState = 1.0f / (states.Length + 1);
            var ybias = -states.Length / 2.0f;
            for (int i = 0; i < states.Length; i++)
            {
                states[i] = rootStateMachine.AddState(signal.name + "_" + i, pos(1, ybias + i));
                states[i].motion = Animations.Named(
                    Names.Prefix + "_" + signal.name + "_" + i,
                    () => Animations.SignalClip(signal, false, i)
                );

                // TODO minimize any state transitions
                var transition = rootStateMachine.AddAnyStateTransition(states[i]);
                transition.duration = 0;
                transition.hasExitTime = false;
                equalsCondition(transition, i);
                transition.canTransitionToSelf = false;
            }

            return rootStateMachine;
        }

        protected override AnimatorStateMachine TwoWayParamLayer(
            AvrcSignal signal,
            int values,
            EqualsCondition equalsCondition,
            NotEqualsCondition notEqualsCondition,
            DriveParameter driveParameter
        )
        {
            var idleMotion = AvrcAssets.EmptyClip();

            AnimatorStateMachine stateMachine = new AnimatorStateMachine();

            stateMachine.entryPosition = pos(1, 0);
            stateMachine.anyStatePosition = pos(1, -1);

            var disconnected = stateMachine.AddState("Disconnected", pos(2, 0));
            disconnected.motion = idleMotion;

            var transition = AddInstantAnyTransition(stateMachine, disconnected);
            transition.AddCondition(AnimatorConditionMode.IfNot, 0, Names.PubParamEitherLocal);

            var rx = stateMachine.AddState("Receive", pos(3, 0));
            rx.motion = idleMotion;
            rx.behaviours = new StateMachineBehaviour[] {ParameterDriver(signal.TxSignalFlag(Names), 0)};
            transition = AddInstantTransition(disconnected, rx);
            transition.AddCondition(AnimatorConditionMode.If, 1, Names.PubParamPeerPresent);
            // This transition controls entry to all passive and active states, so gate it to be
            // IsLocal only.
            AddIsLocalCondition(transition);

            var triggerEntry = stateMachine.AddState("TriggerEntry", pos(2, 1 + values / 2.0f));
            triggerEntry.motion = idleMotion;
            AddParameter(signal.TxSignalFlag(Names), AnimatorControllerParameterType.Bool);
            transition = AddInstantTransition(disconnected, triggerEntry);
            transition.AddCondition(AnimatorConditionMode.If, 1, signal.TxSignalFlag(Names));
            transition = AddInstantTransition(triggerEntry, disconnected);
            transition.AddCondition(AnimatorConditionMode.IfNot, 0, signal.TxSignalFlag(Names));

            var tx = stateMachine.AddState("Transmit", pos(5, 0));
            tx.motion = idleMotion;
            tx.behaviours = new StateMachineBehaviour[] {ParameterDriver(signal.TxSignalFlag(Names), 0)};

            var ybias_owner = 0.5f - values / 2.0f;

            // These states indicate that the transmitter and receiver are in sync.
            // They also force the transmitter state to match the receiver on entry.
            // They are entered only in the local clone of the TX avatar.
            var passiveStates = new AnimatorState[values];
            // These states indicate that the transmitter is pushing a state value to
            // the receiver. They are entered only in the local clone of the TX avatar.
            var activeStates = new AnimatorState[values];
            // These states control the actual transmit trigger. As such, they are
            // entered only in the non-local clones of the TX avatar.
            // TODO: Enter only when the receiver is local.
            var triggerStates = new AnimatorState[values];

            float perState = 1.0f / (passiveStates.Length + 1);

            for (int i = 0; i < passiveStates.Length; i++)
            {
                passiveStates[i] = stateMachine.AddState($"Passive[{i}]", pos(4, ybias_owner + i));
                passiveStates[i].motion = idleMotion;
                activeStates[i] = stateMachine.AddState($"Active[{i}]", pos(6, ybias_owner + i));
                activeStates[i].motion = idleMotion;
                triggerStates[i] = stateMachine.AddState($"Trigger[{i}]", pos(1, 1 + i));
                triggerStates[i].motion = Animations.Named(
                    $"{Names.Prefix}_{signal.name}_{i}",
                    () => Animations.SignalClip(signal, false, i)
                );

                activeStates[i].behaviours = new StateMachineBehaviour[]
                    {ParameterDriver(signal.TxSignalFlag(Names), 1)};

                transition = AddInstantTransition(triggerStates[i], disconnected);
                transition.AddCondition(AnimatorConditionMode.IfNot, 0, signal.TxSignalFlag(Names));
                transition = AddInstantTransition(triggerStates[i], disconnected);
                notEqualsCondition(transition, i);
                transition = AddInstantTransition(triggerEntry, triggerStates[i]);
                transition.AddCondition(AnimatorConditionMode.If, 1, signal.TxSignalFlag(Names));
                equalsCondition(transition, i);
            }

            var ackParam = Names.InternalParameter(signal, "ACK");
            AddParameter(ackParam, AnimatorControllerParameterType.Float);

            for (int i = 0; i < passiveStates.Length; i++)
            {
                // Write TX variable on entering passive state
                var driver = ParameterDriver(signal.TxSignalFlag(Names), 0);
                driveParameter(driver, i);
                passiveStates[i].behaviours = new StateMachineBehaviour[] {driver};

                // Entry into passive state from rx
                transition = AddInstantTransition(rx, passiveStates[i]);
                AddSignalCondition(transition, signal, i, true);
                AddPilotCondition(transition);

                // Exit from passive to rx when ack value changes
                AddAntiSignalConditions(signal, i, true, () =>
                {
                    var t = AddInstantTransition(passiveStates[i], rx);
                    AddPilotCondition(t);
                    return t;
                });

                // Start transmitting when: Local value changes, and remote has not changed
                transition = AddInstantTransition(passiveStates[i], tx);
                AddSignalCondition(transition, signal, i, true);
                AddPilotCondition(transition);
                notEqualsCondition(transition, i);

                // TX to active transition
                transition = AddInstantTransition(tx, activeStates[i]);
                AddPilotCondition(transition);
                equalsCondition(transition, i);

                // Exit from active state when acknowledged
                transition = AddInstantTransition(activeStates[i], passiveStates[i]);
                AddSignalCondition(transition, signal, i, true);
                AddPilotCondition(transition);
            }

            return stateMachine;
        }
    }
}