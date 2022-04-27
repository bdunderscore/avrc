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
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !p.EndsWith("Preferences.asset"))
                .Where(p => p.LastIndexOf(".", StringComparison.InvariantCulture) >
                            p.LastIndexOf("/", StringComparison.InvariantCulture)) // not a full directory
                .ToArray();
            Debug.LogWarning(string.Join(", ", assets));
            AssetDatabase.ExportPackage(assets, "avrc-dev.unitypackage");
        }
    }
}