﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Animations;
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
        private VRCExpressionsMenu _installMenu;
        private AvrcParameters _params;
        private VRCAvatarDescriptor _targetAvatar;
        private Vector2 scrollPos = Vector2.zero;

        private bool showDetails = false;

        private Localizations L => Localizations.Inst;

        private void OnEnable()
        {
            InitSavedState();
        }

        private void OnGUI()
        {
            scrollPos = GUILayout.BeginScrollView(scrollPos);
            Localizations.SwitchLanguageButton();

            EditorGUI.BeginChangeCheck();
            _params = EditorGUILayout.ObjectField(
                L.INST_PARAMS, _params, typeof(AvrcParameters), allowSceneObjects: false
            ) as AvrcParameters;
            _targetAvatar = EditorGUILayout.ObjectField(
                L.INST_AVATAR, _targetAvatar, typeof(VRCAvatarDescriptor), allowSceneObjects: true
            ) as VRCAvatarDescriptor;

            if (EditorGUI.EndChangeCheck())
            {
                _installMenu = null;
                InitSavedState();
            }

            // ReSharper disable once HeapView.BoxingAllocation
            var roleProp = _bindingConfigSO.FindProperty(nameof(_bindingConfig.role));
            roleProp.enumValueIndex = EditorGUILayout.Popup("Role", (int) roleProp.enumValueIndex,
                new string[] {"", "TX", "RX"}
            );

            if (roleProp.enumValueIndex != (int) AvrcBindingConfiguration.Role.RX)
            {
                using (new EditorGUI.DisabledGroupScope(_params == null ||
                                                        _params.embeddedExpressionsMenu == null &&
                                                        _params.sourceExpressionMenu == null))
                {
                    _installMenu = EditorGUILayout.ObjectField(
                        L.INST_MENU, _installMenu, typeof(VRCExpressionsMenu), allowSceneObjects: false
                    ) as VRCExpressionsMenu;
                }
            }

            showDetails = EditorGUILayout.Foldout(showDetails, "Advanced settings");
            if (showDetails)
            {
                using (new EditorGUI.DisabledGroupScope(_bindingConfig == null))
                {
                    if (_bindingConfig == null)
                    {
                        // placeholder
                        EditorGUILayout.TextField("Timeout (seconds)", "");
                    }
                    else
                    {
                        var timeoutProp = _bindingConfigSO.FindProperty(nameof(_bindingConfig.timeoutSeconds));
                        EditorGUILayout.PropertyField(
                            timeoutProp,
                            new GUIContent("Timeout (seconds)")
                        );
                        if (timeoutProp.floatValue < 1.0f) timeoutProp.floatValue = 1.0f;
                    }
                }
            }

            DrawRemapPanel();

            var prechecks = IsReadyToInstall();

            using (new EditorGUI.DisabledGroupScope(!prechecks))
            {
                if (GUILayout.Button("Install"))
                {
                    DoInstall();
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

            GUILayout.EndScrollView();
        }

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

        private void DoInstall()
        {
            var avrcParameters = _params;
            if (avrcParameters == null) return;

            var root = CreateRoot(_targetAvatar.gameObject);

            _bindingConfigSO.ApplyModifiedPropertiesWithoutUndo();
            var names = new AvrcNames(_bindingConfig);
            var objects = new AvrcObjects(avrcParameters, names);

            _bindingConfig.role = _bindingConfig.role;
            _bindingConfig.parameters = _params;

            if (_bindingConfig.role == AvrcBindingConfiguration.Role.RX)
            {
                objects.buildReceiverBase(root, names.Prefix);
                AvrcRxStateMachines.SetupRx(_targetAvatar, _bindingConfig);
            }
            else
            {
                objects.buildTransmitterBase(root, names.Prefix);
                AvrcTxStateMachines.SetupTx(_targetAvatar, _bindingConfig);
                InstallMenu();
            }

            AvrcStateSaver.SaveState(_cachedNames, _targetAvatar, _bindingConfig);
        }

        private static GameObject CreateRoot(GameObject avatar)
        {
            Transform rootTransform = avatar.transform.Find("AVRC");
            GameObject root;
            if (rootTransform != null)
            {
                root = rootTransform.gameObject;
            }
            else
            {
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

            if (root.GetComponent<ScaleConstraint>() == null)
            {
                var constraint = Undo.AddComponent<ScaleConstraint>(root);
                constraint.AddSource(new ConstraintSource
                {
                    weight = 1,
                    sourceTransform = AvrcAssets.Origin().transform
                });
                constraint.locked = true;
                constraint.constraintActive = true;
            }

            return root;
        }

        private bool IsReadyToInstall()
        {
            bool ok = true;

            if (_bindingConfigSO == null || _bindingConfigSO.targetObject == null) InitSavedState();

            _bindingConfigSO.ApplyModifiedPropertiesWithoutUndo();

            ok = ok && Precheck("Role is not set", _bindingConfig.role != AvrcBindingConfiguration.Role.Init);
            ok = ok && Precheck(L.INST_ERR_NO_PARAMS, _params != null);
            ok = ok && Precheck(L.INST_NO_PREFIX, _params.prefix != null && !_params.prefix.Equals(""));
            ok = ok && Precheck(L.INST_NO_AVATAR, _targetAvatar != null);
            ok = ok && Precheck(L.INST_NO_FX, AvrcAnimatorUtils.FindFxLayer(_targetAvatar) != null);
            ok = ok && Precheck(L.INST_MENU_FULL, !IsTargetMenuFull());
            ok = ok && Precheck($"Duplicate parameter name [{duplicateName}]", duplicateName == null);
            if (_bindingConfig.role == AvrcBindingConfiguration.Role.RX)
            {
                ok = ok && Precheck($"Invalid timeout value {_bindingConfig.timeoutSeconds}",
                    _bindingConfig.timeoutSeconds > 1.0f);
            }

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

            AvrcNames names = new AvrcNames(_bindingConfig);

            MenuCloner cloner = new MenuCloner(
                new SerializedObject(menuRef).FindProperty(nameof(MenuReference.menu)),
                rootTargetMenu,
                names.ParameterMap
            );

            menuRef.menu = rootTargetMenu;
            cloner.SyncMenus(_params.sourceExpressionMenu != null
                ? _params.sourceExpressionMenu
                : _params.embeddedExpressionsMenu);

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

        private AvrcBindingConfiguration _bindingConfig;
        private ReorderableList _remapList;
        private AvrcNames _cachedNames;
        private SerializedObject _bindingConfigSO;
        private SerializedProperty _remapProp;
        private string duplicateName = null;

        private AvrcNames SyncNames()
        {
            _bindingConfigSO.ApplyModifiedPropertiesWithoutUndo();
            duplicateName = null;
            if (_params == null)
            {
                _cachedNames = null;
                return null;
            }

            var madeChanges = false;

            _cachedNames = new AvrcNames(_params);
            var boundParams = new HashSet<string>();
            var specParams = new HashSet<string>();
            foreach (var specParam in _params.avrcParams)
            {
                specParams.Add(specParam.name);
            }

            var initialParamCount = _bindingConfig.parameterMappings.Count;
            _bindingConfig.parameterMappings = _bindingConfig.parameterMappings
                .Where(p => specParams.Contains(p.avrcParameterName)).ToList();
            if (initialParamCount != _bindingConfig.parameterMappings.Count) madeChanges = true;
            foreach (var alreadyMapped in _bindingConfig.parameterMappings)
            {
                specParams.Remove(alreadyMapped.avrcParameterName);

                var mappedName = DefaultNameMapping(alreadyMapped);
                if (!String.IsNullOrWhiteSpace(alreadyMapped.remappedParameterName))
                {
                    mappedName = alreadyMapped.remappedParameterName;
                }

                if (!boundParams.Add(mappedName))
                {
                    duplicateName = mappedName;
                }
            }

            foreach (var newParam in specParams)
            {
                _bindingConfig.parameterMappings.Add(new ParameterMapping()
                {
                    avrcParameterName = newParam,
                    remappedParameterName = ""
                });
                madeChanges = true;
            }

            _bindingConfig.parameterMappings.Sort(
                (a, b) => String.Compare(a.avrcParameterName, b.avrcParameterName, StringComparison.CurrentCulture)
            );

            if (madeChanges) InitBindingList();

            return _cachedNames;
        }

        void InitSavedState()
        {
            _remapList = null;

            if (_params == null || _targetAvatar == null)
            {
                _bindingConfig = CreateInstance<AvrcBindingConfiguration>();
                _bindingConfigSO = new SerializedObject(_bindingConfig);

                return;
            }

            _cachedNames = new AvrcNames(_params);
            _bindingConfig = AvrcStateSaver.LoadState(_cachedNames, _targetAvatar);

            InitBindingList();
        }

        private void InitBindingList()
        {
            _bindingConfigSO = new SerializedObject(_bindingConfig);

            _remapProp = _bindingConfigSO.FindProperty(nameof(AvrcBindingConfiguration.parameterMappings));

            _remapList = new ReorderableList(_bindingConfigSO, _remapProp, false, false, false, false)
            {
                headerHeight = 0,
                drawElementCallback = OnDrawListElement,
                elementHeightCallback = elem =>
                {
                    var lines = _bindingConfig.role == AvrcBindingConfiguration.Role.RX ? 2 : 1;
                    return (4 + EditorGUIUtility.singleLineHeight + 4) * lines - 4;
                }
            };
        }

        private Single labelWidth;

        private string DefaultNameMapping(ParameterMapping entry)
        {
            return _bindingConfig.role == AvrcBindingConfiguration.Role.TX
                ? $"AVRC_{_params.prefix}_{entry.avrcParameterName}"
                : entry.avrcParameterName;
        }

        private void OnDrawListElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var initial = rect;
            rect.height = EditorGUIUtility.singleLineHeight;

            Rect labelRect = new Rect()
            {
                width = labelWidth + 10,
                height = rect.height,
                x = rect.x,
                y = rect.y
            };
            GUI.Label(labelRect, new GUIContent(_bindingConfig.parameterMappings[index].avrcParameterName),
                GUI.skin.label);
            rect.x += labelRect.width;
            rect.width -= labelRect.width;

            var element = _remapProp.GetArrayElementAtIndex(index);
            var prop = element.FindPropertyRelative(nameof(ParameterMapping.remappedParameterName));
            EditorGUI.PropertyField(rect, prop, GUIContent.none);

            if (prop.stringValue.Equals(""))
            {
                GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Italic,
                    normal = {textColor = Color.gray}
                };
                EditorGUI.LabelField(
                    rect,
                    DefaultNameMapping(_bindingConfig.parameterMappings[index]),
                    labelStyle
                );
            }

            if (_bindingConfigSO.FindProperty(nameof(_bindingConfig.role)).enumValueIndex ==
                (int) AvrcBindingConfiguration.Role.RX)
            {
                rect = initial;
                rect.y += EditorGUIUtility.singleLineHeight + 4;
                rect.height -= EditorGUIUtility.singleLineHeight + 4 + 4;
                rect.x += labelRect.width;
                rect.width -= labelRect.width;

                var noSignalModeProp = element.FindPropertyRelative(nameof(ParameterMapping.noSignalMode));
                EditorGUI.PropertyField(AvrcUI.AdvanceRect(ref rect, 80, padAfter: 10), noSignalModeProp,
                    GUIContent.none);

                switch ((NoSignalMode) noSignalModeProp.enumValueIndex)
                {
                    case NoSignalMode.Hold:
                        break;
                    case NoSignalMode.Reset:
                    {
                        var defaultValProp = element.FindPropertyRelative(nameof(ParameterMapping.defaultValue));
                        // TODO: Check property type
                        defaultValProp.intValue = EditorGUI.IntField(AvrcUI.AdvanceRect(ref rect, 40), GUIContent.none,
                            defaultValProp.intValue);
                        break;
                    }
                    case NoSignalMode.Forward:
                    {
                        var forwardProp = element.FindPropertyRelative(nameof(ParameterMapping.forwardParameter));
                        // TODO: Select from known properties
                        forwardProp.stringValue = EditorGUI.TextField(AvrcUI.AdvanceRect(ref rect, 120, padAfter: 10),
                            GUIContent.none, forwardProp.stringValue);
                        break;
                    }
                }
            }
        }

        void DrawRemapPanel()
        {
            if (_remapList == null) return;

            // Verify that the remap list is up to date
            SyncNames();

            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Remap parameter names");

            // Compute label width
            labelWidth = new GUIStyle(GUI.skin.label).CalcSize(new GUIContent("Placeholder")).x;

            foreach (var p in _bindingConfig.parameterMappings)
            {
                labelWidth = Mathf.Max(labelWidth,
                    new GUIStyle(GUI.skin.label).CalcSize(new GUIContent(p.avrcParameterName)).x);
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
                _bindingConfigSO.ApplyModifiedPropertiesWithoutUndo();
                SyncNames();
            }
        }

        #endregion
    }
}