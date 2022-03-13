using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace net.fushizen.avrc
{
    [Serializable]
    public struct ParameterMapping
    {
        public string avrcParameterName;
        public string remappedParameterName;
    }

    public class AvrcBindingConfiguration : StateMachineBehaviour
    {
        [Serializable]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public enum Role
        {
            Init,
            TX,
            RX
        }

        public AvrcParameters parameters;

        public List<ParameterMapping> parameterMappings = new List<ParameterMapping>();
        public Role role = Role.Init;
        public float timeoutSeconds = 5.0f;
    }

    [CustomEditor(typeof(AvrcBindingConfiguration))]
    public class AvrcSavedStateEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            // no inspectable attributes
        }
    }

    internal class AvrcStateSaver
    {
        internal static AvrcBindingConfiguration LoadState(AvrcNames names, VRCAvatarDescriptor descriptor)
        {
            var savedState = LoadStateWithoutCloning(names, descriptor);

            if (savedState == null)
            {
                return ScriptableObject.CreateInstance<AvrcBindingConfiguration>();
            }

            // Clone behavior to avoid modifying the original when we don't commit
            return Object.Instantiate(savedState);
        }

        private static AvrcBindingConfiguration LoadStateWithoutCloning(AvrcNames names, VRCAvatarDescriptor descriptor)
        {
            var entryState = FindStateForStorage(names, descriptor);

            if (entryState == null) return null;

            return entryState.behaviours.OfType<AvrcBindingConfiguration>().FirstOrDefault();
        }

        private static AnimatorState FindStateForStorage(AvrcNames names, VRCAvatarDescriptor descriptor)
        {
            var layers = AvrcAnimatorUtils.FindFxLayer(descriptor)?.layers;
            if (layers == null) return null;

            var setupLayer = layers.FirstOrDefault(layer => layer.name.Equals(names.LayerSetup))?.stateMachine;
            if (setupLayer == null) return null;

            var entryState = setupLayer.defaultState;
            return entryState;
        }

        internal static void SaveState(AvrcNames names, VRCAvatarDescriptor descriptor, AvrcBindingConfiguration state)
        {
            var entryState = FindStateForStorage(names, descriptor);
            if (entryState == null) throw new Exception("Couldn't find entry state in setup layer");


            var existingState = entryState.behaviours.OfType<AvrcBindingConfiguration>().FirstOrDefault();
            if (existingState == null)
            {
                existingState = Object.Instantiate(state);
                var tmpList = new List<StateMachineBehaviour>(entryState.behaviours);
                tmpList.Add(existingState);
                entryState.behaviours = tmpList.ToArray();
                var assetPath = AssetDatabase.GetAssetPath(entryState);
                AssetDatabase.AddObjectToAsset(existingState, assetPath);
                EditorUtility.SetDirty(entryState);
            }

            EditorUtility.CopySerialized(state, existingState);
            existingState.hideFlags = HideFlags.HideInInspector;
            EditorUtility.SetDirty(existingState);
        }
    }
}