using System.IO;
using UnityEditor;
using UnityEngine;

namespace net.fushizen.avrc
{
    internal class AvrcAssets
    {
        private const string GUID_PREFAB_ORIGIN = "1af45b2d489f9a745953030d67db2095";
        private const string GUID_EMPTY_CLIP = "f9e80303821c9ea4a8e686817ae423bc";

        internal static GameObject Origin()
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(
                AssetDatabase.GUIDToAssetPath(GUID_PREFAB_ORIGIN)
            );
        }

        internal static AnimationClip EmptyClip()
        {
            return AssetDatabase.LoadAssetAtPath<AnimationClip>(
                AssetDatabase.GUIDToAssetPath(GUID_EMPTY_CLIP)
            );
        }

        internal static string GetGeneratedAssetsFolder()
        {
            var path = AssetDatabase.GUIDToAssetPath(GUID_PREFAB_ORIGIN);
            var dir = Path.GetDirectoryName(path);
            var generatedPath = dir + "/Generated";
            if (!AssetDatabase.IsValidFolder(generatedPath))
            {
                AssetDatabase.CreateFolder(dir, "Generated");
            }

            return generatedPath;
        }
    }
}