using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Serialization;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace net.fushizen.avrc
{
    [Serializable]
    public struct SignalMapping
    {
        [FormerlySerializedAs("avrcParameterName")]
        public string avrcSignalName;

        public string remappedParameterName;
        public string forwardParameter;
        public int defaultValue;
        public NoSignalMode noSignalMode;
        public bool isSecret;
    }

    [Serializable]
    public enum NoSignalMode
    {
        /// <summary>
        ///     Maintain the current value when no signal is received
        /// </summary>
        Hold,

        /// <summary>
        ///     Reset to a fixed value when no signal is received
        /// </summary>
        Reset,

        /// <summary>
        ///     Forward a different parameter when no signal is received
        /// </summary>
        Forward
    }

    [Serializable]
    public enum WriteDefaultsState
    {
        Mixed,
        YesWriteDefaults,
        NoWriteDefaults
    }

    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public enum Role
    {
        Init,
        TX,
        RX
    }

    public static class RoleExtensions
    {
        public static Role Other(this Role role)
        {
            switch (role)
            {
                case Role.RX: return Role.TX;
                case Role.TX: return Role.RX;
                default: throw new Exception($"Invalid role {role}");
            }
        }
    }

    public class AvrcBindingConfiguration : StateMachineBehaviour
    {
        [FormerlySerializedAs("parameters")] public AvrcLinkSpec linkSpec;

        [FormerlySerializedAs("parameterMappings")]
        public List<SignalMapping> signalMappings = new List<SignalMapping>();

        public Role role = Role.Init;
        public float timeoutSeconds = 5.0f;
        public string layerName;
        public WriteDefaultsState writeDefaults = WriteDefaultsState.Mixed;
    }

    [CustomEditor(typeof(AvrcBindingConfiguration))]
    public class AvrcSavedStateEditor : Editor
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

    internal static class AvrcStateSaver
    {
        internal static AvrcBindingConfiguration LoadState(AvrcLinkSpec linkSpec, VRCAvatarDescriptor descriptor)
        {
            var savedState = LoadStateWithoutCloning(linkSpec, descriptor);

            if (savedState == null)
            {
                return ScriptableObject.CreateInstance<AvrcBindingConfiguration>();
            }

            // Clone behavior to avoid modifying the original when we don't commit
            return Object.Instantiate(savedState);
        }

        internal static AvrcBindingConfiguration LoadStateWithoutCloning(AvrcLinkSpec linkSpec,
            VRCAvatarDescriptor descriptor)
        {
            var entryState = FindStateForStorage(linkSpec, descriptor);

            if (entryState == null) return null;

            return entryState.behaviours.OfType<AvrcBindingConfiguration>().FirstOrDefault();
        }

        private static AnimatorStateMachine FindStateForStorage(AvrcLinkSpec linkSpec,
            VRCAvatarDescriptor descriptor)
        {
            var layers = AvrcAnimatorUtils.FindFxLayer(descriptor)?.layers;
            if (layers == null) return null;

            var setupStateMachine = layers.Where(l =>
            {
                return l.stateMachine != null &&
                       (l.stateMachine.behaviours?.OfType<AvrcLayerMarker>()
                            ?.Any(m => m.LinkSpec == linkSpec)
                        ?? false);
            }).FirstOrDefault()?.stateMachine;
            if (setupStateMachine == null) return null;

            return setupStateMachine;
        }

        internal static void SaveState(VRCAvatarDescriptor descriptor, AvrcBindingConfiguration state)
        {
            var setupStateMachine = FindStateForStorage(state.linkSpec, descriptor);
            if (setupStateMachine == null) throw new Exception("Couldn't find entry state in setup layer");

            var existingState = setupStateMachine.behaviours.OfType<AvrcBindingConfiguration>().FirstOrDefault();
            if (existingState == null)
            {
                existingState = Object.Instantiate(state);
                var tmpList = new List<StateMachineBehaviour>(setupStateMachine.behaviours);
                tmpList.Add(existingState);
                setupStateMachine.behaviours = tmpList.ToArray();
                var assetPath = AssetDatabase.GetAssetPath(setupStateMachine);
                AssetDatabase.AddObjectToAsset(existingState, assetPath);
                EditorUtility.SetDirty(setupStateMachine);
            }

            EditorUtility.CopySerialized(state, existingState);
            existingState.hideFlags = HideFlags.NotEditable;
            existingState.name = $"AVRC Binding Config for {existingState.linkSpec.name}";
            EditorUtility.SetDirty(existingState);
        }
    }
}