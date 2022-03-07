using System;
using System.Diagnostics.CodeAnalysis;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using static net.fushizen.avrc.AvrcParameters;
using Debug = System.Diagnostics.Debug;
using Random = UnityEngine.Random;

namespace net.fushizen.avrc
{
    
    [CustomEditor(typeof(AvrcParameters))]
    public class AvrcParametersInspector : Editor
    {
        private ReorderableList _paramsList;
        private SerializedProperty _paramsProp;
        private AvrcParametersGenerator _paramsGen;

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
            
            if (GUILayout.Button("Install"))
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
            EditorGUILayout.PropertyField(srcMenuProp);
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
            
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(AvrcParameters.prefix)));
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

            return ( 4 + EditorGUIUtility.singleLineHeight + 4 ) * lines - 4;
        }

        private static bool ElementHasRangeProp(SerializedProperty elem)
        {
            var tyName = ElementType(elem);

            var hasRange = tyName == AvrcParameterType.Int || tyName == AvrcParameterType.BidiInt;
            return hasRange;
        }

        private static AvrcParameterType? ElementType(SerializedProperty elem)
        {
            var tyProp = elem.FindPropertyRelative("type");
            if (tyProp.enumValueIndex < 0 || tyProp.enumValueIndex >= tyProp.enumNames.Length)
            {
                return null;
            }

            AvrcParameterType ty;
            if (Enum.TryParse(
                    tyProp.enumNames[tyProp.enumValueIndex],
                    out ty
                ))
            {
                return ty;
            }
            else
            {
                return null;
            }
        }

        static Rect AdvanceRect(ref Rect rect, Single width, Single padBefore = 0, Single padAfter = 0)
        {
            var r = rect;
            r.width = width;
            r.x += padBefore;
            rect.x += width + padBefore + padAfter;

            return r;
        }

        static void RenderLabel(
            ref Rect rect,
            GUIContent content,
            GUIStyle style, 
            Single padBefore = 0,
            Single padAfter = 0)
        {
            if (style == null)
            {
                style = GUI.skin.label;
            }

            style = new GUIStyle(style)
            {
                alignment = TextAnchor.MiddleCenter
            };

            Vector2 size = style.CalcSize(content);
            var r = AdvanceRect(ref rect, size.x, padBefore, padAfter);
            GUI.Label(r, content, style);
        }

        static void RenderLabel(
            ref Rect rect,
            string content,
            GUIStyle style = null,
            Single padBefore = 0,
            Single padAfter = 0
        ) {
            RenderLabel(ref rect, new GUIContent(content), style, padBefore, padAfter);
        }

        private void OnDrawListHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, "Parameters");
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

            EditorGUI.PropertyField(
                AdvanceRect(ref rect, 100),
                element.FindPropertyRelative("type"),
                GUIContent.none
            );

            var propName = element.FindPropertyRelative("name");
            if (ElementType(element) != AvrcParameterType.IsLocal)
            {
                EditorGUI.PropertyField(
                    AdvanceRect(ref rect, 100),
                    propName,
                    GUIContent.none
                );                
            }
            else {
                rect.x += 100;
            }
            

            RenderLabel(ref rect, "RX parameter", padBefore: 20, padAfter: 10);

            var rxNameRect = AdvanceRect(ref rect, 100);
            var propRxName = element.FindPropertyRelative("rxName");
            EditorGUI.PropertyField(
                rxNameRect,
                propRxName,
                GUIContent.none
            );

            if (ElementType(element) == AvrcParameterType.IsLocal)
            {
                propName.stringValue = propRxName.stringValue;
            }

            if (element.FindPropertyRelative("rxName").stringValue.Equals(""))
            {
                GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Italic,
                    normal = { textColor = Color.gray}
                };
                EditorGUI.LabelField(
                    rxNameRect,
                    element.FindPropertyRelative("name").stringValue,
                    labelStyle
                );
            }

            if (ElementHasRangeProp(element))
            {
                var minVal = element.FindPropertyRelative("minVal");
                var maxVal = element.FindPropertyRelative("maxVal");

                rect.x = initialRect.x;
                rect.y += 4 + EditorGUIUtility.singleLineHeight;
                
                RenderLabel(ref rect, "Range", padAfter: 10);
                EditorGUI.PropertyField(AdvanceRect(ref rect, 30), minVal, GUIContent.none);
                RenderLabel(ref rect, "...");
                EditorGUI.PropertyField(AdvanceRect(ref rect, 30), maxVal, GUIContent.none);
            }
        }


    }
}