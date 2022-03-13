using System.Diagnostics.CodeAnalysis;
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
        
        private void Setup() {
            // Enable the constraint that places our receiver triggers at the correct location
            AddOrReplaceLayer(Names.LayerSetup, ReceiverSetupLayer());
            // Set up a mesh to expand our bounding box locally for the transmitter
            // AddOrReplaceLayer("_AVRC_" + Names.Prefix + "_RXBounds", BoundsSetupStateMachine());
            
            foreach (var param in Parameters.avrcParams)
            {
                CreateParameterLayer(param);
            }

            AvrcAnimatorUtils.GarbageCollectAnimatorAsset(AnimatorController);
            EditorUtility.SetDirty(AnimatorController);
        }

        private AnimatorStateMachine ReceiverSetupLayer()
        {
            return CommonSetupLayer(
                Names.Prefix + "_EnableRX_",
                Names.ParamTxLocal,
                Names.ParamTxProximity,
                Animations.ReceiverPresentClip
                );
        }

        protected override AnimatorStateMachine IsLocalParamLayer(AvrcParameters.AvrcParameter parameter)
        {
            var stateMachine = new AnimatorStateMachine();

            var init = stateMachine.AddState("Init");
            AddParameter(Names.UserParameter(parameter), AnimatorControllerParameterType.Bool);
            init.behaviours = new StateMachineBehaviour[]
            {
                ParameterDriver(Names.UserParameter(parameter), 0, false)
            };
            init.motion = AvrcAssets.EmptyClip();

            var local = stateMachine.AddState("IsLocal");
            local.motion = init.motion;
            local.behaviours = new StateMachineBehaviour[]
            {
                ParameterDriver(Names.UserParameter(parameter), 1, false)
            };

            var timeout = stateMachine.AddState("Timeout");
            timeout.motion = init.motion;

            var t = AddInstantTransition(init, local);
            AddParameter(Names.ParamTxLocal, AnimatorControllerParameterType.Float);
            AddParameter("IsLocal", AnimatorControllerParameterType.Bool);
            t.AddCondition(AnimatorConditionMode.Greater, 0.5f, Names.ParamTxLocal);
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsLocal");

            t = AddInstantTransition(local, timeout);
            t.hasExitTime = true;
            t.exitTime = 0.5f;

            t = AddInstantTransition(timeout, local);
            t.AddCondition(AnimatorConditionMode.Greater, 0.5f, Names.ParamTxLocal);

            t = AddInstantTransition(timeout, init);
            t.hasExitTime = true;
            t.exitTime = Timeout - 0.5f;

            return stateMachine;
        }

        protected override AnimatorStateMachine TwoWayParamLayer(AvrcParameters.AvrcParameter parameter, int values, EqualsCondition equalsCondition,
            NotEqualsCondition notEqualsCondition, DriveParameter driveParameter)
        {
            return FiniteParamLayer(true, parameter, values, equalsCondition, notEqualsCondition, driveParameter);
        }

        protected override AnimatorStateMachine OneWayParamLayer(
            AvrcParameters.AvrcParameter parameter,
            int values,
            EqualsCondition equalsCondition,
            NotEqualsCondition notEqualsCondition,
            DriveParameter driveParameter
        )
        {
            return FiniteParamLayer(false, parameter, values, equalsCondition, notEqualsCondition, driveParameter);
        }
        AnimatorStateMachine FiniteParamLayer(
            bool twoWay,
            AvrcParameters.AvrcParameter parameter,
            int values,
            EqualsCondition equalsCondition,
            NotEqualsCondition notEqualsCondition, 
            DriveParameter driveParameter
        ) {
            var stateMachine = new AnimatorStateMachine();

            var states = new AnimatorState[values];
            var perState = 1.0f / (states.Length + 1);

            var conditionParamName = Names.InternalParameter(parameter);
            AddParameter(conditionParamName, AnimatorControllerParameterType.Float);
            AddParameter(Names.ParamTxProximity, AnimatorControllerParameterType.Float);

            var remoteDriven = new AnimatorState[states.Length];

            for (var i = 0; i < states.Length; i++)
            {
                var hi = (i + 1.5f) * perState;
                var lo = (i + 0.5f) * perState;
                
                states[i] = stateMachine.AddState($"Passive_{parameter.name}_{i}");
                remoteDriven[i] = stateMachine.AddState($"RemoteDriven_{parameter.name}_{i}");

                var driver = ParameterDriver();
                remoteDriven[i].behaviours = new StateMachineBehaviour[] { driver };
                driveParameter(driver, i);
                
                states[i].motion = AvrcAssets.EmptyClip();
                remoteDriven[i].motion = AvrcAssets.EmptyClip();

                if (twoWay)
                {
                    states[i].motion = Animations.Named(
                        $"{Names.Prefix}_{parameter.name}_{i}_ACK",
                        () => Animations.ConstantClip(Names.ParameterPath(parameter) + "_ACK", Parameters.baseOffset, perState * i)
                    );
                    remoteDriven[i].motion = states[i].motion;
                }
                
                // When driven locally (only) skip the parameter driver
                // TODO: when one-way drive it back to the correct state
                // TODO: avoid large any state transitions
                var transition = stateMachine.AddAnyStateTransition(states[i]);
                transition.duration = 0;
                transition.hasExitTime = false;
                equalsCondition(transition, i);
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

        protected override AnimatorStateMachine FloatParamLayer(AvrcParameters.AvrcParameter parameter)
        {
            // Float parameters are simply received directly; no special logic is required
            return null;
        }

    }
}