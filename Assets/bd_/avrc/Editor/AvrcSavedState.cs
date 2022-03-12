using System;
using System.Collections.Generic;
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
    public class AvrcSavedState : StateMachineBehaviour
    {
        [Serializable]
        public enum Role
        {
            TX, RX
        }
        
        public List<ParameterMapping> parameterMappings = new List<ParameterMapping>();
        public Role role = Role.RX;
    }

    [CustomEditor(typeof(AvrcSavedState))]
    public class AvrcSavedStateEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            // no inspectable attributes
        }
    }

    internal class AvrcStateSaver
    {
        internal static AvrcSavedState LoadState(AvrcNames names, VRCAvatarDescriptor descriptor)
        {
            var savedState = LoadStateWithoutCloning(names, descriptor);

            if (savedState == null)
            {
                return ScriptableObject.CreateInstance<AvrcSavedState>();
            }

            // Clone behavior to avoid modifying the original when we don't commit
            return Object.Instantiate(savedState);
        }

        private static AvrcSavedState LoadStateWithoutCloning(AvrcNames names, VRCAvatarDescriptor descriptor)
        {
            var entryState = FindStateForStorage(names, descriptor);

            if (entryState == null) return null;

            return entryState.behaviours.OfType<AvrcSavedState>().FirstOrDefault();
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

        internal static void SaveState(AvrcNames names, VRCAvatarDescriptor descriptor, AvrcSavedState state)
        {
            var entryState = FindStateForStorage(names, descriptor);
            if (entryState == null) throw new Exception("Couldn't find entry state in setup layer");


            var existingState = entryState.behaviours.OfType<AvrcSavedState>().FirstOrDefault();
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
            EditorUtility.SetDirty(existingState);
        }
    }
}