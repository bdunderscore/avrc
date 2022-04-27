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
using System.Diagnostics.CodeAnalysis;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using Debug = System.Diagnostics.Debug;
using Random = UnityEngine.Random;

namespace net.fushizen.avrc
{
    [CustomEditor(typeof(AvrcLinkSpec))]
    internal class LinkSpecInspector : Editor
    {
        private SerializedProperty _baseOffsetProp;
        private SerializedProperty _embeddedMenuProp;
        private SerializedProperty _guidProp;
        private AvrcLinkSpecGenerator _paramsGen;
        private ReorderableList _paramsList;
        private SerializedProperty _paramsProp;
        private bool _showClearEmbeddedMenu;
        private SerializedProperty _srcMenuProp;

        private GUIStyle _wrappingLabelStyle;

        private Localizations L => Localizations.Inst;

        [SuppressMessage("ReSharper", "HeapView.DelegateAllocation")]
        private void OnEnable()
        {
            _paramsProp = serializedObject.FindProperty(nameof(AvrcLinkSpec.signals));
            _guidProp = serializedObject.FindProperty(nameof(AvrcLinkSpec.guid));
            _baseOffsetProp = serializedObject.FindProperty(nameof(AvrcLinkSpec.baseOffset));
            _srcMenuProp = serializedObject.FindProperty(nameof(AvrcLinkSpec.sourceExpressionMenu));
            _embeddedMenuProp = serializedObject.FindProperty(nameof(AvrcLinkSpec.embeddedExpressionsMenu));

            _paramsList = new ReorderableList(serializedObject, _paramsProp, true, true, true, true)
            {
                drawHeaderCallback = OnDrawListHeader,
                drawElementCallback = OnDrawListElement,
                elementHeightCallback = OnElementHeight
            };

            _paramsGen = new AvrcLinkSpecGenerator(target as AvrcLinkSpec);
        }

        private void InitStyles()
        {
            if (_wrappingLabelStyle == null)
                _wrappingLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    wordWrap = true
                };
        }

        public override void OnInspectorGUI()
        {
            InitStyles();

            EditorGUI.BeginChangeCheck();

            if (string.IsNullOrWhiteSpace(_guidProp.stringValue))
            {
                _guidProp.stringValue = GUID.Generate().ToString();
            }

            Localizations.SwitchLanguageButton();

            if (GUILayout.Button(L.AP_INSTALL))
            {
                InstallWindow.DisplayWindow(target as AvrcLinkSpec);
            }

            Debug.Assert(target != null, nameof(target) + " != null");

            if (_baseOffsetProp.vector3Value.sqrMagnitude < 1)
            {
                // We primarily rely on the contact tags to avoid interference, but we also set a position offset both
                // to avoid too many debug displays at the origin, and to reduce the number of collision tag tests that
                // need to occur by allowing the contact/collision broadphase to separately cluster contacts for
                // different AVRC channels.
                //
                // We do want to avoid making this too large, as it'll increase the effect of rotation on instantaneous
                // position offset and thus risk falsely detecting a loss of communication.
                //
                // In total, we add 
                _baseOffsetProp.vector3Value = new Vector3(
                    // We use increments of 5m as this is (slightly more than) the diameter of the signal contacts. 
                    Random.Range(-4, 4) * 5, // ~3 bits
                    // We use 8m here as we are a bit more worried about floating point error on the Y axis; thus,
                    // we try to keep the low few bits of the coordinate zero.
                    Random.Range(-1024, 1024) * 8, // 11 bits
                    Random.Range(-4, 4) * 5 // ~3 bits
                );
                EditorUtility.SetDirty(target);
                AssetDatabase.SaveAssets();
            }

            var srcMenu = _srcMenuProp.objectReferenceValue;
            if (srcMenu == null && _embeddedMenuProp.objectReferenceValue != null)
            {
                EditorGUILayout.LabelField(L.AP_HAS_EMBED, _wrappingLabelStyle);
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.PropertyField(_embeddedMenuProp, L.AP_SRC_MENU);
                }

                if (!_showClearEmbeddedMenu)
                {
                    _showClearEmbeddedMenu = GUILayout.Button(L.AP_CLEAR_EMBED_BUTTON);
                }
                else
                {
                    if (GUILayout.Button(L.AP_CLEAR_EMBED_CANCEL))
                    {
                        _showClearEmbeddedMenu = false;
                    }
                    else
                    {
                        EditorGUILayout.LabelField(L.AP_CLEAR_EMBED_WARN, _wrappingLabelStyle);
                        var oldBackground = GUI.backgroundColor;
                        GUI.backgroundColor = Color.red;
                        if (GUILayout.Button(L.AP_CLEAR_EMBED_CONFIRM /*, _clearEmbedConfirmStyle*/))
                            ClearEmbeddedMenu();

                        GUI.backgroundColor = oldBackground;
                    }
                }
            }
            else
            {
                _showClearEmbeddedMenu = false;

                EditorGUILayout.LabelField(L.AP_CAN_EMBED, _wrappingLabelStyle);

                EditorGUILayout.PropertyField(_srcMenuProp, L.AP_SRC_MENU);

                if (srcMenu != _srcMenuProp.objectReferenceValue &&
                    _srcMenuProp.objectReferenceValue != null &&
                    _srcMenuProp.objectReferenceValue.name.StartsWith("ZZZ_AVRC_EMBEDDED_"))
                    // Reject attempts to reference our embedded assets.
                    _srcMenuProp.objectReferenceValue = srcMenu;

                if (srcMenu != _srcMenuProp.objectReferenceValue &&
                    (srcMenu != null || _srcMenuProp.objectReferenceValue != null))
                {
                    // Clear the destination menu property so we'll clean up the cloned assets.
                    var destMenuProp = serializedObject.FindProperty(nameof(AvrcLinkSpec.embeddedExpressionsMenu));
                    destMenuProp.objectReferenceValue = null;

                    // Force a clone and save ASAP. This avoids issues where the user immediately copies out the menu asset
                    // before we run the on-save logic.
                    EditorApplication.delayCall += () =>
                    {
                        if (target != null)
                        {
                            serializedObject.ApplyModifiedProperties();
                            AvrcAssetProcessorCallbacks.initClones(target as AvrcLinkSpec);
                            AssetDatabase.SaveAssets();
                        }
                    };
                }
            }
            /*if (GUILayout.Button("Sync menus"))
            {
                MenuCloner.InitCloner(target)?.SyncMenus(target.sourceExpressionMenu);
            }*/

