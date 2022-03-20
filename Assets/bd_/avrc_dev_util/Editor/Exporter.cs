using UnityEditor;
using UnityEngine;

public class Exporter : MonoBehaviour
{
    [MenuItem("bd_/AVRC Dev/Export")]
    private static void Export()
    {
        AssetDatabase.ExportPackage("Assets/bd_/avrc", "avrc-dev.unitypackage", ExportPackageOptions.Recurse);
    }
}