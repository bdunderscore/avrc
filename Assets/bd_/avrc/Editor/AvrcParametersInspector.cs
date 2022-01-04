using System;
using net.fushizen.avrc;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;
using Debug = System.Diagnostics.Debug;
using Random = UnityEngine.Random;

namespace net.fushizen.avrc
{
    
    [CustomEditor(typeof(AvrcParameters))]
    public class AvrcParametersInspector : UnityEditor.Editor
    {
        private VRCAvatarDescriptor fApplyToDescriptor;
        
        public override void OnInspectorGUI()
        {
            fApplyToDescriptor = EditorGUILayout.ObjectField("Avatar", fApplyToDescriptor, typeof(VRCAvatarDescriptor), true)
                as VRCAvatarDescriptor;
            
            AvrcParameters target = this.target as AvrcParameters;
            Debug.Assert(target != null, nameof(base.target) + " != null");
            
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

            using (new EditorGUI.DisabledScope(fApplyToDescriptor == null))
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
            
            base.OnInspectorGUI();
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
            
            var root = CreateRoot(fApplyToDescriptor.gameObject);
            
            AvrcObjects.buildReceiverBase(root, avrcParameters.Names.Prefix, avrcParameters);
            AvrcRxStateMachines.SetupRx(fApplyToDescriptor, avrcParameters);
        }

        // ReSharper disable Unity.PerformanceAnalysis
        private void ApplyTransmitter()
        {
            var avrcParameters = target as AvrcParameters;
            if (avrcParameters == null) return;
            
            var root = CreateRoot(fApplyToDescriptor.gameObject);
            
            AvrcObjects.buildTransmitterBase(root, avrcParameters.Names.Prefix, avrcParameters);
            AvrcTxStateMachines.SetupTx(fApplyToDescriptor, avrcParameters);
        }
    }
}