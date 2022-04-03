using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Serialization;

namespace net.fushizen.avrc
{
    [Serializable]
    public enum GlobalLayerType
    {
        NotGlobalLayer,
        GlobalDefaults
    }

    public class AvrcLayerMarker : StateMachineBehaviour
    {
        [FormerlySerializedAs("Parameters")] public AvrcLinkSpec LinkSpec;
        public GlobalLayerType GlobalLayerType;

        internal static void MarkLayer(
            AnimatorStateMachine stateMachine,
            AvrcLinkSpec linkSpec = null,
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
            behaviour.LinkSpec = linkSpec;
        }

        internal static bool IsAvrcLayer(AnimatorControllerLayer layer, out GlobalLayerType globalLayerType)
        {
            globalLayerType = GlobalLayerType.NotGlobalLayer;
            if (layer.stateMachine == null) return false;

            var behaviour = layer.stateMachine.behaviours?.OfType<AvrcLayerMarker>().FirstOrDefault();
            if (behaviour != null) globalLayerType = behaviour.GlobalLayerType;

            return behaviour != null;
        }

        internal static bool IsAvrcLayer(AnimatorControllerLayer layer)
        {
            return IsAvrcLayer(layer, out _);
        }

        internal static bool IsMatchingLayer(AnimatorControllerLayer layer, AvrcLinkSpec linkSpec)
        {
            return layer.stateMachine != null && (
                layer.stateMachine.behaviours?.OfType<AvrcLayerMarker>().Any(
                    behavior => behavior.LinkSpec == linkSpec
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