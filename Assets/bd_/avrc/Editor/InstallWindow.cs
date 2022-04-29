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
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
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
        internal static HashSet<InstallWindow> OPEN_WINDOWS = new HashSet<InstallWindow>();
        private SerializedProperty _installMenu;
        private AvrcLinkSpec _params;
        private Vector2 _scrollPos = Vector2.zero;

        private bool _showDetails;
        private VRCAvatarDescriptor _targetAvatar;

        private static Localizations L => Localizations.Inst;

        private void OnEnable()
        {
            OPEN_WINDOWS.Add(this);
            InitSavedState();
        }

        private void OnDisable()
        {
            OPEN_WINDOWS.Remove(this);
        }

        private void OnGUI()
        {
            if (_bindingConfigSO == null || _bindingConfig == null) InitSavedState();

            _scrollPos = GUILayout.BeginScrollView(_scrollPos);
            Localizations.SwitchLanguageButton();

            EditorGUI.BeginChangeCheck();
            _params = EditorGUILayout.ObjectField(
                L.INST_PARAMS, _params, typeof(AvrcLinkSpec), false
            ) as AvrcLinkSpec;
            _targetAvatar = EditorGUILayout.ObjectField(
                L.INST_AVATAR, _targetAvatar, typeof(VRCAvatarDescriptor), allowSceneObjects: true
            ) as VRCAvatarDescriptor;

            if (EditorGUI.EndChangeCheck())
            {
                _installMenu = null;
                InitSavedState();

                var writeDefaultsProp_init = _bindingConfigSO.FindProperty(nameof(_bindingConfig.writeDefaults));
                if (writeDefaultsProp_init.enumValueIndex == (int) WriteDefaultsState.Mixed)
                    // Initialize
                    writeDefaultsProp_init.enumValueIndex =
                        (int) AvrcAnimatorUtils.GetWriteDefaultsState(_targetAvatar);
            }

            // ReSharper disable once HeapView.BoxingAllocation
            var roleProp = _bindingConfigSO.FindProperty(nameof(_bindingConfig.role));
            roleProp.enumValueIndex = EditorGUILayout.Popup(L.INST_ROLE, roleProp.enumValueIndex,
                L.PROP_ROLE_NAMES
            );

            if (roleProp.enumValueIndex != (int) Role.RX && _installMenu != null)
            {
                using (new EditorGUI.DisabledGroupScope(_params == null ||
                                                        _params.embeddedExpressionsMenu == null &&
                                                        _params.sourceExpressionMenu == null))
                {
                    _installMenu.objectReferenceValue = EditorGUILayout.ObjectField(
                        L.INST_MENU, _installMenu.objectReferenceValue, typeof(VRCExpressionsMenu), false
                    ) as VRCExpressionsMenu;
                }
            }

            var writeDefaultsProp = _bindingConfigSO.FindProperty(nameof(_bindingConfig.writeDefaults));

            var writeDefaults = writeDefaultsProp.enumValueIndex == (int) WriteDefaultsState.YesWriteDefaults;
            EditorGUI.BeginChangeCheck();
            writeDefaults = EditorGUILayout.Toggle(L.INST_WRITE_DEFAULTS, writeDefaults);
            if (EditorGUI.EndChangeCheck())
            {
                var newEnumValue = writeDefaults
                    ? WriteDefaultsState.YesWriteDefaults
                    : WriteDefaultsState.NoWriteDefaults;
                _bindingConfigSO.FindProperty(nameof(_bindingConfig.writeDefaults)).enumValueIndex = (int) newEnumValue;
            }

            _showDetails = EditorGUILayout.Foldout(_showDetails, L.INST_ADV_SETTINGS);
            if (_showDetails)
            {
                using (new EditorGUI.DisabledGroupScope(_bindingConfig == null))
                {
                    if (_bindingConfig == null)
                    {
                        // placeholder
                        EditorGUILayout.TextField(L.INST_TIMEOUT, "");
                        EditorGUILayout.TextField(L.INST_LAYER_NAME, "");
                    }
                    else
                    {
                        var timeoutProp = _bindingConfigSO.FindProperty(nameof(_bindingConfig.timeoutSeconds));
                        EditorGUILayout.PropertyField(
                            timeoutProp,
                            L.INST_TIMEOUT
                        );
                        if (timeoutProp.floatValue < 1.0f) timeoutProp.floatValue = 1.0f;
                        EditorGUILayout.PropertyField(
                            _bindingConfigSO.FindProperty(nameof(_bindingConfig.layerName)),
                            L.INST_LAYER_NAME
                        );
                    }
                }
            }

            DrawBindingPanel();

            var prechecks = IsReadyToInstall();

            using (new EditorGUI.DisabledGroupScope(!prechecks))
            {
                if (GUILayout.Button(L.INST_INSTALL))
                {
                    DoInstall();
                }
            }

            bool hasThisAvrc = (_params != null && _targetAvatar != null)
                               && AvrcUninstall.HasAvrcConfiguration(_targetAvatar, _params);
            var hasAnyAvrc = _targetAvatar != null && AvrcUninstall.HasAvrcConfiguration(_targetAvatar);

            using (new EditorGUI.DisabledGroupScope(!hasThisAvrc))
            {
                if (GUILayout.Button(L.INST_UNINSTALL))
                {
                    _bindingConfigSO.ApplyModifiedPropertiesWithoutUndo();
                    AvrcUninstall.RemoveAvrcConfiguration(_targetAvatar, _bindingConfig);
                }
            }

            using (new EditorGUI.DisabledGroupScope(!hasAnyAvrc))
            {
                if (GUILayout.Button(L.INST_UNINSTALL_ALL))
                {
                    AvrcUninstall.RemoveAvrcConfiguration(_targetAvatar);
                }
            }

            GUILayout.EndScrollView();
        }

        internal static void LinkUpdated(AvrcLinkSpec link)
        {
            foreach (var opened in OPEN_WINDOWS)
                if (opened._params == link)
                {
                    // Reinitialize to reflect any changes in the AvrcLinkSpec
                    opened.InitSavedState();
                    opened.Repaint();
                }
        }

        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public void EditorWindow()
        {
            titleContent = new GUIContent(L.INST_TITLE);
        }

        [MenuItem("Window/bd_/AVRC Installer")]
        internal static void DisplayWindow(AvrcLinkSpec p = null)
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

            _bindingConfigSO.ApplyModifiedPropertiesWithoutUndo();
            _bindingConfig.linkSpec = _params;
            if (_bindingConfig.writeDefaults == WriteDefaultsState.Mixed)
                // Collapse the wave function
                _bindingConfig.writeDefaults = WriteDefaultsState.NoWriteDefaults;

            if (string.IsNullOrWhiteSpace(_bindingConfig.layerName)) _bindingConfig.layerName = _params.name;
            EditorUtility.SetDirty(_bindingConfig);

            var oldConfig = AvrcStateSaver.LoadStateWithoutCloning(_params, _targetAvatar);
            if (oldConfig != null && oldConfig.layerName != _bindingConfig.layerName)
                // Clean up old layers and internal parameters
                AvrcUninstall.RemoveAvrcConfiguration(_targetAvatar, oldConfig);

            var names = new AvrcNames(_bindingConfig);
            new AvrcObjects(avrcParameters, names, _bindingConfig.role).CreateContacts(_targetAvatar.gameObject);

            if (_bindingConfig.role == Role.RX)
            {
                AvrcRxStateMachines.SetupRx(_targetAvatar, _bindingConfig);
            }
            else
            {
                AvrcTxStateMachines.SetupTx(_targetAvatar, _bindingConfig);
                InstallMenu();
            }

            AddParameters(names);

            AvrcStateSaver.SaveState(_targetAvatar, _bindingConfig);
        }

        private bool IsReadyToInstall()
        {
            if (_bindingConfigSO == null || _bindingConfigSO.targetObject == null) InitSavedState();

            _bindingConfigSO.ApplyModifiedPropertiesWithoutUndo();

            // Basic prechecks - we bail out early if these break

            var ok = Precheck(L.INST_ERR_NO_ROLE, _bindingConfig.role != Role.Init);
            ok = ok && Precheck(L.INST_ERR_NO_PARAMS, _params != null);
            ok = ok && Precheck(L.INST_ERR_NO_AVATAR, _targetAvatar != null);
            ok = ok && Precheck(L.INST_ERR_NO_FX, AvrcAnimatorUtils.FindFxLayer(_targetAvatar) != null);

            ok = ok && _cachedNames != null;
            if (!ok) return false;

            // For the following checks we allow multiple to be displayed
            ok &= Precheck(L.INST_MENU_FULL, !IsTargetMenuFull());
            ok &= Precheck(string.Format(L.INST_ERR_DUP_PARAM, _duplicateName), _duplicateName == null);
            if (_bindingConfig.role == Role.RX)
            {
                ok &= Precheck(string.Format(L.INST_ERR_BAD_TIMEOUT, _bindingConfig.timeoutSeconds),
                    _bindingConfig.timeoutSeconds > 1.0f);
            }

            var avatarWDState = AvrcAnimatorUtils.GetWriteDefaultsState(_targetAvatar);
            if (avatarWDState == WriteDefaultsState.Mixed)
                EditorGUILayout.HelpBox(
                    L.INST_ERR_MIXED_WRITE_DEFAULTS,
                    MessageType.Warning);
            else if (_bindingConfig.writeDefaults != WriteDefaultsState.Mixed &&
                     avatarWDState != _bindingConfig.writeDefaults)
                EditorGUILayout.HelpBox(
                    L.INST_ERR_WD_MISMATCH,
                    MessageType.Warning);

            if (_targetAvatar == null || _cachedNames == null) return false;

            if (_targetAvatar.expressionParameters == null)
            {
                Precheck(L.INST_ERR_NO_EXP_PARAMS, false);
                return false;
            }

            var syncedParams = _targetAvatar.expressionParameters.parameters.Select(p => p.name)
                .ToImmutableHashSet();

            foreach (var param in _bindingConfig.signalMappings)
            {
                var paramName = _cachedNames.SignalMap[param.avrcSignalName];
                if (param.remappedParameterName != null) paramName = param.remappedParameterName;
                if (param.isSecret && syncedParams.Contains(paramName))
                    ok &= Precheck(string.Format(L.INST_ERR_SYNCED_SECRET_PARAM, paramName), false);
            }

            return ok;
        }

        private bool IsTargetMenuFull()
        {
            var menu = _installMenu?.objectReferenceValue as VRCExpressionsMenu;

            if (menu == null) return false;

            return menu.controls.Count >= VRCExpressionsMenu.MAX_CONTROLS;
        }

        private bool Precheck(string message, bool ok)
        {
            if (ok) return true;

            EditorGUILayout.HelpBox(message, MessageType.Error);

            return false;
        }

        private void InstallMenu()
        {
            var menu = _installMenu?.objectReferenceValue as VRCExpressionsMenu;
            if (menu == null) return;
            Undo.RecordObject(menu, "AVRC: Add submenu reference");

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
                names.SignalMap
            );
            cloner.hideFlags = HideFlags.HideInInspector;
            cloner.objectNamePrefix = "ZZZ_AVRC_EXTRACTED_";

            menuRef.menu = rootTargetMenu;
            cloner.SyncMenus(_params.sourceExpressionMenu != null
                ? _params.sourceExpressionMenu
                : _params.embeddedExpressionsMenu);

            if (menu.controls.All(c => c.subMenu != rootTargetMenu))
            {
                menu.controls.Add(new VRCExpressionsMenu.Control
                {
                    name = _params.name,
                    type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                    subMenu = rootTargetMenu
                });
            }

            FilterControls(rootTargetMenu);

            EditorUtility.SetDirty(menu);
        }

        private void FilterControls(VRCExpressionsMenu rootTargetMenu)
        {
            var paramToType = new Dictionary<string, AvrcSignalType>();
            foreach (var param in _params.signals)
                if (_cachedNames.SignalMap.ContainsKey(param.name))
                {
                    var mapped = _cachedNames.SignalMap[param.name];

                    paramToType[mapped] = param.type;
                }

            var pendingMenus = new Queue<VRCExpressionsMenu>();
            var seen = new HashSet<long>();

            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(rootTargetMenu, out _, out long localId);
            seen.Add(localId);
            pendingMenus.Enqueue(rootTargetMenu);

            while (pendingMenus.Count > 0)
            {
                var menu = pendingMenus.Dequeue();

                var filteredControls = new List<VRCExpressionsMenu.Control>();

                foreach (var elem in menu.controls)
                {
                    if (!paramToType.ContainsKey(elem.parameter.name))
                    {
                        if (elem.type != VRCExpressionsMenu.Control.ControlType.SubMenu) continue;
                        elem.parameter.name = "";
                    }

                    // Subparameters are currently unsupported
                    elem.subParameters = Array.Empty<VRCExpressionsMenu.Control.Parameter>();

                    switch (elem.type)
                    {
                        case VRCExpressionsMenu.Control.ControlType.Toggle:
                        case VRCExpressionsMenu.Control.ControlType.Button:
                            break;
                        case VRCExpressionsMenu.Control.ControlType.SubMenu:
                            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(elem.subMenu, out _, out localId);
                            if (!seen.Contains(localId))
                            {
                                seen.Add(localId);
                                pendingMenus.Enqueue(elem.subMenu);
                            }

                            break;
                        default:
                            continue; // don't add other control types, we don't support floats yet
                    }

                    filteredControls.Add(elem);
                }

                menu.controls = filteredControls;
            }
        }

        private void AddParameters(AvrcNames names)
        {
            var expParams = new List<VRCExpressionParameters.Parameter>(_targetAvatar.expressionParameters.parameters);
            var expParamsAlreadyPresent = new HashSet<string>(expParams.Select(p => p.name));

            var paramDefs = _params.signals.Select(p => new KeyValuePair<string, AvrcSignal>(p.name, p))
                .ToImmutableDictionary();

            foreach (var binding in _bindingConfig.signalMappings)
            {
                if (binding.isSecret) continue;

                var mappedName = names.SignalMap[binding.avrcSignalName];
                if (expParamsAlreadyPresent.Contains(mappedName)) continue;

                expParams.Add(new VRCExpressionParameters.Parameter
                {
                    name = mappedName,
                    defaultValue = 0,
                    saved = false,
                    valueType = paramDefs[binding.avrcSignalName].type == AvrcSignalType.Bool
                        ? VRCExpressionParameters.ValueType.Bool
                        : VRCExpressionParameters.ValueType.Int
                });
                expParamsAlreadyPresent.Add(mappedName);
            }

            _targetAvatar.expressionParameters.parameters = expParams.ToArray();
            EditorUtility.SetDirty(_targetAvatar.expressionParameters);
        }

        #region Remap panel

        private AvrcBindingConfiguration _bindingConfig;
        private ReorderableList _remapList;
        private AvrcNames _cachedNames;

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private SerializedObject _bindingConfigSO;

        private SerializedProperty _remapProp;
        private string _duplicateName;
        private Dictionary<string, AvrcSignal> _signalMap;

        private void SyncNames()
        {
            _bindingConfigSO.ApplyModifiedPropertiesWithoutUndo();
            _duplicateName = null;
            if (_params == null)
            {
                _cachedNames = null;
                return;
            }

            var madeChanges = false;

            _cachedNames = new AvrcNames(_params, _bindingConfig.role, _bindingConfig.layerName);
            var boundParams = new HashSet<string>();
            var specParams = new HashSet<string>();
            foreach (var specParam in _params.signals)
            {
                specParams.Add(specParam.name);
            }

            var initialParamCount = _bindingConfig.signalMappings.Count;
            _bindingConfig.signalMappings = _bindingConfig.signalMappings
                .Where(p => specParams.Contains(p.avrcSignalName)).ToList();
            if (initialParamCount != _bindingConfig.signalMappings.Count) madeChanges = true;
            foreach (var alreadyMapped in _bindingConfig.signalMappings)
            {
                specParams.Remove(alreadyMapped.avrcSignalName);

                var mappedName = DefaultNameMapping(alreadyMapped);
                if (!String.IsNullOrWhiteSpace(alreadyMapped.remappedParameterName))
                {
                    mappedName = alreadyMapped.remappedParameterName;
                }

                if (!boundParams.Add(mappedName))
                {
                    _duplicateName = mappedName;
                }
            }

            foreach (var newParam in specParams)
            {
                _bindingConfig.signalMappings.Add(new SignalMapping
                {
                    avrcSignalName = newParam,
                    remappedParameterName = ""
                });
                madeChanges = true;
            }

            _bindingConfig.signalMappings.Sort(
                (a, b) => string.Compare(a.avrcSignalName, b.avrcSignalName, StringComparison.CurrentCulture)
            );

            if (madeChanges) InitBindingList();
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
            _signalMap = new Dictionary<string, AvrcSignal>();
            _bindingConfig = AvrcStateSaver.LoadState(_params, _targetAvatar);

            if (_bindingConfig.writeDefaults == WriteDefaultsState.Mixed)
                _bindingConfig.writeDefaults = AvrcAnimatorUtils.GetWriteDefaultsState(_targetAvatar);

            InitBindingList();

            _installMenu = _bindingConfigSO.FindProperty(nameof(AvrcBindingConfiguration.installTargetMenu));
        }

        private void InitBindingList()
        {
            _bindingConfigSO = new SerializedObject(_bindingConfig);
            foreach (var sig in _params.signals) _signalMap[sig.name] = sig;

            _remapProp = _bindingConfigSO.FindProperty(nameof(AvrcBindingConfiguration.signalMappings));

            _remapList = new ReorderableList(_bindingConfigSO, _remapProp, false, false, false, false)
            {
                headerHeight = 0,
                drawElementCallback = OnDrawListElement,
                elementHeightCallback = elem =>
                {
                    var lines = _bindingConfig.role == Role.RX ? 2 : 1;
                    return (4 + EditorGUIUtility.singleLineHeight + 4) * lines - 4;
                }
            };
        }

        private float _labelWidth;

        private string DefaultNameMapping(SignalMapping entry)
        {
            return _bindingConfig.role == Role.TX
                ? _cachedNames.SignalMap[entry.avrcSignalName]
                : entry.avrcSignalName;
        }

        private void OnDrawListElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var initial = rect;
            rect.height = EditorGUIUtility.singleLineHeight;

            Rect labelRect = new Rect()
            {
                width = _labelWidth + 10,
                height = rect.height,
                x = rect.x,
                y = rect.y
            };
            GUI.Label(labelRect, new GUIContent(_bindingConfig.signalMappings[index].avrcSignalName),
                GUI.skin.label);
            rect.x += labelRect.width;
            rect.width -= labelRect.width;

            var element = _remapProp.GetArrayElementAtIndex(index);
            var prop = element.FindPropertyRelative(nameof(SignalMapping.remappedParameterName));
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
                    DefaultNameMapping(_bindingConfig.signalMappings[index]),
                    labelStyle
                );
            }

            if (_bindingConfigSO.FindProperty(nameof(_bindingConfig.role)).enumValueIndex ==
                (int) Role.RX)
            {
                rect = initial;
                rect.y += EditorGUIUtility.singleLineHeight + 4;
                rect.height -= EditorGUIUtility.singleLineHeight + 4 + 4;
                rect.x += labelRect.width;
                rect.width -= labelRect.width;

                var noSignalModeProp = element.FindPropertyRelative(nameof(SignalMapping.noSignalMode));
                EditorGUI.PropertyField(AvrcUI.AdvanceRect(ref rect, 80, padAfter: 10), noSignalModeProp,
                    GUIContent.none);

                var container = AvrcUI.AdvanceRect(ref rect, 120, padAfter: 10);

                switch ((NoSignalMode) noSignalModeProp.enumValueIndex)
                {
                    case NoSignalMode.Hold:
                        break;
                    case NoSignalMode.Reset:
                    {
                        var defaultValProp = element.FindPropertyRelative(nameof(SignalMapping.defaultValue));
                        // TODO: Check property type
                        defaultValProp.intValue = EditorGUI.IntField(AvrcUI.AdvanceRect(ref container, 40),
                            GUIContent.none,
                            defaultValProp.intValue);
                        break;
                    }
                    case NoSignalMode.Forward:
                    {
                        var forwardProp = element.FindPropertyRelative(nameof(SignalMapping.forwardParameter));
                        // TODO: Select from known properties
                        forwardProp.stringValue =
                            EditorGUI.TextField(container, GUIContent.none, forwardProp.stringValue);
                        break;
                    }
                }

                var secretProp = element.FindPropertyRelative(nameof(SignalMapping.isSecret));
                var signalName = element.FindPropertyRelative(nameof(SignalMapping.avrcSignalName)).stringValue;
                if (_signalMap[signalName].syncDirection == SyncDirection.OneWay)
                {
                    secretProp.boolValue = EditorGUI.Toggle(
                        AvrcUI.AdvanceRect(ref rect, EditorGUIUtility.singleLineHeight),
                        GUIContent.none, secretProp.boolValue
                    );
                    AvrcUI.RenderLabel(ref rect, L.INST_SECRET_MODE);
                }
                else if (secretProp.boolValue)
                {
                    // TODO - show label?
                    secretProp.boolValue = false;
                }
            }
        }

        private void DrawBindingPanel()
        {
            if (_remapList == null) return;

            // Verify that the remap list is up to date
            SyncNames();

            EditorGUILayout.Separator();
            EditorGUILayout.LabelField(L.INST_PARAM_SETTINGS);

            // Compute label width
            _labelWidth = new GUIStyle(GUI.skin.label).CalcSize(new GUIContent("Placeholder")).x;

            foreach (var p in _bindingConfig.signalMappings)
            {
                _labelWidth = Mathf.Max(_labelWidth,
                    new GUIStyle(GUI.skin.label).CalcSize(new GUIContent(p.avrcSignalName)).x);
            }

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