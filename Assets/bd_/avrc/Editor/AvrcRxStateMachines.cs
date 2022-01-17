using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace net.fushizen.avrc
{
    [SuppressMessage("ReSharper", "HeapView.ObjectAllocation.Evident")]
    [SuppressMessage("ReSharper", "HeapView.BoxingAllocation")]
    internal class AvrcRxStateMachines : AvrcLayerSetupCommon
    {
        private AvrcRxStateMachines(VRCAvatarDescriptor avatarDescriptor, AvrcParameters parameters)
            : base(avatarDescriptor, parameters)
        {
        }

        public static void SetupRx(VRCAvatarDescriptor avatarDescriptor, AvrcParameters parameters)
        {
            new AvrcRxStateMachines(avatarDescriptor, parameters).Setup();
        }
        
        private void Setup() {
            // Enable the constraint that places our receiver triggers at the correct location
            AddOrReplaceLayer(Names.LayerSetup, ReceiverSetupLayer());
            // Set up a mesh to expand our bounding box locally for the transmitter
            // AddOrReplaceLayer("_AVRC_" + Names.Prefix + "_RXBounds", BoundsSetupStateMachine());
            
            foreach (var param in m_parameters.avrcParams)
            {
                CreateParameterLayer(param);
            }

            GarbageCollectAnimatorAsset();
            EditorUtility.SetDirty(m_animatorController);
        }

        private AnimatorStateMachine ReceiverSetupLayer()
        {
            return CommonSetupLayer(Names.Prefix + "_EnableRX_", Names.ParamTxLocal, AvrcAnimations.ReceiverPresentClip);
        }

        protected override AnimatorStateMachine IsLocalParamLayer(AvrcParameters.AvrcParameter parameter)
        {
            return BoolParamLayer(parameter, false);
        }

        protected override AnimatorStateMachine BoolParamLayer(AvrcParameters.AvrcParameter parameter)
        {
            return BoolParamLayer(parameter, true);
        }

        private AnimatorStateMachine BoolParamLayer(AvrcParameters.AvrcParameter parameter, bool localOnly)
        {
            var stateMachine = new AnimatorStateMachine();

            var conditionParamName = $"{parameter.name}_F";
            AddParameter(Names.ParamTxProximity, AnimatorControllerParameterType.Float);
            AddParameter(conditionParamName, AnimatorControllerParameterType.Float);
            AddParameter(parameter.RxParameterName, AnimatorControllerParameterType.Bool);

            var s_false = stateMachine.AddState("False");
            s_false.behaviours = new StateMachineBehaviour[] {ParameterDriver(parameter.RxParameterName, 0, localOnly: localOnly)};
            s_false.motion = AvrcAssets.EmptyClip();

            var s_true = stateMachine.AddState("True");
            s_true.behaviours = new StateMachineBehaviour[] {ParameterDriver(parameter.RxParameterName, 1, localOnly: localOnly)};
            s_true.motion = AvrcAssets.EmptyClip();

            var t = stateMachine.AddAnyStateTransition(s_true);
            t.duration = 0;
            t.hasExitTime = false;
            t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.Greater, 0.5f, conditionParamName);
            AddPresenceCondition(t, Names.ParamTxProximity);

            t = stateMachine.AddAnyStateTransition(s_false);
            t.duration = 0;
            t.hasExitTime = false;
            t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.Less, 0.5f, conditionParamName);
            AddPresenceCondition(t, Names.ParamTxProximity);

            return stateMachine;
        }

        protected override AnimatorStateMachine IntParamLayer(AvrcParameters.AvrcParameter parameter)
        {
            var stateMachine = new AnimatorStateMachine();

            var states = new AnimatorState[parameter.maxVal - parameter.minVal + 1];
            var perState = 1.0f / (states.Length + 1);

            var conditionParamName = $"{parameter.name}_F";
            AddParameter(conditionParamName, AnimatorControllerParameterType.Float);
            AddParameter(parameter.RxParameterName, AnimatorControllerParameterType.Int);
            AddParameter(Names.ParamTxProximity, AnimatorControllerParameterType.Float);

            var remoteDriven = new AnimatorState[states.Length];

            for (var i = 0; i < states.Length; i++)
            {
                var hi = (i + 1.5f) * perState;
                var lo = (i + 0.5f) * perState;
                
                states[i] = stateMachine.AddState($"Passive_{parameter.name}_{i}");
                remoteDriven[i] = stateMachine.AddState($"RemoteDriven_{parameter.name}_{i}");

                remoteDriven[i].behaviours = new StateMachineBehaviour[] { ParameterDriver(parameter.RxParameterName, i) };
                states[i].motion = AvrcAssets.EmptyClip();
                remoteDriven[i].motion = AvrcAssets.EmptyClip();

                if (parameter.type == AvrcParameters.AvrcParameterType.BidiInt)
                {
                    states[i].motion = AvrcAnimations.Named(
                        $"{Names.Prefix}_{parameter.name}_{i}_ACK",
                        () => AvrcAnimations.ConstantClip(m_parameters.Names.ParameterPath(parameter) + "_ACK", m_parameters.baseOffset, perState * i)
                    );
                    remoteDriven[i].motion = states[i].motion;
                }
                
                // When driven locally (only) skip the parameter driver
                var transition = stateMachine.AddAnyStateTransition(states[i]);
                transition.duration = 0;
                transition.hasExitTime = false;
                transition.AddCondition(AnimatorConditionMode.Equals, i + parameter.minVal, parameter.RxParameterName);
                transition.AddCondition(AnimatorConditionMode.Less, perState / 2, conditionParamName);
                transition.canTransitionToSelf = false;

                // TODO - separate transitions for local and nonlocal
                transition = stateMachine.AddAnyStateTransition(remoteDriven[i]);
                transition.duration = 0;
                transition.hasExitTime = false;
                transition.AddCondition(AnimatorConditionMode.Greater, lo, conditionParamName);
                transition.AddCondition(AnimatorConditionMode.Less, hi, conditionParamName);
                AddPresenceCondition(transition, Names.ParamTxProximity);
                transition.canTransitionToSelf = false;
            }

            return stateMachine;
        }

        protected override AnimatorStateMachine BidiIntParamLayer(AvrcParameters.AvrcParameter parameter)
        {
            return IntParamLayer(parameter);
        }

        protected override AnimatorStateMachine FloatParamLayer(AvrcParameters.AvrcParameter parameter)
        {
            // Float parameters are simply received directly; no special logic is required
            return null;
        }

    }
}