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