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

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace net.fushizen.avrc
{
    [InitializeOnLoad]
    internal class AvrcAssetProcessorCallbacks : UnityEditor.AssetModificationProcessor
    {
        private static Dictionary<string, MenuCloner> avrcMenuPaths = new Dictionary<string, MenuCloner>();
        private static HashSet<string> sourceMenuPaths = new HashSet<string>();

        static AvrcAssetProcessorCallbacks()
        {
            EditorApplication.delayCall += () =>
            {
                foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(AvrcLinkSpec)))
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    var paramsAsset = AssetDatabase.LoadAssetAtPath<AvrcLinkSpec>(assetPath);
                    if (paramsAsset != null)
                    {
                        initClones(paramsAsset);
                    }
                }

                UpdateMenuAssets();
            };
        }

        private static void OnWillCreateAsset(string assetPath)
        {
            Debug.Log($"OnWillCreateAsset({assetPath})");

            EditorApplication.delayCall += () =>
            {
                var paramsAsset = AssetDatabase.LoadAssetAtPath<AvrcLinkSpec>(assetPath);
                if (paramsAsset != null)
                {
                    initClones(paramsAsset);

                    UpdateMenuAssets();
                }
            };
        }

        private static AssetDeleteResult OnWillDeleteAsset(string assetPath, RemoveAssetOptions options)
        {
            if (avrcMenuPaths.Remove(assetPath))
            {
                UpdateMenuAssets();
            }

            return AssetDeleteResult.DidNotDelete;
        }

        private static AssetMoveResult OnWillMoveAsset(string sourcePath, string destinationPath)
        {
            if (avrcMenuPaths.ContainsKey(sourcePath))
            {
                avrcMenuPaths[destinationPath] = avrcMenuPaths[sourcePath];
                avrcMenuPaths.Remove(sourcePath);
            }

            return AssetMoveResult.DidNotMove;
        }

        static string[] OnWillSaveAssets(string[] paths)
        {
            var pathList = new HashSet<string>(paths);

            if (paths.Any(p => sourceMenuPaths.Contains(p)))
            {
                foreach (var cloner in avrcMenuPaths.Values)
                {
                    if (cloner.SyncMenus((cloner.ContainingObject as AvrcLinkSpec)?.sourceExpressionMenu))
                    {
                        pathList.Add(cloner.ContainingPath);
                    }
                }

                paths = pathList.ToArray();
            }

            foreach (var p in paths.Where(avrcMenuPaths.ContainsKey))
            {
                var cloner = avrcMenuPaths[p];
                var srcMenu = (cloner.ContainingObject as AvrcLinkSpec)?.sourceExpressionMenu;
                if (srcMenu != null) avrcMenuPaths[p].SyncMenus(srcMenu);
            }

            return paths;
        }

        /// <summary>
        /// Examine the given signals class, and ensure it is in sync with its source menus.
        /// </summary>
        /// <param name="avrcParams"></param>
        internal static void initClones(AvrcLinkSpec avrcParams)
        {
            var path = AssetDatabase.GetAssetPath(avrcParams);
            var cloner = MenuCloner.InitCloner(avrcParams);

            if (cloner == null)
            {
                avrcMenuPaths.Remove(path);
            }
            else
            {
                avrcMenuPaths[path] = cloner;
                if (avrcParams.sourceExpressionMenu != null) cloner.SyncMenus(avrcParams.sourceExpressionMenu);
            }
        }

        private static void UpdateMenuAssets()
        {
            sourceMenuPaths = new HashSet<string>(
                avrcMenuPaths.Values
                    .Where(v => v != null)
                    .SelectMany(v => v.SourcePaths)
                    .Where(p => p != null && !p.Equals(""))
            );
        }
    }
}