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
    internal class AvrcTxStateMachines : AvrcLayerSetupCommon
    {
        private AvrcTxStateMachines(VRCAvatarDescriptor avatarDescriptor, AvrcParameters parameters)
            : base(avatarDescriptor, parameters)
        {
            
        }

        public static void SetupTx(VRCAvatarDescriptor avatarDescriptor, AvrcParameters parameters)
        {
            new AvrcTxStateMachines(avatarDescriptor, parameters).Setup();

        }
        
        private void Setup() {
            AddOrReplaceLayer(Names.LayerTxEnable, TransmitterEnableStateMachine());

            foreach (var param in m_parameters.avrcParams)
            {
                CreateParameterLayer(param);
            }
            
            GarbageCollectAnimatorAsset();
            EditorUtility.SetDirty(m_animatorController);
        }

        private AnimatorStateMachine TransmitterEnableStateMachine()
        {
            AnimatorStateMachine rootStateMachine = new AnimatorStateMachine();
            var entry = rootStateMachine.AddState("ConstraintEnabled");
            
            entry.motion = AvrcAnimations.Named(
                m_parameters.Names.Prefix + "_EnableTX",
                () => AvrcAnimations.ReceiverPresentClip(m_parameters)
            );

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
            float perState = 1.0f / states.Length;
            for (int i = 0; i < states.Length; i++)
            {
                states[i] = rootStateMachine.AddState(parameter.name + "_" + i);
                states[i].motion = AvrcAnimations.Named(
                    m_parameters.Names.Prefix + "_" + parameter.name + "_" + i,
                    () => AvrcAnimations.ConstantClip(m_parameters.Names.ParameterPath(parameter), m_parameters.baseOffset,
                        perState * i)
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
    }
}