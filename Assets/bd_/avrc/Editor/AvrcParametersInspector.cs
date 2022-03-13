using System;
using System.Diagnostics.CodeAnalysis;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using static net.fushizen.avrc.AvrcParameters;
using Debug = System.Diagnostics.Debug;
using Random = UnityEngine.Random;

namespace net.fushizen.avrc
{
    [CustomEditor(typeof(AvrcParameters))]
    public class AvrcParametersInspector : Editor
    {
        private AvrcParametersGenerator _paramsGen;
        private ReorderableList _paramsList;
        private SerializedProperty _paramsProp;

        private Localizations L => Localizations.Inst;

        [SuppressMessage("ReSharper", "HeapView.DelegateAllocation")]
        private void OnEnable()
        {
            _paramsProp = serializedObject.FindProperty("avrcParams");

            _paramsList = new ReorderableList(serializedObject, _paramsProp, true, true, true, true)
            {
                drawHeaderCallback = OnDrawListHeader,
                drawElementCallback = OnDrawListElement,
                elementHeightCallback = OnElementHeight
            };

            _paramsGen = new AvrcParametersGenerator(target as AvrcParameters);
        }

        public override void OnInspectorGUI()
        {
            // ReSharper disable once LocalVariableHidesMember
            AvrcParameters target = this.target as AvrcParameters;

            Localizations.SwitchLanguageButton();

            if (GUILayout.Button(L.AP_INSTALL))
            {
                InstallWindow.DisplayWindow(target);
            }

            Debug.Assert(target != null, nameof(target) + " != null");

            if (target.baseOffset.sqrMagnitude < 1)
            {
                target.baseOffset = new Vector3(
                    Random.Range(10000, 20000),
                    Random.Range(10000, 20000),
                    0
                );
                EditorUtility.SetDirty(target);
                AssetDatabase.SaveAssets();
            }

            serializedObject.Update();

            var srcMenuProp = serializedObject.FindProperty(nameof(AvrcParameters.sourceExpressionMenu));
            var srcMenu = srcMenuProp.objectReferenceValue;
            EditorGUILayout.PropertyField(srcMenuProp, L.AP_SRC_MENU);

            if (srcMenu != srcMenuProp.objectReferenceValue && srcMenuProp.objectReferenceValue == null)
            {
                // Clear the destination menu property so we'll clean up the cloned assets.
                // MenuCloner will be run when this asset is saved to do the cleanup process.
                var destMenuProp = serializedObject.FindProperty(nameof(AvrcParameters.embeddedExpressionsMenu));
                destMenuProp.objectReferenceValue = null;
            }
            /*if (GUILayout.Button("Sync menus"))
            {
                MenuCloner.InitCloner(target)?.SyncMenus(target.sourceExpressionMenu);
            }*/

            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(AvrcParameters.prefix)), L.AP_PREFIX);
            EditorGUILayout.Separator();

            Rect rect = GUILayoutUtility.GetRect(100, _paramsList.GetHeight(), new GUIStyle());
            _paramsList.DoList(rect);

            serializedObject.ApplyModifiedProperties();

            _paramsGen.GenerateParametersUI();
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
            var tyName = GetEnumProp<AvrcParameterType>(nameof(AvrcParameter.type), elem);

            var hasRange = tyName == AvrcParameterType.Int;
            return hasRange;
        }

        private static bool ElementIsIntLike(SerializedProperty elem)
        {
            var tyName = GetEnumProp<AvrcParameterType>(nameof(AvrcParameter.type), elem);

            return tyName == AvrcParameterType.Int || tyName == AvrcParameterType.Bool;
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
                var mode = element.FindPropertyRelative(nameof(AvrcParameter.syncDirection));
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