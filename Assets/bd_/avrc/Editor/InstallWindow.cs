using System;
using System.Diagnostics.CodeAnalysis;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace net.fushizen.avrc
{
    public class InstallWindow : EditorWindow
    {
        private AvrcParameters _params;
        private VRCAvatarDescriptor _targetAvatar;
        private bool _installMenu;

        private SerializedProperty prop_params, prop_targetAvatar, prop_installMenu;
        
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public void EditorWindow()
        {
            titleContent = new GUIContent("AVRC Installer");
        }
        
        [MenuItem("Window/AVRC Installer Test")]
        internal static void DisplayWindow(AvrcParameters p = null)
        {
            var window = GetWindow<InstallWindow>(title: "AVRC Installer");

            if (p != null)
            {
                window._params = p;
            }
        }
        private void OnGUI()
        {
            if (prop_params == null)
            {
                var obj = new SerializedObject(this);
                prop_params = obj.FindProperty(nameof(_params));
                prop_targetAvatar = obj.FindProperty(nameof(_targetAvatar));
                prop_installMenu = obj.FindProperty(nameof(_installMenu));
            }

            _params = EditorGUILayout.ObjectField(
                "AVRC Params", _params, typeof(AvrcParameters), allowSceneObjects: false
            ) as AvrcParameters;
            _targetAvatar = EditorGUILayout.ObjectField(
                "Target avatar", _targetAvatar, typeof(VRCAvatarDescriptor), allowSceneObjects: true
            ) as VRCAvatarDescriptor;
            _installMenu = EditorGUILayout.Toggle("Install expressions menu", _installMenu);

            var prechecks = IsReadyToInstall();

            using (new EditorGUI.DisabledGroupScope(!prechecks))
            {
                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Install transmitter"))
                    {
                        ApplyTransmitter();
                    }

                    if (GUILayout.Button("Install receiver"))
                    {
                        ApplyReceiver();
                    }
                }
            }

            if (GUILayout.Button("TODO Uninstall"))
            {
                
            }
            
            if (GUILayout.Button("TODO Uninstall ALL AVRC components"))
            {
                
            }
        }
        
        // ReSharper disable Unity.PerformanceAnalysis
        private void ApplyReceiver()
        {
            var avrcParameters = _params;
            if (avrcParameters == null) return;
            
            var root = CreateRoot(_targetAvatar.gameObject);
            
            AvrcObjects.buildReceiverBase(root, avrcParameters.Names.Prefix, avrcParameters);
            AvrcRxStateMachines.SetupRx(_targetAvatar, avrcParameters);
        }

        // ReSharper disable Unity.PerformanceAnalysis
        private void ApplyTransmitter()
        {
            var avrcParameters = _params;
            if (avrcParameters == null) return;
            
            var root = CreateRoot(_targetAvatar.gameObject);
            
            AvrcObjects.buildTransmitterBase(root, avrcParameters.Names.Prefix, avrcParameters);
            AvrcTxStateMachines.SetupTx(_targetAvatar, avrcParameters);
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

            ok = ok && Precheck("AVRC Parameters must be set", _params != null);
            ok = ok && Precheck("Prefix must not be empty", _params.prefix != null);
            ok = ok && Precheck("Avatar must be selected", _targetAvatar != null);

            return ok;
        }

        private bool Precheck(string message, bool ok)
        {
            if (ok) return ok;
            
            EditorGUILayout.HelpBox(message, MessageType.Error);

            return false;
        }
    }
}