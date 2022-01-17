using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Codice.Client.Commands.Tree;
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
        private readonly VRCAvatarDescriptor m_avatarDescriptor;
        
        private AvrcTxStateMachines(VRCAvatarDescriptor avatarDescriptor, AvrcParameters parameters)
            : base(avatarDescriptor, parameters)
        {
            m_avatarDescriptor = avatarDescriptor;
        }

        public static void SetupTx(VRCAvatarDescriptor avatarDescriptor, AvrcParameters parameters)
        {
            new AvrcTxStateMachines(avatarDescriptor, parameters).Setup();

        }
        
        private void Setup()
        {
            AddBidirectionalTransferParameters();
            AddOrReplaceLayer(Names.LayerTxEnable, TransmitterEnableStateMachine());

            foreach (var param in m_parameters.avrcParams)
            {
                CreateParameterLayer(param);
            }
            
            GarbageCollectAnimatorAsset();
            EditorUtility.SetDirty(m_animatorController);
        }

        private void AddBidirectionalTransferParameters()
        {
            HashSet<string> knownParameters = new HashSet<string>();
            if (m_avatarDescriptor.expressionParameters == null)
            {
                throw new Exception("No expression parameters found");
            }
            
            foreach (var param in m_avatarDescriptor.expressionParameters.parameters)
            {
                knownParameters.Add(param.name);
            }

            int remaining = VRCExpressionParameters.MAX_PARAMETER_COST -
                            m_avatarDescriptor.expressionParameters.CalcTotalCost();
            List<VRCExpressionParameters.Parameter> parameters
                = new List<VRCExpressionParameters.Parameter>(m_avatarDescriptor.expressionParameters.parameters);

            foreach (var param in m_parameters.avrcParams)
            {
                if (param.type != AvrcParameters.AvrcParameterType.BidiInt) continue;
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

            m_avatarDescriptor.expressionParameters.parameters = parameters.ToArray();
        }

        private AnimatorStateMachine TransmitterEnableStateMachine()
        {
            AddParameter(Names.ParamTxActive, AnimatorControllerParameterType.Int);
            
            AnimatorStateMachine rootStateMachine = new AnimatorStateMachine();
            var entry = rootStateMachine.AddState("ConstraintEnabled");
            
            entry.motion = AvrcAnimations.Named(
                m_parameters.Names.Prefix + "_EnableTX",
                () => AvrcAnimations.ReceiverPresentClip(m_parameters)
            );
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
            AnimatorStateMachine rootStateMachine = new AnimatorStateMachine();
            var entry = rootStateMachine.AddState("Entry");
            var isLocal = rootStateMachine.AddState("IsLocal");
            
            var transition = entry.AddTransition(isLocal, false);
            transition.duration = 0;
            transition.hasExitTime = false;
            transition.AddCondition(AnimatorConditionMode.If, 1, "IsLocal");
            
            entry.motion = AvrcAssets.EmptyClip();
            isLocal.motion = AvrcAnimations.Named(
                m_parameters.Names.Prefix + "_" + parameter.name,
                () => AvrcAnimations.ConstantClip(m_parameters.Names.ParameterPath(parameter), m_parameters.baseOffset, 1)
            );

            return rootStateMachine;
        }

        protected override AnimatorStateMachine BoolParamLayer(AvrcParameters.AvrcParameter parameter)
        {
            AnimatorStateMachine rootStateMachine = new AnimatorStateMachine();
            var entry = rootStateMachine.AddState("Entry");
            var st_true = rootStateMachine.AddState("True");
            var st_false = rootStateMachine.AddState("False");

            entry.motion = AvrcAssets.EmptyClip();
            st_true.motion = AvrcAnimations.Named(
                m_parameters.Names.Prefix + "_" + parameter.name + "_True",
                () => AvrcAnimations.ConstantClip(m_parameters.Names.ParameterPath(parameter), m_parameters.baseOffset, 1)
            );
            st_false.motion = AvrcAnimations.Named(
                m_parameters.Names.Prefix + "_" + parameter.name + "_False",
                () => AvrcAnimations.ConstantClip(m_parameters.Names.ParameterPath(parameter), m_parameters.baseOffset, -1)
            );

            var transition = rootStateMachine.AddAnyStateTransition(st_true);
            transition.duration = 0;
            transition.hasExitTime = false;
            transition.canTransitionToSelf = false;
            AddParameter(parameter.name, AnimatorControllerParameterType.Bool);
            transition.AddCondition(AnimatorConditionMode.If, 1, parameter.name);
            
            transition = rootStateMachine.AddAnyStateTransition(st_false);
            transition.duration = 0;
            transition.hasExitTime = false;
            transition.canTransitionToSelf = false;
            transition.AddCondition(AnimatorConditionMode.IfNot, 0, parameter.name);

            return rootStateMachine;
        }

        protected override AnimatorStateMachine FloatParamLayer(AvrcParameters.AvrcParameter parameter)
        {
            AnimatorStateMachine rootStateMachine = new AnimatorStateMachine();
            var entry = rootStateMachine.AddState("Entry");

            entry.motion = AvrcAnimations.Named(
                m_parameters.Names.Prefix + "_" + parameter.name,
                () => AvrcAnimations.LinearClip(m_parameters.Names.ParameterPath(parameter), m_parameters.baseOffset)
            );

            entry.timeParameterActive = true;
            entry.timeParameter = parameter.name;
            AddParameter(parameter.name, AnimatorControllerParameterType.Float);
            
            return rootStateMachine;
        }

        protected override AnimatorStateMachine IntParamLayer(AvrcParameters.AvrcParameter parameter)
        {
            AnimatorStateMachine rootStateMachine = new AnimatorStateMachine();

            AnimatorState[] states = new AnimatorState[parameter.maxVal - parameter.minVal + 1];
            float perState = 1.0f / (states.Length + 1);
            for (int i = 0; i < states.Length; i++)
            {
                states[i] = rootStateMachine.AddState(parameter.name + "_" + i);
                states[i].motion = AvrcAnimations.Named(
                    m_parameters.Names.Prefix + "_" + parameter.name + "_" + i,
                    () => AvrcAnimations.ConstantClip(m_parameters.Names.ParameterPath(parameter), m_parameters.baseOffset,
                        perState * (i + 1))
                );

                var transition = rootStateMachine.AddAnyStateTransition(states[i]);
                transition.duration = 0;
                transition.hasExitTime = false;
                transition.AddCondition(AnimatorConditionMode.Equals, i + parameter.minVal, parameter.name);
                transition.canTransitionToSelf = false;
            }
            
            AddParameter(parameter.name, AnimatorControllerParameterType.Int);

            return rootStateMachine;
        }

        protected override AnimatorStateMachine BidiIntParamLayer(AvrcParameters.AvrcParameter parameter)
        {
            var idleMotion = AvrcAnimations.Named(
                m_parameters.Names.Prefix + "_" + parameter.name + "_Idle",
                () => AvrcAnimations.ConstantClip(m_parameters.Names.ParameterPath(parameter), m_parameters.baseOffset, -1)
            );
            
            AnimatorStateMachine stateMachine = new AnimatorStateMachine();
            var disconnected = stateMachine.AddState("Disconnected");
            disconnected.motion = idleMotion;

            var t = AddInstantAnyTransition(stateMachine, disconnected);
            t.AddCondition(AnimatorConditionMode.Equals, 0, Names.ParamTxActive);

            var rx = stateMachine.AddState("Receive");
            rx.motion = idleMotion;
            rx.behaviours = new StateMachineBehaviour[]{ParameterDriver(parameter.TxParameterFlag(Names), 0)};
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
            tx.behaviours = new StateMachineBehaviour[]{ParameterDriver(parameter.TxParameterFlag(Names), 0)};
            
            int lo = parameter.minVal;
            int hi = parameter.maxVal;

            // These states indicate that the transmitter and receiver are in sync.
            // They also force the transmitter state to match the receiver on entry.
            // They are entered only in the local clone of the TX avatar.
            var passiveStates = new AnimatorState[hi - lo + 1];
            // These states indicate that the transmitter is pushing a state value to
            // the receiver. They are entered only in the local clone of the TX avatar.
            var activeStates = new AnimatorState[hi - lo + 1];
            // These states control the actual transmit trigger. As such, they are
            // entered only in the non-local clones of the TX avatar.
            // TODO: Enter only when the receiver is local.
            var triggerStates = new AnimatorState[hi - lo + 1];
            
            float perState = 1.0f / (passiveStates.Length + 1);

            for (int i = 0; i < passiveStates.Length; i++)
            {
                passiveStates[i] = stateMachine.AddState($"Passive[{lo + i}]");
                passiveStates[i].motion = idleMotion;
                activeStates[i] = stateMachine.AddState($"Active[{lo + i}]");
                activeStates[i].motion = idleMotion;
                triggerStates[i] = stateMachine.AddState($"Trigger[{lo + i}]");
                triggerStates[i].motion = AvrcAnimations.Named(
                    $"{Names.Prefix}_{parameter.name}_{i}",
                    () => AvrcAnimations.ConstantClip(m_parameters.Names.ParameterPath(parameter), m_parameters.baseOffset, perState * (i + 1))
                );

                activeStates[i].behaviours = new StateMachineBehaviour[]
                    {ParameterDriver(parameter.TxParameterFlag(Names), 1)};

                t = AddInstantTransition(triggerStates[i], disconnected);
                t.AddCondition(AnimatorConditionMode.IfNot, 0, parameter.TxParameterFlag(Names));
                t = AddInstantTransition(triggerStates[i], disconnected);
                t.AddCondition(AnimatorConditionMode.NotEqual, i + lo, parameter.name);
                t = AddInstantTransition(triggerEntry, triggerStates[i]);
                t.AddCondition(AnimatorConditionMode.If, 1, parameter.TxParameterFlag(Names));
                t.AddCondition(AnimatorConditionMode.Equals, i + lo, parameter.name);
            }
            
            AddParameter(parameter.name, AnimatorControllerParameterType.Int);
            var ackParam = parameter.name + "_ACK";
            AddParameter(ackParam, AnimatorControllerParameterType.Float);

            for (int i = 0; i < passiveStates.Length; i++)
            {
                float rxRangeLow = perState * (i - 0.5f);
                float rxRangeHi = perState * (i + 0.5f);
                
                // Write TX variable on entering passive state
                passiveStates[i].behaviours = new StateMachineBehaviour[]
                {
                    ParameterDriver(parameter.name, i + lo),
                    ParameterDriver(parameter.TxParameterFlag(Names), 0),
                };

                // Entry into passive state from rx
                var transition = AddInstantTransition(rx, passiveStates[i]);
                transition.AddCondition(AnimatorConditionMode.Greater, rxRangeLow, ackParam);
                transition.AddCondition(AnimatorConditionMode.Less, rxRangeHi, ackParam);
                AddPresenceCondition(transition, Names.ParamRxPresent);
                
                // Exit from passive to rx when ack value changes
                transition = AddInstantTransition(passiveStates[i], rx);
                transition.AddCondition(AnimatorConditionMode.Equals, i + lo, parameter.name);
                transition.AddCondition(AnimatorConditionMode.Greater, rxRangeHi, ackParam);
                AddPresenceCondition(transition, Names.ParamRxPresent);
                
                transition = AddInstantTransition(passiveStates[i], rx);
                transition.AddCondition(AnimatorConditionMode.Equals, i + lo, parameter.name);
                transition.AddCondition(AnimatorConditionMode.Less, rxRangeLow, ackParam);
                AddPresenceCondition(transition, Names.ParamRxPresent);
                
                // Start transmitting when: Local value changes, and remote has not changed
                transition = AddInstantTransition(passiveStates[i], tx);
                transition.AddCondition(AnimatorConditionMode.Greater, rxRangeLow, ackParam);
                transition.AddCondition(AnimatorConditionMode.Less, rxRangeHi, ackParam);
                AddPresenceCondition(transition, Names.ParamRxPresent);
                transition.AddCondition(AnimatorConditionMode.NotEqual, i + lo, parameter.name);
                
                // TX to active transition
                transition = AddInstantTransition(tx, activeStates[i]);
                AddPresenceCondition(transition, Names.ParamRxPresent);
                transition.AddCondition(AnimatorConditionMode.Equals, i + lo, parameter.name);

                // Exit from active state when acknowledged
                transition = AddInstantTransition(activeStates[i], passiveStates[i]);
                transition.AddCondition(AnimatorConditionMode.Greater, rxRangeLow, ackParam);
                transition.AddCondition(AnimatorConditionMode.Less, rxRangeHi, ackParam);
                AddPresenceCondition(transition, Names.ParamRxPresent);
            }

            return stateMachine;
        }

        private static AnimatorStateTransition AddInstantAnyTransition(AnimatorStateMachine sourceState, AnimatorState destinationState)
        {
            AnimatorStateTransition transition = sourceState.AddAnyStateTransition(destinationState);
            transition.exitTime = 0;
            transition.hasExitTime = false;
            transition.duration = 0;
            transition.canTransitionToSelf = false;
            return transition;
        }

        private static AnimatorStateTransition AddInstantTransition(AnimatorState startState, AnimatorState state)
        {
            var transition = startState.AddTransition(state);
            transition.exitTime = 0;
            transition.hasExitTime = false;
            transition.duration = 0;
            return transition;
        }

        private void AddIsLocalCondition(AnimatorStateTransition transition)
        {
            AddParameter("IsLocal", AnimatorControllerParameterType.Bool);
            transition.AddCondition(AnimatorConditionMode.If, 1, "IsLocal");
        }
    }
}