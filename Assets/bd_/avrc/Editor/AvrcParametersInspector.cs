using System;
using System.Diagnostics.CodeAnalysis;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using Debug = System.Diagnostics.Debug;
using Random = UnityEngine.Random;

namespace net.fushizen.avrc
{
    
    [CustomEditor(typeof(AvrcParameters))]
    public class AvrcParametersInspector : Editor
    {
        private VRCAvatarDescriptor _fApplyToDescriptor;
        private ReorderableList _paramsList;
        private SerializedProperty _paramsProp;

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
        }

        public override void OnInspectorGUI()
        {
            _fApplyToDescriptor = EditorGUILayout.ObjectField("Avatar", _fApplyToDescriptor, typeof(VRCAvatarDescriptor), true)
                as VRCAvatarDescriptor;
            
            // ReSharper disable once LocalVariableHidesMember
            AvrcParameters target = this.target as AvrcParameters;
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

            using (new EditorGUI.DisabledScope(_fApplyToDescriptor == null))
            {
                GUILayout.BeginHorizontal();

                if (GUILayout.Button("Apply Receiver"))
                {
                    ApplyReceiver();
                }

                if (GUILayout.Button("Apply Transmitter"))
                {
                    ApplyTransmitter();
                }
                
                GUILayout.EndHorizontal();
            }
            
            serializedObject.Update();
            
            EditorGUILayout.PropertyField(serializedObject.FindProperty("prefix"));
            EditorGUILayout.Separator();
       
            Rect rect = GUILayoutUtility.GetRect(100, _paramsList.GetHeight(), new GUIStyle());
            _paramsList.DoList(rect);

            serializedObject.ApplyModifiedProperties();
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
            var ty = elem.FindPropertyRelative("type");
            var tyName = ty.enumNames[ty.enumValueIndex];

            var hasRange = tyName.Equals("BidiInt") || tyName.Equals("Int");
            return hasRange;
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

            EditorGUI.PropertyField(
                AdvanceRect(ref rect, 100),
                element.FindPropertyRelative("name"),
                GUIContent.none
            );

            RenderLabel(ref rect, "RX parameter", padBefore: 20, padAfter: 10);

            var rxNameRect = AdvanceRect(ref rect, 100);
            EditorGUI.PropertyField(
                rxNameRect,
                element.FindPropertyRelative("rxName"),
                GUIContent.none
            );
            

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
        
        // ReSharper disable Unity.PerformanceAnalysis
        private void ApplyReceiver()
        {
            var avrcParameters = target as AvrcParameters;
            if (avrcParameters == null) return;
            
            var root = CreateRoot(_fApplyToDescriptor.gameObject);
            
            AvrcObjects.buildReceiverBase(root, avrcParameters.Names.Prefix, avrcParameters);
            AvrcRxStateMachines.SetupRx(_fApplyToDescriptor, avrcParameters);
        }

        // ReSharper disable Unity.PerformanceAnalysis
        private void ApplyTransmitter()
        {
            var avrcParameters = target as AvrcParameters;
            if (avrcParameters == null) return;
            
            var root = CreateRoot(_fApplyToDescriptor.gameObject);
            
            AvrcObjects.buildTransmitterBase(root, avrcParameters.Names.Prefix, avrcParameters);
            AvrcTxStateMachines.SetupTx(_fApplyToDescriptor, avrcParameters);
        }
    }
}