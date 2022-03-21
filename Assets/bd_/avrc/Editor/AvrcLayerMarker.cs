using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace net.fushizen.avrc
{
    [Serializable]
    public enum GlobalLayerType
    {
        NotGlobalLayer,
        BoundsSetup
    }

    public class AvrcLayerMarker : StateMachineBehaviour
    {
        public AvrcParameters Parameters;
        public GlobalLayerType GlobalLayerType;

        internal static void MarkLayer(
            AnimatorStateMachine stateMachine,
            AvrcParameters parameters = null,
            GlobalLayerType globalLayerType = GlobalLayerType.NotGlobalLayer
        )
        {
            var behaviour = stateMachine.behaviours.OfType<AvrcLayerMarker>().FirstOrDefault();

            if (behaviour == null)
            {
                var list = new List<StateMachineBehaviour>(stateMachine.behaviours);
                behaviour = CreateInstance<AvrcLayerMarker>();
                list.Add(behaviour);
                stateMachine.behaviours = list.ToArray();
            }

            behaviour.hideFlags = HideFlags.NotEditable;
            behaviour.name = "AVRC Layer Marker";
            behaviour.GlobalLayerType = globalLayerType;
            behaviour.Parameters = parameters;
        }

        internal static bool IsAvrcLayer(AnimatorControllerLayer layer)
        {
            return layer.stateMachine != null &&
                   (layer.stateMachine.behaviours?.OfType<AvrcLayerMarker>().Any() ?? false);
        }

        internal static bool IsMatchingLayer(AnimatorControllerLayer layer, AvrcParameters parameters)
        {
            return layer.stateMachine != null && (
                layer.stateMachine.behaviours?.OfType<AvrcLayerMarker>().Any(
                    behavior => behavior.Parameters == parameters
                ) ?? false
            );
        }
    }

    [CustomEditor(typeof(AvrcLayerMarker))]
    internal class AvrcLayerMarkerInspector : Editor
    {
        private bool foldout;

        public override void OnInspectorGUI()
        {
            foldout = EditorGUILayout.Foldout(foldout, "Debug display");
            if (foldout)
                using (new EditorGUI.DisabledScope(true))
                {
                    base.OnInspectorGUI();
                }
        }
    }
}