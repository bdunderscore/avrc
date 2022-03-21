using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;

namespace net.fushizen.avrc
{
    public class AvrcLayerMarker : StateMachineBehaviour
    {
        [HideInInspector] public AvrcParameters parameters;

        internal static void MarkLayer(AnimatorStateMachine stateMachine, AvrcParameters parameters)
        {
            var startState = stateMachine.defaultState;

            var existingBehavior = startState.behaviours.OfType<AvrcLayerMarker>().FirstOrDefault();

            if (existingBehavior == null)
            {
                var list = new List<StateMachineBehaviour>(startState.behaviours);
                existingBehavior = CreateInstance<AvrcLayerMarker>();
                list.Add(existingBehavior);
                startState.behaviours = list.ToArray();
            }

            existingBehavior.parameters = parameters;
        }

        internal static bool IsAvrcLayer(AnimatorControllerLayer layer)
        {
            return layer.stateMachine.defaultState.behaviours.OfType<AvrcLayerMarker>().Any();
        }

        internal static bool IsMatchingLayer(AnimatorControllerLayer layer, AvrcParameters parameters)
        {
            return layer.stateMachine.defaultState.behaviours.OfType<AvrcLayerMarker>().Any(
                behavior => behavior.parameters == parameters
            );
        }
    }
}