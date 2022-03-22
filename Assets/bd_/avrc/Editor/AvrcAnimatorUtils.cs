using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace net.fushizen.avrc
{
    internal class AvrcAnimatorUtils
    {
        /**
         * Removes unreferenced sub-assets from the given AnimatorController, and conversely, adds as sub-assets
         * unsaved state machines and state behaviors referenced by this AnimatorController.
         */
        internal static void GarbageCollectAnimatorAsset(AnimatorController animator)
        {
            var path = AssetDatabase.GetAssetPath(animator);
            if (path == null) return;

            var assets = AssetDatabase.LoadAllAssetsAtPath(path);

            // Check for multiple Animators first
            foreach (var maybeController in assets)
            {
                if (maybeController is AnimatorController && maybeController != animator)
                {
                    Debug.LogError("Multiple animation controllers found in asset "
                                   + AssetDatabase.GetAssetPath(animator)
                                   + "; not garbage collecting.");
                }
            }

            var referencedAssets = new HashSet<Object>();

            var visitQueue = new Queue<SerializedObject>();
            visitQueue.Enqueue(new SerializedObject(animator));
            referencedAssets.Add(animator);

            while (visitQueue.Count > 0)
            {
                var serializedObject = visitQueue.Dequeue();

                var iterator = serializedObject.GetIterator();
                while (iterator.Next(true))
                {
                    ProcessProperty(iterator);
                }
            }

            void ProcessProperty(SerializedProperty serializedProperty)
            {
                if (serializedProperty.isArray)
                {
                    for (int i = 0; i < serializedProperty.arraySize; i++)
                    {
                        ProcessProperty(serializedProperty.GetArrayElementAtIndex(i));
                    }
                }
                else
                {
                    if (serializedProperty.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        var obj = serializedProperty.objectReferenceValue;
                        if (obj != null && referencedAssets.Add(obj))
                        {
                            visitQueue.Enqueue(new SerializedObject(obj));
                        }
                    }
                }
            }

            foreach (var asset in assets)
            {
                if (asset != null && !referencedAssets.Contains(asset))
                {
                    AssetDatabase.RemoveObjectFromAsset(asset);
                }
            }

            foreach (var referenced in referencedAssets)
            {
                if (AssetDatabase.GetAssetPath(referenced) == "")
                {
                    referenced.hideFlags = HideFlags.HideInHierarchy;
                    AssetDatabase.AddObjectToAsset(referenced, animator);
                }
            }
        }

        public static AnimatorController FindFxLayer(VRCAvatarDescriptor descriptor)
        {
            foreach (var layer in descriptor.baseAnimationLayers)
            {
                if (layer.type == VRCAvatarDescriptor.AnimLayerType.FX && !layer.isDefault)
                    return layer.animatorController as AnimatorController;
            }

            return null;
        }

        public static WriteDefaultsState GetWriteDefaultsState(VRCAvatarDescriptor descriptor)
        {
            if (descriptor == null) return WriteDefaultsState.NoWriteDefaults;

            var hasOff = false;
            var hasOn = false;

            var fx = FindFxLayer(descriptor);

            if (fx == null) return WriteDefaultsState.NoWriteDefaults;

            var toProcess = new Queue<AnimatorStateMachine>();

            foreach (var layer in fx.layers) toProcess.Enqueue(layer.stateMachine);

            while (toProcess.Count > 0)
            {
                var next = toProcess.Dequeue();

                foreach (var state in next.states)
                {
                    var writeDefaults = state.state.writeDefaultValues;
                    if (writeDefaults)
                    {
                        hasOn = true;
                    }
                    else
                    {
                        hasOff = true;
                    }
                }

                foreach (var subStateMachine in next.stateMachines) toProcess.Enqueue(subStateMachine.stateMachine);
            }

            if (hasOff && hasOn) return WriteDefaultsState.Mixed;
            if (hasOff) return WriteDefaultsState.NoWriteDefaults;
            if (hasOn) return WriteDefaultsState.YesWriteDefaults;
            return WriteDefaultsState.NoWriteDefaults;
        }
    }
}