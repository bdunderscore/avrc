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
// transmission) before the true transmitter has applied the parameter driver states.
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
            AddOrReplaceLayer(Names.LayerSetup, TransmitterSetupLayer());
            AddOrReplaceLayer(Names.LayerTxEnable, TransmitterEnableStateMachine());

            foreach (var param in Parameters.avrcParams)
            {
                CreateParameterLayer(param);
            }

            AvrcAnimatorUtils.GarbageCollectAnimatorAsset(AnimatorController);
            EditorUtility.SetDirty(AnimatorController);
        }

        private AnimatorStateMachine TransmitterSetupLayer()
        {
            return CommonSetupLayer(
                Names.Prefix + "_EnableTX_",
                Names.ParamRxLocal,
                Names.ParamRxPresent,
                Animations.TransmitterPresentClip
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

            foreach (var param in Parameters.avrcParams)
            {
                if (param.syncDirection != AvrcParameters.SyncDirection.TwoWay) continue;
                var parameterName = param.TxParameterFlag(Names);

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

        private AnimatorStateMachine TransmitterEnableStateMachine()
        {
            AddParameter(Names.ParamTxActive, AnimatorControllerParameterType.Int);

            AnimatorStateMachine rootStateMachine = new AnimatorStateMachine();
            var entry = rootStateMachine.AddState("Idle");

            entry.motion = AvrcAssets.EmptyClip();
            entry.behaviours = new StateMachineBehaviour[] {ParameterDriver(Names.ParamTxActive, 0, false)};

            var timeout = rootStateMachine.AddState("Timeout");
            timeout.motion = entry.motion;

            // After 5s without any contact from the transmitter, go inactive (this resets bidirectional variable states)
            var t = rootStateMachine.AddAnyStateTransition(timeout);
            t.exitTime = 0;
            t.hasExitTime = false;
            t.duration = 0;
            t.canTransitionToSelf = false;
            // We use max value here (minus epsilon) because we are uninterested in whether communication is precise,
            // just whether the TX is present here at all
            t.AddCondition(AnimatorConditionMode.Greater, 1.0f - EPSILON, Names.ParamRxPresent);
            t.AddCondition(AnimatorConditionMode.Equals, 1, Names.ParamTxActive);

            t = timeout.AddTransition(entry);
            t.exitTime = 5;
            t.hasExitTime = true;
            t.duration = 0;

            var rxPresentDelay = rootStateMachine.AddState("Delay");
            rxPresentDelay.motion = entry.motion;
            rxPresentDelay.behaviours = new StateMachineBehaviour[] {ParameterDriver(Names.ParamTxActive, 1, false)};

            t = AddInstantTransition(entry, rxPresentDelay);
            AddPresenceCondition(t, Names.ParamRxPresent);

            var rxPresent = rootStateMachine.AddState("RxPresent");
            rxPresent.motion = entry.motion;

            t = AddInstantTransition(rxPresentDelay, rxPresent);
            t.AddCondition(AnimatorConditionMode.Equals, 1, Names.ParamTxActive); // always true

            // Cancel timeout if we see the RX again
            t = AddInstantTransition(timeout, rxPresent);
            t.AddCondition(AnimatorConditionMode.Less, 1.0f - EPSILON, Names.ParamRxPresent);

            return rootStateMachine;
        }

        protected override AnimatorStateMachine IsLocalParamLayer(AvrcParameters.AvrcParameter parameter)
        {
            // No TX work needed for the IsLocal layers
            return null;
        }

        protected override AnimatorStateMachine FloatParamLayer(AvrcParameters.AvrcParameter parameter)
        {
            AnimatorStateMachine rootStateMachine = new AnimatorStateMachine();
            var entry = rootStateMachine.AddState("Entry");

            entry.motion = Animations.Named(
                Names.Prefix + "_" + parameter.name,
                () => Animations.LinearClip(Names.ParameterPath(parameter), Parameters.baseOffset)
            );

            entry.timeParameterActive = true;
            entry.timeParameter = Names.UserParameter(parameter);
            AddParameter(Names.UserParameter(parameter), AnimatorControllerParameterType.Float);

            return rootStateMachine;
        }

        protected override AnimatorStateMachine OneWayParamLayer(
            AvrcParameters.AvrcParameter parameter,
            int values,
            EqualsCondition equalsCondition,
            NotEqualsCondition notEqualsCondition,
            DriveParameter driveParameter
        )
        {
            AnimatorStateMachine rootStateMachine = new AnimatorStateMachine();

            AnimatorState[] states = new AnimatorState[values];
            float perState = 1.0f / (states.Length + 1);
            for (int i = 0; i < states.Length; i++)
            {
                states[i] = rootStateMachine.AddState(parameter.name + "_" + i);
                states[i].motion = Animations.Named(
                    Names.Prefix + "_" + parameter.name + "_" + i,
                    () => Animations.ConstantClip(Names.ParameterPath(parameter), Parameters.baseOffset,
                        perState * (i + 1))
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
            AvrcParameters.AvrcParameter parameter,
            int values,
            EqualsCondition equalsCondition,
            NotEqualsCondition notEqualsCondition,
            DriveParameter driveParameter
        )
        {
            var idleMotion = Animations.Named(
                Names.Prefix + "_" + parameter.name + "_Idle",
                () => Animations.ConstantClip(Names.ParameterPath(parameter), Parameters.baseOffset, -1)
            );

            AnimatorStateMachine stateMachine = new AnimatorStateMachine();
            var disconnected = stateMachine.AddState("Disconnected");
            disconnected.motion = idleMotion;

            var t = AddInstantAnyTransition(stateMachine, disconnected);
            t.AddCondition(AnimatorConditionMode.Equals, 0, Names.ParamTxActive);

            var rx = stateMachine.AddState("Receive");
            rx.motion = idleMotion;
            rx.behaviours = new StateMachineBehaviour[] {ParameterDriver(parameter.TxParameterFlag(Names), 0)};
            t = AddInstantTransition(disconnected, rx);
            t.AddCondition(AnimatorConditionMode.Equals, 1, Names.ParamTxActive);
            // This transition controls entry to all passive and active states, so gate it to be
            // IsLocal only.
            AddIsLocalCondition(t);

            var triggerEntry = stateMachine.AddState("TriggerEntry");
            triggerEntry.motion = idleMotion;
            AddParameter(parameter.TxParameterFlag(Names), AnimatorControllerParameterType.Bool);
            t = AddInstantTransition(disconnected, triggerEntry);
            t.AddCondition(AnimatorConditionMode.If, 1, parameter.TxParameterFlag(Names));
            t = AddInstantTransition(triggerEntry, disconnected);
            t.AddCondition(AnimatorConditionMode.IfNot, 0, parameter.TxParameterFlag(Names));

            var tx = stateMachine.AddState("Transmit");
            tx.motion = idleMotion;
            tx.behaviours = new StateMachineBehaviour[] {ParameterDriver(parameter.TxParameterFlag(Names), 0)};

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
                passiveStates[i] = stateMachine.AddState($"Passive[{i}]");
                passiveStates[i].motion = idleMotion;
                activeStates[i] = stateMachine.AddState($"Active[{i}]");
                activeStates[i].motion = idleMotion;
                triggerStates[i] = stateMachine.AddState($"Trigger[{i}]");
                triggerStates[i].motion = Animations.Named(
                    $"{Names.Prefix}_{parameter.name}_{i}",
                    () => Animations.ConstantClip(Names.ParameterPath(parameter), Parameters.baseOffset,
                        perState * (i + 1))
                );

                activeStates[i].behaviours = new StateMachineBehaviour[]
                    {ParameterDriver(parameter.TxParameterFlag(Names), 1)};

                t = AddInstantTransition(triggerStates[i], disconnected);
                t.AddCondition(AnimatorConditionMode.IfNot, 0, parameter.TxParameterFlag(Names));
                t = AddInstantTransition(triggerStates[i], disconnected);
                notEqualsCondition(t, i);
                t = AddInstantTransition(triggerEntry, triggerStates[i]);
                t.AddCondition(AnimatorConditionMode.If, 1, parameter.TxParameterFlag(Names));
                equalsCondition(t, i);
            }

            var ackParam = Names.InternalParameter(parameter, "ACK");
            AddParameter(ackParam, AnimatorControllerParameterType.Float);

            for (int i = 0; i < passiveStates.Length; i++)
            {
                float rxRangeLow = perState * (i - 0.5f);
                float rxRangeHi = perState * (i + 0.5f);

                // Write TX variable on entering passive state
                var driver = ParameterDriver(parameter.TxParameterFlag(Names), 0);
                driveParameter(driver, i);
                passiveStates[i].behaviours = new StateMachineBehaviour[] {driver};

                // Entry into passive state from rx
                var transition = AddInstantTransition(rx, passiveStates[i]);
                transition.AddCondition(AnimatorConditionMode.Greater, rxRangeLow, ackParam);
                transition.AddCondition(AnimatorConditionMode.Less, rxRangeHi, ackParam);
                AddPresenceCondition(transition, Names.ParamRxPresent);

                // Exit from passive to rx when ack value changes
                transition = AddInstantTransition(passiveStates[i], rx);
                equalsCondition(transition, i);
                transition.AddCondition(AnimatorConditionMode.Greater, rxRangeHi, ackParam);
                AddPresenceCondition(transition, Names.ParamRxPresent);

                transition = AddInstantTransition(passiveStates[i], rx);
                equalsCondition(transition, i);
                transition.AddCondition(AnimatorConditionMode.Less, rxRangeLow, ackParam);
                AddPresenceCondition(transition, Names.ParamRxPresent);

                // Start transmitting when: Local value changes, and remote has not changed
                transition = AddInstantTransition(passiveStates[i], tx);
                transition.AddCondition(AnimatorConditionMode.Greater, rxRangeLow, ackParam);
                transition.AddCondition(AnimatorConditionMode.Less, rxRangeHi, ackParam);
                AddPresenceCondition(transition, Names.ParamRxPresent);
                notEqualsCondition(transition, i);

                // TX to active transition
                transition = AddInstantTransition(tx, activeStates[i]);
                AddPresenceCondition(transition, Names.ParamRxPresent);
                equalsCondition(transition, i);

                // Exit from active state when acknowledged
                transition = AddInstantTransition(activeStates[i], passiveStates[i]);
                transition.AddCondition(AnimatorConditionMode.Greater, rxRangeLow, ackParam);
                transition.AddCondition(AnimatorConditionMode.Less, rxRangeHi, ackParam);
                AddPresenceCondition(transition, Names.ParamRxPresent);
            }

            return stateMachine;
        }
    }
}