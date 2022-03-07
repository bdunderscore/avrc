using System;
using System.IO;
using NUnit.Framework.Constraints;
using UnityEditor;
using UnityEngine;

namespace net.fushizen.avrc
{
    [Serializable]
    internal enum Language
    {
        EN,
        JA
    }
    
    public class AvrcPrefs : ScriptableObject
    {
        [SerializeField] internal Language Language;
        
        internal static AvrcPrefs Get()
        {
            AvrcPrefs tmp = ScriptableObject.CreateInstance<AvrcPrefs>();
            MonoScript script = MonoScript.FromScriptableObject(tmp);
            var path = AssetDatabase.GetAssetPath(script);
            var dir = Path.GetDirectoryName(Path.GetDirectoryName(path));
            
            var prefsPath = Path.Combine(dir, "Preferences.asset");
            var existingPrefs = AssetDatabase.LoadAssetAtPath<AvrcPrefs>(prefsPath);
            if (existingPrefs != null)
            {
                return existingPrefs;
            }
            else
            {
                AssetDatabase.CreateAsset(tmp, prefsPath);
                return tmp;
            }
        }
    }
}