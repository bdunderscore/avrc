/*
 * MIT License
 * 
 * Copyright (c) 2021-2022 bd_
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE. 
 */

using System;
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