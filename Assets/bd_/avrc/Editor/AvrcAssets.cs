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