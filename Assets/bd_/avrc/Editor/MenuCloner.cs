using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.VersionControl;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace net.fushizen.avrc
{
    /// <summary>
    /// Clones a VRCExpressionsMenu to an asset embedded within an AvrcParameters asset, and maintains the relationship
    /// between the two as constituent assets are modified.
    /// </summary>
    public class MenuCloner
    {
        private readonly AvrcParameters _avrcParameters;
        private readonly bool _notReady;

        private HashSet<VRCExpressionsMenu> enqueuedAssets = new HashSet<VRCExpressionsMenu>();
        private Queue<VRCExpressionsMenu> pendingClone = new Queue<VRCExpressionsMenu>();
        private Dictionary<VRCExpressionsMenu, VRCExpressionsMenu> srcToCloneMap
            = new Dictionary<VRCExpressionsMenu, VRCExpressionsMenu>();

        private MenuCloner(AvrcParameters avrcParams)
        {
            _avrcParameters = avrcParams;

            var path = AssetDatabase.GetAssetPath(_avrcParameters);
            _notReady = (path == null || path.Equals("") || _avrcParameters.sourceExpressionMenu == null);
            if (_notReady) return;

            // Create a new VRCExpressionsMenu asset if not present
            if (_avrcParameters.embeddedExpressionsMenu == null)
            {
                var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                _avrcParameters.embeddedExpressionsMenu = menu;
                AssetDatabase.AddObjectToAsset(menu, _avrcParameters);
            }

            srcToCloneMap[_avrcParameters.sourceExpressionMenu] = _avrcParameters.embeddedExpressionsMenu;
        }

        public static MenuCloner InitCloner(AvrcParameters avrcParameters)
        {
            var cloner = new MenuCloner(avrcParameters);

            if (cloner._notReady)
            {
                return null;
            }

            return cloner;
        }

        private VRCExpressionsMenu mapAsset(VRCExpressionsMenu src, VRCExpressionsMenu hint = null)
        {
            VRCExpressionsMenu target;
            if (srcToCloneMap.ContainsKey(src))
            {
                target = srcToCloneMap[src];
            }
            else if (hint != null && !enqueuedAssets.Contains(hint)
                                  && AssetDatabase.GetAssetPath(_avrcParameters).Equals(AssetDatabase.GetAssetPath(hint)))
            {
                target = hint;

            }
            else {
                var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                AssetDatabase.AddObjectToAsset(menu, _avrcParameters);
                target = menu;
            }
            
            srcToCloneMap[src] = target;
            target.name = src.name;
            enqueuedAssets.Add(target);
            Debug.Log($"Enqueue source asset {AssetDatabase.GetAssetPath(src)}");
            pendingClone.Enqueue(src);

            return target;
        }

        private bool SyncSingleMenu(VRCExpressionsMenu sourceMenu)
        {
            Debug.Log($"Visit source asset {AssetDatabase.GetAssetPath(sourceMenu)}");
            var source = new SerializedObject(sourceMenu);
            var target = new SerializedObject(srcToCloneMap[sourceMenu]);
            source.UpdateIfRequiredOrScript();
            target.UpdateIfRequiredOrScript();

            bool dirty = false;
            
            var iter = source.GetIterator();

            bool enterChildren = true;
            while (iter.Next(enterChildren))
            {
                enterChildren = true;
                
                if (iter.propertyType == SerializedPropertyType.ObjectReference && iter.objectReferenceValue is VRCExpressionsMenu menu)
                {
                    var targetProp = target.FindProperty(iter.propertyPath);
                    var submenu = mapAsset(menu, targetProp.objectReferenceValue as VRCExpressionsMenu);
                    
                    Debug.Log($"Target {(targetProp.objectReferenceValue != null ? "" + targetProp.objectReferenceValue.GetInstanceID() : "(null)")} " +
                              $"Source {menu.GetInstanceID()} Clone {submenu.GetInstanceID()}");
                    if (targetProp.objectReferenceValue != submenu)
                    {
                        targetProp.objectReferenceValue = submenu;
                        dirty = true;
                        Debug.Log($"{iter.propertyPath}/{iter.propertyType}");
                    }
                    
                    // Skip the m_FileID property
                    enterChildren = false;
                }
                else
                {
                    var changed = target.CopyFromSerializedPropertyIfDifferent(iter);

                    if (changed)
                    {
                        dirty = true;
                        Debug.Log($"{iter.propertyPath}/{iter.propertyType}");
                    }
                }
            }

            if (dirty)
            {
                Debug.Log($"Dirty: {target.targetObject.name}");
                target.ApplyModifiedPropertiesWithoutUndo();
                target.UpdateIfRequiredOrScript();
                EditorUtility.SetDirty(srcToCloneMap[sourceMenu]);
            }

            return dirty;
        }
        
        public void SyncMenus()
        {
            enqueuedAssets.Clear();
            pendingClone.Clear();
            
            // Bootstrap
            mapAsset(_avrcParameters.sourceExpressionMenu);

            bool dirty = false;
            while (pendingClone.Count > 0)
            {
                var nextToClone = pendingClone.Dequeue();

                if (SyncSingleMenu(nextToClone))
                {
                    dirty = true;
                }
            }

            if (dirty)
            {
                EditorUtility.SetDirty(_avrcParameters);
            }
            
            CleanAssets();
        }

        public void CleanAssets()
        {
            List<VRCExpressionsMenu> toRemoveFromMap = new List<VRCExpressionsMenu>();
            foreach (var entry in srcToCloneMap)
            {
                if (!enqueuedAssets.Contains(entry.Value))
                {
                    toRemoveFromMap.Add(entry.Key);
                }
            }

            foreach (var toRemove in toRemoveFromMap)
            {
                srcToCloneMap.Remove(toRemove);
            }
            
            var objects = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(_avrcParameters));
            foreach (var obj in objects)
            {
                if (obj is VRCExpressionsMenu menu && !enqueuedAssets.Contains(menu))
                {
                    Debug.Log($"Pruning asset {menu.name}");
                    AssetDatabase.RemoveObjectFromAsset(menu);
                    UnityEngine.Object.DestroyImmediate(menu);
                }
            }
        }
    }
}