            //EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(AvrcParameters.prefix)), L.AP_PREFIX);
            EditorGUILayout.Separator();

            Rect rect = GUILayoutUtility.GetRect(100, _paramsList.GetHeight(), new GUIStyle());
            _paramsList.DoList(rect);

            serializedObject.ApplyModifiedProperties();

            _paramsGen.GenerateParametersUI();

            serializedObject.UpdateIfRequiredOrScript();

            if (EditorGUI.EndChangeCheck()) InstallWindow.LinkUpdated(target as AvrcLinkSpec);
        }

        private float OnElementHeight(int index)
        {
            int lines = 1;
            if (index < _paramsProp.arraySize && index >= 0)
            {
                var elem = _paramsProp.GetArrayElementAtIndex(index);
                if (elem != null)
                {
                    lines = ElementHasRangeProp(elem) ? 2 : 1;
                }
            }

            return (4 + EditorGUIUtility.singleLineHeight + 4) * lines - 4;
        }

        private static bool ElementHasRangeProp(SerializedProperty elem)
        {
            var tyName = GetEnumProp<AvrcSignalType>(nameof(AvrcSignal.type), elem);

            var hasRange = tyName == AvrcSignalType.Int;
            return hasRange;
        }

        private static bool ElementIsIntLike(SerializedProperty elem)
        {
            var tyName = GetEnumProp<AvrcSignalType>(nameof(AvrcSignal.type), elem);

            return tyName == AvrcSignalType.Int || tyName == AvrcSignalType.Bool;
        }

        private static T GetEnumProp<T>(string name, SerializedProperty elem) where T : Enum
        {
            var tyProp = elem.FindPropertyRelative(name);
            if (tyProp.enumValueIndex < 0 || tyProp.enumValueIndex >= tyProp.enumNames.Length)
            {
                return default;
            }

            var values = Enum.GetValues(typeof(T));
            if (tyProp.enumValueIndex < 0 || tyProp.enumValueIndex >= values.Length)
            {
                return default;
            }

            return (T) values.GetValue(tyProp.enumValueIndex);
        }

        private void OnDrawListHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, L.AP_PARAMETERS);
            rect.y += 4 + EditorGUIUtility.singleLineHeight;
        }

        private void OnDrawListElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            //UnityEngine.Debug.Log($"Element: {rect.x} {rect.y}");
            var initialRect = rect;
            SerializedProperty element = _paramsProp.GetArrayElementAtIndex(index);

            if (element == null) return;

            rect.y += 2;
            rect.height = EditorGUIUtility.singleLineHeight;

            EditorGUI.PropertyField(AvrcUI.AdvanceRect(ref rect, 60),
                element.FindPropertyRelative("type"),
                GUIContent.none
            );

            if (ElementIsIntLike(element))
            {
                var mode = element.FindPropertyRelative(nameof(AvrcSignal.syncDirection));
                EditorGUI.PropertyField(AvrcUI.AdvanceRect(ref rect, 80), mode, GUIContent.none);
            }

            var propName = element.FindPropertyRelative("name");
            EditorGUI.PropertyField(AvrcUI.AdvanceRect(ref rect, rect.width),
                propName,
                GUIContent.none
            );

            if (ElementHasRangeProp(element))
            {
                rect.x = initialRect.x;
                rect.y += 4 + EditorGUIUtility.singleLineHeight;

                var minVal = element.FindPropertyRelative("minVal");
                var maxVal = element.FindPropertyRelative("maxVal");

                AvrcUI.RenderLabel(ref rect, L.AP_RANGE, padAfter: 10);
                EditorGUI.PropertyField(AvrcUI.AdvanceRect(ref rect, 30), minVal, GUIContent.none);
                AvrcUI.RenderLabel(ref rect, "...");
                EditorGUI.PropertyField(AvrcUI.AdvanceRect(ref rect, 30), maxVal, GUIContent.none);
            }
        }

        private void ClearEmbeddedMenu()
        {
            _srcMenuProp.objectReferenceValue = null;
            _embeddedMenuProp.objectReferenceValue = null;

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(target)))
            {
                UnityEngine.Debug.Log(asset.GetType());
                if (asset is VRCExpressionsMenu menu)
                {
                    AssetDatabase.RemoveObjectFromAsset(menu);
                    DestroyImmediate(menu, true);
                }
            }
        }
    }
}