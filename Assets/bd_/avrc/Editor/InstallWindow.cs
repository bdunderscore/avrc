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
        private VRCExpressionsMenu _installMenu;
        private AvrcLinkSpec _params;
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
            if (_bindingConfigSO == null || _bindingConfig == null) InitSavedState();

            scrollPos = GUILayout.BeginScrollView(scrollPos);
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
            }

            // ReSharper disable once HeapView.BoxingAllocation
            var roleProp = _bindingConfigSO.FindProperty(nameof(_bindingConfig.role));
            roleProp.enumValueIndex = EditorGUILayout.Popup("Role", (int) roleProp.enumValueIndex,
                new string[] {"", "TX", "RX"}
            );

            if (roleProp.enumValueIndex != (int) Role.RX)
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

            var writeDefaultsProp = _bindingConfigSO.FindProperty(nameof(_bindingConfig.writeDefaults));
            var writeDefaults = writeDefaultsProp.enumValueIndex == (int) WriteDefaultsState.YesWriteDefaults;
            EditorGUI.BeginChangeCheck();
            writeDefaults = EditorGUILayout.Toggle("Write defaults", writeDefaults);
            if (EditorGUI.EndChangeCheck())
            {
                var newEnumValue = writeDefaults
                    ? WriteDefaultsState.YesWriteDefaults
                    : WriteDefaultsState.NoWriteDefaults;
                _bindingConfigSO.FindProperty(nameof(_bindingConfig.writeDefaults)).enumValueIndex = (int) newEnumValue;
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
                        EditorGUILayout.TextField("Layer name", "");
                    }
                    else
                    {
                        var timeoutProp = _bindingConfigSO.FindProperty(nameof(_bindingConfig.timeoutSeconds));
                        EditorGUILayout.PropertyField(
                            timeoutProp,
                            new GUIContent("Timeout (seconds)")
                        );
                        if (timeoutProp.floatValue < 1.0f) timeoutProp.floatValue = 1.0f;
                        EditorGUILayout.PropertyField(
                            _bindingConfigSO.FindProperty(nameof(_bindingConfig.layerName)),
                            new GUIContent("Layer name")
                        );
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
                    _bindingConfigSO.ApplyModifiedPropertiesWithoutUndo();
                    AvrcUninstall.RemoveAvrcConfiguration(_targetAvatar, _bindingConfig);
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

            AvrcStateSaver.SaveState(_targetAvatar, _bindingConfig);
        }

        private bool IsReadyToInstall()
        {
            bool ok = true;

            if (_bindingConfigSO == null || _bindingConfigSO.targetObject == null) InitSavedState();

            _bindingConfigSO.ApplyModifiedPropertiesWithoutUndo();

            ok = ok && Precheck("Role is not set", _bindingConfig.role != Role.Init);
            ok = ok && Precheck(L.INST_ERR_NO_PARAMS, _params != null);
            ok = ok && Precheck(L.INST_NO_AVATAR, _targetAvatar != null);
            ok = ok && Precheck(L.INST_NO_FX, AvrcAnimatorUtils.FindFxLayer(_targetAvatar) != null);
            ok = ok && Precheck(L.INST_MENU_FULL, !IsTargetMenuFull());
            ok = ok && Precheck($"Duplicate parameter name [{duplicateName}]", duplicateName == null);
            if (_bindingConfig.role == Role.RX)
            {
                ok = ok && Precheck($"Invalid timeout value {_bindingConfig.timeoutSeconds}",
                    _bindingConfig.timeoutSeconds > 1.0f);
            }

            var avatarWDState = AvrcAnimatorUtils.GetWriteDefaultsState(_targetAvatar);
            if (avatarWDState == WriteDefaultsState.Mixed)
                EditorGUILayout.HelpBox(
                    "Mixed Write Defaults configuration found on your avatar. This may cause problems.",
                    MessageType.Warning);
            else if (_bindingConfig.writeDefaults != WriteDefaultsState.Mixed &&
                     avatarWDState != _bindingConfig.writeDefaults)
                EditorGUILayout.HelpBox(
                    "Write Defaults configuration does not match existing animators on your avatar. This may cause problems.",
                    MessageType.Warning);

            if (_targetAvatar != null && _cachedNames != null)
            {
                if (_targetAvatar.expressionParameters == null)
                {
                    Precheck("No expression parameters found on avatar", false);
                    return false;
                }

                var syncedParams = _targetAvatar.expressionParameters.parameters.Select(p => p.name)
                    .ToImmutableHashSet();

                foreach (var param in _bindingConfig.signalMappings)
                    if (param.isSecret)
                    {
                        var paramName = _cachedNames.SignalMap[param.avrcSignalName];
                        if (param.remappedParameterName != null) paramName = param.remappedParameterName;
                        if (syncedParams.Contains(paramName))
                            ok = Precheck($"Parameter [{paramName}] should not be synced, as it is secret.", false);
                    }
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
                names.SignalMap
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

            AddParameters(rootTargetMenu);

            EditorUtility.SetDirty(_installMenu);
        }

        private void AddParameters(VRCExpressionsMenu rootTargetMenu)
        {
            var paramToType =
                new Dictionary<string, AvrcSignalType>();
            foreach (var param in _params.signals)
                if (_cachedNames.SignalMap.ContainsKey(param.name))
                {
                    var mapped = _cachedNames.SignalMap[param.name];

                    paramToType[mapped] = param.type;
                }

            var pendingMenus = new Queue<VRCExpressionsMenu>();
            var seen = new HashSet<long>();
            var menuParams = new HashSet<string>();

            long localId;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(rootTargetMenu, out _, out localId);
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
                            menuParams.Add(elem.parameter.name);
                            break;
                        case VRCExpressionsMenu.Control.ControlType.SubMenu:
                            menuParams.Add(elem.parameter.name);
                            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(elem.subMenu, out _, out localId);
                            if (!seen.Contains(localId))
                            {
                                seen.Add(localId);
                                pendingMenus.Enqueue(elem.subMenu);
                            }

                            break;
                        default:
                            continue;
                    }

                    filteredControls.Add(elem);
                }

                menu.controls = filteredControls;
            }

            var expParams = new List<VRCExpressionParameters.Parameter>(_targetAvatar.expressionParameters.parameters);
            var expParamsAlreadyPresent = new HashSet<string>(expParams.Select(p => p.name));

            foreach (var mappedParam in menuParams)
            {
                if (!paramToType.ContainsKey(mappedParam) || expParamsAlreadyPresent.Contains(mappedParam)) continue;

                expParams.Add(new VRCExpressionParameters.Parameter
                {
                    name = mappedParam,
                    defaultValue = 0,
                    saved = false,
                    valueType = paramToType[mappedParam] == AvrcSignalType.Bool
                        ? VRCExpressionParameters.ValueType.Bool
                        : VRCExpressionParameters.ValueType.Int
                });
                expParamsAlreadyPresent.Add(mappedParam);
            }

            _targetAvatar.expressionParameters.parameters = expParams.ToArray();
            EditorUtility.SetDirty(_targetAvatar.expressionParameters);
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
                    duplicateName = mappedName;
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
            _bindingConfig = AvrcStateSaver.LoadState(_params, _targetAvatar);

            if (_bindingConfig.writeDefaults == WriteDefaultsState.Mixed)
                _bindingConfig.writeDefaults = AvrcAnimatorUtils.GetWriteDefaultsState(_targetAvatar);

            InitBindingList();
        }

        private void InitBindingList()
        {
            _bindingConfigSO = new SerializedObject(_bindingConfig);

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

        private Single labelWidth;

        private string DefaultNameMapping(SignalMapping entry)
        {
            return _bindingConfig.role == Role.TX
                ? $"AVRC_{_params.name}_{entry.avrcSignalName}"
                : entry.avrcSignalName;
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
                secretProp.boolValue = EditorGUI.Toggle(
                    AvrcUI.AdvanceRect(ref rect, EditorGUIUtility.singleLineHeight),
                    GUIContent.none, secretProp.boolValue
                );
                AvrcUI.RenderLabel(ref rect, "Secret mode");
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

            foreach (var p in _bindingConfig.signalMappings)
            {
                labelWidth = Mathf.Max(labelWidth,
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