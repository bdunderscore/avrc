/*
 * MIT License
 * 
 * Copyright (c) 2021-2022 bd_
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE. 
 */

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
            AddOrReplaceLayer(Names.LayerProbe, ProbeLayer());

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
                    () => SignalEncoding.SignalDrivers[signal.SignalName][i].Clip(Names)
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
            AddParameter(Names.PubParamForceTransmit, AnimatorControllerParameterType.Bool, false);

            AnimatorStateMachine stateMachine = new AnimatorStateMachine();

            stateMachine.entryPosition = pos(1, 0);
            stateMachine.anyStatePosition = pos(1, -1);

            var disconnected = stateMachine.AddState("Disconnected", pos(2, 0));
            disconnected.motion = idleMotion;
            disconnected.behaviours = new StateMachineBehaviour[]
            {
                ParameterDriver(true, (signal.TxSignalFlag(Names), 0))
            };

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
            transition.AddCondition(AnimatorConditionMode.IfNot, 0, IS_LOCAL);
            transition = AddInstantTransition(triggerEntry, disconnected);
            transition.AddCondition(AnimatorConditionMode.IfNot, 0, signal.TxSignalFlag(Names));

            var tx = stateMachine.AddState("Transmit", pos(5, 0));
            tx.motion = idleMotion;
            tx.behaviours = new StateMachineBehaviour[] {ParameterDriver(signal.TxSignalFlag(Names), 0)};

            transition = tx.AddExitTransition();
            transition.duration = 0;
            transition.hasExitTime = false;
            transition.AddCondition(AnimatorConditionMode.IfNot, 0, Names.PubParamPeerPresent);

            stateMachine.exitPosition = pos(7, 0);

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
                    () => SignalEncoding.SignalDrivers[signal.SignalName][i].Clip(Names)
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
                SignalEncoding.SignalDrivers[signal.AckSignalName][i].AddCondition(transition);
                transition.AddCondition(AnimatorConditionMode.If, 0, Names.PubParamPeerPresent);

                // Exit from passive to rx when ack value changes
                transition = AddInstantTransition(passiveStates[i], rx);
                SignalEncoding.SignalDrivers[signal.AckSignalName][i].AddOtherValueCondition(transition);
                transition.AddCondition(AnimatorConditionMode.If, 0, Names.PubParamPeerPresent);

                // Start transmitting when: Local value changes
                transition = AddInstantTransition(passiveStates[i], tx);
                transition.AddCondition(AnimatorConditionMode.If, 0, Names.PubParamPeerPresent);
                notEqualsCondition(transition, i);

                // or when force TX is enabled
                transition = AddInstantTransition(passiveStates[i], tx);
                transition.AddCondition(AnimatorConditionMode.If, 0, Names.PubParamPeerPresent);
                transition.AddCondition(AnimatorConditionMode.If, 0, Names.PubParamForceTransmit);

                // TX to active transition
                transition = AddInstantTransition(tx, activeStates[i]);
                transition.AddCondition(AnimatorConditionMode.If, 0, Names.PubParamPeerPresent);
                equalsCondition(transition, i);

                // Exit from active state when acknowledged, provided we're not in force TX mode
                transition = AddInstantTransition(activeStates[i], passiveStates[i]);
                SignalEncoding.SignalDrivers[signal.AckSignalName][i].AddCondition(transition);
                transition.AddCondition(AnimatorConditionMode.If, 0, Names.PubParamPeerPresent);
                transition.AddCondition(AnimatorConditionMode.IfNot, 0, Names.PubParamForceTransmit);

                // Return back to decision state if local state changed a second time
                transition = AddInstantTransition(activeStates[i], tx);
                notEqualsCondition(transition, i);

                // Exit when disconnected
                transition = activeStates[i].AddExitTransition();
                transition.duration = 0;
                transition.hasExitTime = false;
                transition.AddCondition(AnimatorConditionMode.IfNot, 0, Names.PubParamPeerPresent);
            }

            return stateMachine;
        }
    }
}