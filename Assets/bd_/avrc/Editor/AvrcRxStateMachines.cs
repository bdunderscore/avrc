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
        private const float PROXIMITY_EPSILON = 1 - 0.01f;

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
            AddOrReplaceLayer(Names.LayerRxConstraint, ConstraintSetupStateMachine());
            // Set up a mesh to expand our bounding box locally for the transmitter
            // AddOrReplaceLayer("_AVRC_" + Names.Prefix + "_RXBounds", BoundsSetupStateMachine());
            
            foreach (var param in m_parameters.avrcParams)
            {
                CreateParameterLayer(param);
            }

            GarbageCollectAnimatorAsset();
            EditorUtility.SetDirty(m_animatorController);
        }

        private AnimatorStateMachine ConstraintSetupStateMachine()
        {
            AnimatorStateMachine stateMachine = new AnimatorStateMachine();
            var entry = stateMachine.AddState("Entry");

            entry.motion = AvrcAnimations.Named(
                $"{Names.Prefix}_EnableRX",
                () => AvrcAnimations.EnableConstraintClip(Names.ObjectPath)
            );

            return stateMachine;
        }

        private VRCAvatarParameterDriver ParameterDriver(string paramName, int value, bool localOnly = true)
        {
            var driver = ScriptableObject.CreateInstance<VRCAvatarParameterDriver>();
            driver.name = $"Driver_{paramName}_{value}";
            driver.localOnly = localOnly;
            driver.parameters = new List<VRC_AvatarParameterDriver.Parameter>()
            {
                new VRC_AvatarParameterDriver.Parameter()
                {
                    chance = 1,
                    name = paramName,
                    value = value,
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                }
            };
            
            AssetDatabase.AddObjectToAsset(driver, m_animatorController);

            return driver;
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
            t.AddCondition(AnimatorConditionMode.Greater, PROXIMITY_EPSILON, Names.ParamTxProximity);

            t = stateMachine.AddAnyStateTransition(s_false);
            t.duration = 0;
            t.hasExitTime = false;
            t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.Less, 0.5f, conditionParamName);
            t.AddCondition(AnimatorConditionMode.Greater, PROXIMITY_EPSILON, Names.ParamTxProximity);

            return stateMachine;
        }

        protected override AnimatorStateMachine IntParamLayer(AvrcParameters.AvrcParameter parameter)
        {
            var stateMachine = new AnimatorStateMachine();

            var states = new AnimatorState[parameter.maxVal - parameter.minVal + 1];
            var perState = 1.0f / states.Length;

            var conditionParamName = $"{parameter.name}_F";
            AddParameter(conditionParamName, AnimatorControllerParameterType.Float);
            AddParameter(Names.ParamTxProximity, AnimatorControllerParameterType.Float);

            for (var i = 0; i < states.Length; i++)
            {
                var hi = (i + 0.5f) * perState;
                var lo = (i - 0.5f) * perState;
                
                states[i] = stateMachine.AddState($"{parameter.name}_{i}");
                states[i].behaviours = new StateMachineBehaviour[] { ParameterDriver(parameter.RxParameterName, i) };
                states[i].motion = AvrcAssets.EmptyClip();

                var transition = stateMachine.AddAnyStateTransition(states[i]);
                transition.duration = 0;
                transition.hasExitTime = false;
                transition.AddCondition(AnimatorConditionMode.Greater, lo, conditionParamName);
                transition.AddCondition(AnimatorConditionMode.Less, hi, conditionParamName);
                transition.AddCondition(AnimatorConditionMode.Greater, PROXIMITY_EPSILON, Names.ParamTxProximity);
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