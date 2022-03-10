using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using VRC.SDK3.Avatars.Components;

namespace net.fushizen.avrc
{
    internal class AvrcUninstall
    {
        public static bool HasAvrcConfiguration(VRCAvatarDescriptor avatarDescriptor, AvrcParameters parameters = null)
        {
            var names = parameters != null ? new AvrcNames(parameters) : null;
            var fx = AvrcAnimatorUtils.FindFxLayer(avatarDescriptor);

            if (names != null)
            {
                return avatarDescriptor.transform.Find(names.ObjectPath) != null
                       || (fx?.layers ?? Array.Empty<AnimatorControllerLayer>())
                            .Any(layer => layer.name.StartsWith(names.LayerPrefix));
            }
            else
            {
                return avatarDescriptor.transform.Find("AVRC") != null
                       || (fx?.layers ?? Array.Empty<AnimatorControllerLayer>())
                            .Any(layer => layer.name.StartsWith("_AVRC"));
            }
        }

        public static void RemoveAvrcConfiguration(VRCAvatarDescriptor avatarDescriptor, AvrcParameters parameters = null)
        {
            var names = parameters != null ? new AvrcNames(parameters) : null;
            var fx = AvrcAnimatorUtils.FindFxLayer(avatarDescriptor);
            var scene = avatarDescriptor.gameObject.scene;

            var layerPrefix = names?.LayerPrefix ?? "_AVRC_";
            
            // Purge objects
            var objectRootName = parameters != null ? names.ObjectPath : "AVRC";

            var paramsObjectRoot = avatarDescriptor.transform.Find(objectRootName);

            if (paramsObjectRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(paramsObjectRoot.gameObject);
                EditorSceneManager.MarkSceneDirty(scene);
            }

            var avrcRoot = avatarDescriptor.transform.Find("AVRC");

            if (parameters != null && avrcRoot != null)
            {
                bool noChildren = avrcRoot.childCount == 0;
                bool onlyBound = avrcRoot.childCount == 1 && avrcRoot.GetChild(0).name.Equals("AVRC_Bounds");

                if (noChildren || onlyBound)
                {
                    UnityEngine.Object.DestroyImmediate(avrcRoot.gameObject);
                    EditorSceneManager.MarkSceneDirty(scene);
                }
            }
            
            // Purge layers
            if (fx != null)
            {
                Undo.RegisterFullObjectHierarchyUndo(fx, "Remove AVRC");
                fx.layers = fx.layers.Where(layer => !layer.name.StartsWith(layerPrefix)).ToArray();
                EditorUtility.SetDirty(fx);
                AvrcAnimatorUtils.GarbageCollectAnimatorAsset(fx);
            }
        }
    }
}