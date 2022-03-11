using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using NUnit.Framework.Constraints;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace net.fushizen.avrc
{
    internal class MenuReference : ScriptableObject
    {
        public VRCExpressionsMenu menu;
    } 
    
    public class InstallWindow : EditorWindow
    {
        private AvrcParameters _params;
        private VRCAvatarDescriptor _targetAvatar;
        private VRCExpressionsMenu _installMenu;
        private AvrcSavedState.Role _role = AvrcSavedState.Role.RX;
        
        private Localizations L => Localizations.Inst;
        
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public void EditorWindow()
        {
            titleContent = new GUIContent(L.INST_TITLE);
        }
        
        [MenuItem("Window/bd_/AVRC Installer")]
        internal static void DisplayWindow(AvrcParameters p = null)
        {
            var window = GetWindow<InstallWindow>(Localizations.Inst.INST_TITLE.text);

            if (p != null)
            {
                window._params = p;
            }
        }

        private void OnEnable()
        {
            InitRemapPanel();
        }

        private void OnGUI()
        {
            Localizations.SwitchLanguageButton();

            _params = EditorGUILayout.ObjectField(
                L.INST_PARAMS, _params, typeof(AvrcParameters), allowSceneObjects: false
            ) as AvrcParameters;
            var priorTargetAvatar = _targetAvatar;
            _targetAvatar = EditorGUILayout.ObjectField(
                L.INST_AVATAR, _targetAvatar, typeof(VRCAvatarDescriptor), allowSceneObjects: true
            ) as VRCAvatarDescriptor;

            if (_targetAvatar != priorTargetAvatar)
            {
                _installMenu = null;
                InitRemapPanel();
            }

            // ReSharper disable once HeapView.BoxingAllocation
            _role = (AvrcSavedState.Role)EditorGUILayout.EnumPopup("Role", _role);

            if (_role != AvrcSavedState.Role.RX)
            {
                using (new EditorGUI.DisabledGroupScope(_params == null || _params.embeddedExpressionsMenu == null))
                {
                    _installMenu = EditorGUILayout.ObjectField(
                        L.INST_MENU, _installMenu, typeof(VRCExpressionsMenu), allowSceneObjects: false
                    ) as VRCExpressionsMenu;
                }
            }

            DrawRemapPanel();

            var prechecks = IsReadyToInstall();

            using (new EditorGUI.DisabledGroupScope(!prechecks))
            {
                using (new GUILayout.HorizontalScope())
                {
                    if (_role == AvrcSavedState.Role.TX && GUILayout.Button(L.INST_TX))
                    {
                        ApplyTransmitter();
                    }

                    if (_role == AvrcSavedState.Role.RX && GUILayout.Button(L.INST_RX))
                    {
                        ApplyReceiver();
                    }
                }
            }

            bool hasThisAvrc = (_params != null && _targetAvatar != null) 
                               && AvrcUninstall.HasAvrcConfiguration(_targetAvatar, _params);
            bool hasAnyAvrc = _targetAvatar != null && AvrcUninstall.HasAvrcConfiguration(_targetAvatar, null);

            using (new EditorGUI.DisabledGroupScope(!hasThisAvrc))
            {
                if (GUILayout.Button(L.INST_UNINSTALL))
                {
                    AvrcUninstall.RemoveAvrcConfiguration(_targetAvatar, _params);
                }
            }
            using (new EditorGUI.DisabledGroupScope(!hasAnyAvrc))
            {
                if (GUILayout.Button(L.INST_UNINSTALL_ALL))
                {
                    AvrcUninstall.RemoveAvrcConfiguration(_targetAvatar, null);
                }
            }
        }

        // ReSharper disable Unity.PerformanceAnalysis
        private void ApplyReceiver()
        {
            var avrcParameters = _params;
            if (avrcParameters == null) return;
            
            var root = CreateRoot(_targetAvatar.gameObject);

            var names = ApplyNameOverrides();
            var objects = new AvrcObjects(avrcParameters, names);
            
            objects.buildReceiverBase(root, names.Prefix);
            AvrcRxStateMachines.SetupRx(_targetAvatar, avrcParameters, names);
            
            InstallMenu();
        }

        // ReSharper disable Unity.PerformanceAnalysis
        private void ApplyTransmitter()
        {
            var avrcParameters = _params;
            if (avrcParameters == null) return;
            
            var root = CreateRoot(_targetAvatar.gameObject);
            
            var names = ApplyNameOverrides();
            var objects = new AvrcObjects(avrcParameters, names);
            
            objects.buildTransmitterBase(root, names.Prefix);
            AvrcTxStateMachines.SetupTx(_targetAvatar, avrcParameters, names);

            InstallMenu();
        }

        private static GameObject CreateRoot(GameObject avatar)
        {
            Transform rootTransform = avatar.transform.Find("AVRC");
            GameObject root;
            if (rootTransform != null)
            {
                root = rootTransform.gameObject;
            } else {
                root = new GameObject
                {
                    transform =
                    {
                        parent = avatar.transform,
                        localPosition = Vector3.zero,
                        localRotation = Quaternion.identity
                    },
                    name = "AVRC"
                };
                Undo.RegisterCreatedObjectUndo(root, "AVRC setup");
            }

            return root;
        }

        private bool IsReadyToInstall()
        {
            bool ok = true;

            ok = ok && Precheck(L.INST_ERR_NO_PARAMS, _params != null);
            ok = ok && Precheck(L.INST_NO_PREFIX, _params.prefix != null && !_params.prefix.Equals(""));
            ok = ok && Precheck(L.INST_NO_AVATAR, _targetAvatar != null);
            ok = ok && Precheck(L.INST_NO_FX, AvrcAnimatorUtils.FindFxLayer(_targetAvatar) != null);
            ok = ok && Precheck(L.INST_MENU_FULL, !IsTargetMenuFull());
            ok = ok && Precheck($"Duplicate parameter name [{duplicateName}]", duplicateName == null);

            return ok;
        }
        
        private bool IsTargetMenuFull()
        {
            if (_installMenu == null) return false;

            return _installMenu.controls.Count >= VRCExpressionsMenu.MAX_CONTROLS;
        }

        private bool Precheck(string message, bool ok)
        {
            if (ok) return ok;
            
            EditorGUILayout.HelpBox(message, MessageType.Error);

            return false;
        }

        private void InstallMenu()
        {
            if (_installMenu == null) return;
            Undo.RecordObject(_installMenu, "AVRC: Add submenu reference");

            MenuReference menuRef = CreateInstance<MenuReference>();
            var path = AssetDatabase.GetAssetPath(_params);
            var prefix = path.Substring(0, path.LastIndexOf('.'));
            var suffix = path.Substring(path.LastIndexOf('.'));
            var extractPath = prefix + "_AVRC_EXTRACTED" + suffix;

            var rootTargetMenu = AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>(extractPath);
            if (rootTargetMenu == null)
            {
                rootTargetMenu = CreateInstance<VRCExpressionsMenu>();
                AssetDatabase.CreateAsset(rootTargetMenu, extractPath);
            }

            MenuCloner cloner = new MenuCloner(
                new SerializedObject(menuRef).FindProperty(nameof(MenuReference.menu)),
                rootTargetMenu
            );
            
            menuRef.menu = rootTargetMenu;
            cloner.SyncMenus(_params.embeddedExpressionsMenu);

            if (_installMenu.controls.All(c => c.subMenu != rootTargetMenu))
            {
                _installMenu.controls.Add(new VRCExpressionsMenu.Control()
                {
                    name = _params.name,
                    type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                    subMenu = rootTargetMenu
                });
            }

            EditorUtility.SetDirty(_installMenu);
        }
        
        #region Remap panel

        [Serializable]
        private struct RemapEntry
        {
            public string paramName;
            public string mappedName;
        }

        [Serializable]
        private class RemapEntryContainer : ScriptableObject
        {
            public List<RemapEntry> _entries = new List<RemapEntry>();
        }

        private RemapEntryContainer _container;
        private ReorderableList _remapList;
        private AvrcNames _cachedNames;
        bool _foldout_remap_panel = false;
        private SerializedObject _remapObject;
        private SerializedProperty _remapProp;
        private string duplicateName = null;

        AvrcNames ApplyNameOverrides()
        {
            duplicateName = null;
            if (_params == null)
            {
                _cachedNames = null;
                return null;
            }
            _cachedNames = new AvrcNames(_params);
            foreach (var entry in _container._entries)
            {
                if (!String.IsNullOrWhiteSpace(entry.mappedName))
                {
                    _cachedNames.ParameterMap[entry.paramName] = entry.mappedName;
                }
            }

            HashSet<string> dupeCheck = new HashSet<string>();
            foreach (var value in _cachedNames.ParameterMap.Values)
            {
                if (!dupeCheck.Add(value))
                {
                    duplicateName = value;
                }
            }

            return _cachedNames;
        }
        
        void InitRemapPanel()
        {
            _container = CreateInstance<RemapEntryContainer>();
            _remapList = null;

            if (_params == null) return;

            ApplyNameOverrides();
            
            foreach (var k in _cachedNames.ParameterMap.Keys)
            {
                _container._entries.Add(new RemapEntry
                {
                    paramName = k,
                    mappedName = ""
                });
            }

            _remapObject = new SerializedObject(_container);
            _remapProp = _remapObject.FindProperty(nameof(RemapEntryContainer._entries));
            _remapList = new ReorderableList(_remapObject, _remapProp, false, false, false, false)
            {
                headerHeight = 0,
                drawElementCallback = OnDrawListElement,
                elementHeight = 4 + EditorGUIUtility.singleLineHeight
            };
        }

        private Single labelWidth;

        private string DefaultNameMapping(RemapEntry entry)
        {
            return _role == AvrcSavedState.Role.TX ? $"AVRC_{_params.prefix}_{entry.paramName}" : entry.paramName;
        }

        private void OnDrawListElement(Rect rect, int index, bool isActive, bool isFocused)
        {

            Rect labelRect = new Rect()
            {
                width = labelWidth + 10,
                height = rect.height,
                x = rect.x,
                y = rect.y
            };
            GUI.Label(labelRect, new GUIContent(_container._entries[index].paramName), GUI.skin.label);
            rect.x += labelRect.width;
            rect.width -= labelRect.width;

            var prop = _remapProp.GetArrayElementAtIndex(index).FindPropertyRelative(nameof(RemapEntry.mappedName));
            EditorGUI.PropertyField(rect, prop, GUIContent.none);

            if (prop.stringValue.Equals(""))
            {
                GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Italic,
                    normal = { textColor = Color.gray}
                };
                EditorGUI.LabelField(
                    rect,
                    DefaultNameMapping(_container._entries[index]),
                    labelStyle
                );
            }
        }

        void DrawRemapPanel()
        {
            if (_remapList == null) return;
            
            // Verify that the remap list is up to date
            if (_container._entries.Count != _params.avrcParams.Count)
            {
                InitRemapPanel();
            }
            else
            {
                // O(n^2) but we'll probably only have a handful of entries... probably...
                foreach (var param in _params.avrcParams)
                {
                    if (_container._entries.All(e => e.paramName != param.name))
                    {
                        InitRemapPanel();
                        break;
                    }
                }
            }
            
            _foldout_remap_panel = EditorGUILayout.Foldout(_foldout_remap_panel, "Remap parameter names");

            // Compute label width
            labelWidth = new GUIStyle(GUI.skin.label).CalcSize(new GUIContent("Placeholder")).x;

            foreach (var p in _container._entries)
            {
                labelWidth = Mathf.Max(labelWidth, new GUIStyle(GUI.skin.label).CalcSize(new GUIContent(p.paramName)).x);
            }

            EditorGUILayout.HelpBox(
                "In this section you can adjust the name of the remote-controlled parameters as they are applied to your avatar.\n" +
                "This is recommended for transmitters to avoid clashing with controls for your own avatar.\n",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            
            Rect rect = GUILayoutUtility.GetRect(100, _remapList.GetHeight(), new GUIStyle());
            _remapList.DoList(rect);

            if (EditorGUI.EndChangeCheck())
            {
                _remapObject.ApplyModifiedPropertiesWithoutUndo();
                ApplyNameOverrides();
            }
        }
    
        #endregion
        
    }
}