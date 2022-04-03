using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace net.fushizen.avrc
{
    internal static class AvrcUninstall
    {
        public static bool HasAvrcConfiguration(VRCAvatarDescriptor avatarDescriptor, AvrcLinkSpec linkSpec = null)
        {
            var names = linkSpec != null ? new AvrcNames(linkSpec) : null;
            var fx = AvrcAnimatorUtils.FindFxLayer(avatarDescriptor);

            if (names != null)
            {
                return avatarDescriptor.transform.Find(names.ObjectPath) != null
                       || (fx?.layers ?? Array.Empty<AnimatorControllerLayer>())
                       .Any(layer => AvrcLayerMarker.IsAvrcLayer(layer));
            }
            else
            {
                return avatarDescriptor.transform.Find("AVRC") != null
                       || (fx?.layers ?? Array.Empty<AnimatorControllerLayer>())
                       .Any(layer => AvrcLayerMarker.IsMatchingLayer(layer, linkSpec));
            }
        }

        public static void RemoveAvrcConfiguration(VRCAvatarDescriptor avatarDescriptor,
            AvrcBindingConfiguration bindingConfiguration = null
        )
        {
            AvrcNames names = null;
            if (bindingConfiguration != null) names = new AvrcNames(bindingConfiguration);
            var fx = AvrcAnimatorUtils.FindFxLayer(avatarDescriptor);
            var scene = avatarDescriptor.gameObject.scene;

            var layerPrefix = names?.LayerPrefix ?? "_AVRC_";

            var paramPrefixes = names != null
                ? new string[] {names.ParamPrefix, names.PubParamPrefix}
                : new string[] {"AVRC_", "_AVRCI_"};

            // Purge objects
            var objectRootName = names?.ObjectPath ?? "AVRC";

            var paramsObjectRoot = avatarDescriptor.transform.Find(objectRootName);

            if (paramsObjectRoot != null)
            {
                Object.DestroyImmediate(paramsObjectRoot.gameObject);
                EditorSceneManager.MarkSceneDirty(scene);
            }

            var avrcRoot = avatarDescriptor.transform.Find("AVRC");

            if (bindingConfiguration != null && avrcRoot != null)
            {
                bool noChildren = avrcRoot.childCount == 0;
                bool onlyBound = avrcRoot.childCount == 1 && avrcRoot.GetChild(0).name.Equals("AVRC_Bounds");

                if (noChildren || onlyBound)
                {
                    Object.DestroyImmediate(avrcRoot.gameObject);
                    EditorSceneManager.MarkSceneDirty(scene);
                }
            }

            // Purge layers
            if (fx != null)
            {
                Undo.RegisterFullObjectHierarchyUndo(fx, "Remove AVRC");
                fx.layers = fx.layers.Where(layer =>
                {
                    if (bindingConfiguration != null && bindingConfiguration.linkSpec != null)
                        return !AvrcLayerMarker.IsMatchingLayer(layer, bindingConfiguration.linkSpec);
                    return !AvrcLayerMarker.IsAvrcLayer(layer);
                }).ToArray();
                fx.parameters = fx.parameters
                    .Where(parameter => paramPrefixes.All(prefix => !parameter.name.StartsWith(prefix)))
                    .ToArray();

                var avrcLayers = fx.layers.SelectMany(l =>
                {
                    GlobalLayerType ty;

                    if (AvrcLayerMarker.IsAvrcLayer(l, out ty))
                        return new[] {(ty, l)};
                    return Array.Empty<(GlobalLayerType, AnimatorControllerLayer)>();
                }).ToArray();

                if (avrcLayers.Length > 0 && avrcLayers.All(pair => pair.Item1 != GlobalLayerType.NotGlobalLayer))
                    // Purge all AVRC layers
                    fx.layers = fx.layers.Where(l => !AvrcLayerMarker.IsAvrcLayer(l)).ToArray();

                EditorUtility.SetDirty(fx);
                AvrcAnimatorUtils.GarbageCollectAnimatorAsset(fx);
            }
        }
    }
}