using System.Linq;
using System.Reflection;
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
                .Where(p => !p.EndsWith("License.asset"))
                .Where(p => !p.Contains("/NoExport"))
                .Select(AssetDatabase.GUIDToAssetPath)
                .ToArray();
            Debug.LogWarning(string.Join(", ", assets));
            AssetDatabase.ExportPackage(assets, "avrc-dev.unitypackage");

            var assembly = Assembly.GetAssembly(typeof(AvrcParameters));
            var gen = assembly.GetType("net.fushizen.avrc.NoExportLicenseGen", true);
            gen.GetMethod("GenerateLicense", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);

            AssetDatabase.SaveAssets();
            AssetDatabase.ExportPackage("Assets/bd_/avrc/License.asset", "avrc-license.unitypackage");
        }
    }
}