using System.Diagnostics.CodeAnalysis;
using System.Linq;
using UnityEditor;
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
        private AvrcParameters _params;
        private VRCAvatarDescriptor _targetAvatar;
        private VRCExpressionsMenu _installMenu;

        private SerializedProperty prop_params, prop_targetAvatar, prop_installMenu;
        
        private Localizations L => Localizations.Inst;
        
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
        private void OnGUI()
        {
            Localizations.SwitchLanguageButton();

            if (prop_params == null)
            {
                var obj = new SerializedObject(this);
                prop_params = obj.FindProperty(nameof(_params));
                prop_targetAvatar = obj.FindProperty(nameof(_targetAvatar));
                prop_installMenu = obj.FindProperty(nameof(_installMenu));
            }

            _params = EditorGUILayout.ObjectField(
                L.INST_PARAMS, _params, typeof(AvrcParameters), allowSceneObjects: false
            ) as AvrcParameters;
            var priorTargetAvatar = _targetAvatar;
            _targetAvatar = EditorGUILayout.ObjectField(
                L.INST_AVATAR, _targetAvatar, typeof(VRCAvatarDescriptor), allowSceneObjects: true
            ) as VRCAvatarDescriptor;

            if (_targetAvatar != priorTargetAvatar)
            {
                _installMenu = null;
            }

            using (new EditorGUI.DisabledGroupScope(_params == null || _params.embeddedExpressionsMenu == null))
            {
                _installMenu = EditorGUILayout.ObjectField(
                    L.INST_MENU, _installMenu, typeof(VRCExpressionsMenu), allowSceneObjects: false
                ) as VRCExpressionsMenu;
            }

            var prechecks = IsReadyToInstall();

            using (new EditorGUI.DisabledGroupScope(!prechecks))
            {
                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(L.INST_TX))
                    {
                        ApplyTransmitter();
                    }

                    if (GUILayout.Button(L.INST_RX))
                    {
                        ApplyReceiver();
                    }
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
        }

        // ReSharper disable Unity.PerformanceAnalysis
        private void ApplyReceiver()
        {
            var avrcParameters = _params;
            if (avrcParameters == null) return;
            
            var root = CreateRoot(_targetAvatar.gameObject);

            var names = new AvrcNames(avrcParameters);
            var objects = new AvrcObjects(avrcParameters, names);
            
            objects.buildReceiverBase(root, new AvrcNames(avrcParameters).Prefix);
            AvrcRxStateMachines.SetupRx(_targetAvatar, avrcParameters);
            
            InstallMenu();
        }

        // ReSharper disable Unity.PerformanceAnalysis
        private void ApplyTransmitter()
        {
            var avrcParameters = _params;
            if (avrcParameters == null) return;
            
            var root = CreateRoot(_targetAvatar.gameObject);
            
            var names = new AvrcNames(avrcParameters);
            var objects = new AvrcObjects(avrcParameters, names);
            
            objects.buildReceiverBase(root, new AvrcNames(avrcParameters).Prefix);
            AvrcTxStateMachines.SetupTx(_targetAvatar, avrcParameters);

            InstallMenu();
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

            ok = ok && Precheck(L.INST_ERR_NO_PARAMS, _params != null);
            ok = ok && Precheck(L.INST_NO_PREFIX, _params.prefix != null && !_params.prefix.Equals(""));
            ok = ok && Precheck(L.INST_NO_AVATAR, _targetAvatar != null);
            ok = ok && Precheck(L.INST_NO_FX, AvrcAnimatorUtils.FindFxLayer(_targetAvatar) != null);
            ok = ok && Precheck(L.INST_MENU_FULL, !IsTargetMenuFull());

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

            MenuCloner cloner = new MenuCloner(
                new SerializedObject(menuRef).FindProperty(nameof(MenuReference.menu)),
                rootTargetMenu
            );
            
            menuRef.menu = rootTargetMenu;
            cloner.SyncMenus(_params.embeddedExpressionsMenu);

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
    }
}