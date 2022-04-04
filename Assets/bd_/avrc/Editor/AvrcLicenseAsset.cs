using UnityEditor;
using UnityEngine;

namespace net.fushizen.avrc
{
    internal class AvrcLicenseAsset : ScriptableObject
    {
        [SerializeField] [HideInInspector] internal string secret;
    }

    [CustomEditor(typeof(AvrcLicenseAsset))]
    internal class AvrcLicenseEditor : Editor
    {
        private void OnEnable()
        {
            AvrcLicenseManager.ClearCache();
        }

        public override void OnInspectorGUI()
        {
            switch (AvrcLicenseManager.GetLicenseState())
            {
                case LicenseState.Ok:
                    EditorGUILayout.HelpBox("License is valid", MessageType.Info);
                    break;
                case LicenseState.Invalid:
                    EditorGUILayout.HelpBox("License is invalid", MessageType.Error);
                    break;
                case LicenseState.Unlicensed:
                    EditorGUILayout.HelpBox("No license found... somehow?", MessageType.Warning);
                    break;
            }
        }
    }
}