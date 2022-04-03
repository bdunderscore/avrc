using System;
using System.Diagnostics.CodeAnalysis;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
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
        private SerializedProperty _srcMenuProp;

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

        public override void OnInspectorGUI()
        {
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
                _baseOffsetProp.vector3Value = new Vector3(
                    Random.Range(-10, 10),
                    Random.Range(-100, 100),
                    Random.Range(-10, 10)
                );
                EditorUtility.SetDirty(target);
                AssetDatabase.SaveAssets();
            }

            var srcMenu = _srcMenuProp.objectReferenceValue;
            if (srcMenu == null && _embeddedMenuProp.objectReferenceValue != null)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.PropertyField(_embeddedMenuProp, L.AP_SRC_MENU);
                }
            }
            else
            {
                EditorGUILayout.PropertyField(_srcMenuProp, L.AP_SRC_MENU);

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
    }
}