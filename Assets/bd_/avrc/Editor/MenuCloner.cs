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
        private readonly UnityEngine.Object _containingObject;
        private readonly SerializedProperty _dstProperty;
        private readonly bool _notReady;

        private Dictionary<string, string> _paramRemapDict;

        private HashSet<VRCExpressionsMenu> enqueuedAssets = new HashSet<VRCExpressionsMenu>();
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

        internal UnityEngine.Object ContainingObject
        {
            get { return _containingObject; }
        }

        internal string ContainingPath
        {
            get { return AssetDatabase.GetAssetPath(ContainingObject); }
        }

        public static MenuCloner InitCloner(AvrcParameters avrcParameters)
        {
            var cloner =
                new MenuCloner(
                    new SerializedObject(avrcParameters).FindProperty(nameof(AvrcParameters.embeddedExpressionsMenu)),
                    avrcParameters);

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
            target.name = src.name;
            enqueuedAssets.Add(target);
            pendingClone.Enqueue(src);

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
                if (obj is VRCExpressionsMenu menu && !enqueuedAssets.Contains(menu))
                {
                    AssetDatabase.RemoveObjectFromAsset(menu);
                    UnityEngine.Object.DestroyImmediate(menu);
                }
            }
        }
    }
}