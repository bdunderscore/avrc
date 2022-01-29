using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace net.fushizen.avrc
{
    [InitializeOnLoad]
    public class AvrcAssetProcessorCallbacks : UnityEditor.AssetModificationProcessor
    {
        private static Dictionary<string, MenuCloner> avrcMenuPaths = new Dictionary<string, MenuCloner>();
        private static HashSet<string> sourceMenuPaths = new HashSet<string>();

        static AvrcAssetProcessorCallbacks() {
            EditorApplication.delayCall += () =>
            {
                foreach (var guid in AssetDatabase.FindAssets("t:AvrcParameters"))
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    var paramsAsset = AssetDatabase.LoadAssetAtPath<AvrcParameters>(assetPath);
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
                var paramsAsset = AssetDatabase.LoadAssetAtPath<AvrcParameters>(assetPath);
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

        /// <summary>
        /// Examine the given avrcParams class, and ensure it is in sync with its source menus.
        /// </summary>
        /// <param name="avrcParams"></param>
        private static void initClones(AvrcParameters avrcParams)
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
                cloner.SyncMenus();
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
                    if (cloner.SyncMenus())
                    {
                        pathList.Add(AssetDatabase.GetAssetPath(cloner.AvrcParams));
                    }
                }

                paths = pathList.ToArray();
            }

            foreach (var p in paths.Where(avrcMenuPaths.ContainsKey))
            {
                avrcMenuPaths[p].SyncMenus();
            }

            return paths;
        }
    }
}