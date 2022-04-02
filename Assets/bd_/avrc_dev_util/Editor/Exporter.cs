using System.Linq;
using UnityEditor;
using UnityEngine;

namespace net.fushizen.avrc
{
    public class Exporter : MonoBehaviour
    {
        [MenuItem("bd_/AVRC Dev/Export")]
        private static void Export()
        {
            var assets = AssetDatabase.FindAssets("*", new[] {"Assets/bd_/avrc"})
                .Where(p => !p.EndsWith("Preferences.asset"))
                .Where(p => !p.Contains("/NoExport"))
                .Select(AssetDatabase.GUIDToAssetPath)
                .ToArray();
            Debug.LogWarning(string.Join(", ", assets));

            AssetDatabase.ExportPackage(assets, "avrc-dev.unitypackage");
        }
    }
}