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
using VRC.SDK3.Avatars.ScriptableObjects;

namespace net.fushizen.avrc
{
    /// <summary>
    /// Clones a VRCExpressionsMenu to an asset embedded within an AvrcParameters asset, and maintains the relationship
    /// between the two as constituent assets are modified.
    /// </summary>
    public class MenuCloner
    {
        private readonly Object _containingObject;
        private readonly SerializedProperty _dstProperty;
        private readonly bool _notReady;
        private readonly HashSet<long> retainedLocalIds = new HashSet<long>();

        private Dictionary<string, string> _paramRemapDict;

        private HashSet<VRCExpressionsMenu> enqueuedAssets = new HashSet<VRCExpressionsMenu>();

        internal HideFlags hideFlags = HideFlags.None;
        internal string objectNamePrefix = "ZZZ_AVRC_";
        private Queue<VRCExpressionsMenu> pendingClone = new Queue<VRCExpressionsMenu>();

        private Dictionary<VRCExpressionsMenu, VRCExpressionsMenu> srcToCloneMap
            = new Dictionary<VRCExpressionsMenu, VRCExpressionsMenu>();

        public MenuCloner(
            SerializedProperty dstProperty,
            Object containingObject,
            Dictionary<string, string> paramRemapDict = null
        )
        {
            _dstProperty = dstProperty;
            _containingObject = containingObject;
            _paramRemapDict = paramRemapDict ?? new Dictionary<string, string>();

            var path = ContainingPath;
            _notReady = path == null || path.Equals("");
            if (_notReady) return;
        }

        /// <summary>
        /// Returns the set of asset paths that correspond to the source menus (including submenus) for this cloner. +        /// </summary>
        internal IEnumerable<string> SourcePaths
        {
            get { return srcToCloneMap.Keys.Select(AssetDatabase.GetAssetPath); }
        }

        internal SerializedProperty ReferencingProperty
        {
            get { return _dstProperty; }
        }

        internal Object ContainingObject
        {
            get { return _containingObject; }
        }

        internal string ContainingPath
        {
            get { return AssetDatabase.GetAssetPath(ContainingObject); }
        }

        public static MenuCloner InitCloner(AvrcLinkSpec avrcLinkSpec)
        {
            var cloner =
                new MenuCloner(
                    new SerializedObject(avrcLinkSpec).FindProperty(nameof(AvrcLinkSpec.embeddedExpressionsMenu)),
                    avrcLinkSpec);
            cloner.hideFlags = HideFlags.HideInInspector | HideFlags.NotEditable;
            cloner.objectNamePrefix = "ZZZ_AVRC_EMBEDDED_";

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
                return srcToCloneMap[src];
            }
            else if (hint != null && !enqueuedAssets.Contains(hint)
                                  && ContainingPath.Equals(AssetDatabase.GetAssetPath(hint)))
            {
                target = hint;
            }
            else
            {
                var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                AssetDatabase.AddObjectToAsset(menu, ContainingObject);
                target = menu;
            }

            srcToCloneMap[src] = target;
            var newName = objectNamePrefix + src.name;
            if (target.name != newName || target.hideFlags != hideFlags)
            {
                target.hideFlags = hideFlags;
                target.name = newName;
                EditorUtility.SetDirty(target);
            }

            enqueuedAssets.Add(target);
            pendingClone.Enqueue(src);

            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(target, out _, out long localId))
                retainedLocalIds.Add(localId);
            else
                Debug.LogError($"Failed to get GUID and local file identifier for {target.name}");

            return target;
        }

        private bool SyncSingleMenu(VRCExpressionsMenu sourceMenu)
        {
            var source = new SerializedObject(sourceMenu);
            var target = new SerializedObject(srcToCloneMap[sourceMenu]);
            source.UpdateIfRequiredOrScript();
            target.UpdateIfRequiredOrScript();

            bool dirty = false;

            var iter = source.GetIterator();

            bool enterChildren = true;
            while (iter.Next(enterChildren))
            {
                if (iter.name == "m_ObjectHideFlags" || iter.name == "m_Name")
                {
                    enterChildren = false;
                    continue;
                }

                enterChildren = true;

                if (iter.propertyType == SerializedPropertyType.ObjectReference &&
                    iter.objectReferenceValue is VRCExpressionsMenu menu)
                {
                    var targetProp = target.FindProperty(iter.propertyPath);
                    var submenu = mapAsset(menu, targetProp.objectReferenceValue as VRCExpressionsMenu);

                    if (targetProp.objectReferenceValue != submenu)
                    {
                        targetProp.objectReferenceValue = submenu;
                        dirty = true;
                    }

                    // Skip the m_FileID property
                    enterChildren = false;
                }
                else if (iter.propertyPath.EndsWith("parameter.name") &&
                         iter.propertyType == SerializedPropertyType.String)
                {
                    if (_paramRemapDict.ContainsKey(iter.stringValue))
                    {
                        var remapped = _paramRemapDict[iter.stringValue];
                        var targetProp = target.FindProperty(iter.propertyPath);
                        if (!remapped.Equals(targetProp.stringValue))
                        {
                            targetProp.stringValue = remapped;
                            dirty = true;
                        }

                        // skip the inner array
                        enterChildren = false;
                    }
                }
                else if (iter.propertyPath.Contains("subParameters.Array.data") &&
                         iter.propertyPath.EndsWith(".name") &&
                         iter.propertyType == SerializedPropertyType.String)
                {
                    if (_paramRemapDict.ContainsKey(iter.stringValue))
                    {
                        var remapped = _paramRemapDict[iter.stringValue];
                        var targetProp = target.FindProperty(iter.propertyPath);
                        if (!remapped.Equals(targetProp.stringValue))
                        {
                            targetProp.stringValue = remapped;
                            dirty = true;
                        }

                        // skip the inner array
                        enterChildren = false;
                    }
                }
                else
                {
                    var changed = target.CopyFromSerializedPropertyIfDifferent(iter);

                    if (changed)
                    {
                        dirty = true;
                    }
                }
            }

            if (dirty)
            {
                target.ApplyModifiedPropertiesWithoutUndo();
                target.UpdateIfRequiredOrScript();
                EditorUtility.SetDirty(srcToCloneMap[sourceMenu]);
            }

            return dirty;
        }

        public bool SyncMenus(VRCExpressionsMenu sourceMenu)
        {
            enqueuedAssets.Clear();
            pendingClone.Clear();

            _dstProperty.serializedObject.UpdateIfRequiredOrScript();

            bool dirty;
            if (sourceMenu == null)
            {
                CleanAssets();
                return false;
            }

            // Bootstrap
            var priorValue = _dstProperty.objectReferenceValue;
            _dstProperty.objectReferenceValue = mapAsset(sourceMenu, priorValue as VRCExpressionsMenu);
            dirty = priorValue != _dstProperty.objectReferenceValue;

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
                _dstProperty.serializedObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(ContainingObject);
            }

            CleanAssets();

            return dirty;
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

            var objects = AssetDatabase.LoadAllAssetsAtPath(ContainingPath);
            foreach (var obj in objects)
            {
                long localId = -1;
                if (obj is VRCExpressionsMenu menu
                    && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(menu, out _, out localId)
                    && !retainedLocalIds.Contains(localId))
                {
                    AssetDatabase.RemoveObjectFromAsset(menu);
                    Object.DestroyImmediate(menu);
                }
            }
        }
    }
}