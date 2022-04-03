﻿using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace net.fushizen.avrc
{
    [Serializable]
    public enum Language
    {
        EN,
        JA
    }

    public class AvrcPrefs : ScriptableObject
    {
        [SerializeField] internal Language Language;

        internal static AvrcPrefs Get()
        {
            AvrcPrefs tmp = CreateInstance<AvrcPrefs>();
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