using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace net.fushizen.avrc
{
    internal enum LicenseState
    {
        Ok,
        Unlicensed,
        Invalid
    }

    internal class GUIDHolder : ScriptableObject
    {
        [SerializeField] internal GUID productGUID;
    }

    internal class AvrcLicenseManager
    {
        private const string ident = "Copyright (c) bd_ : AVRC. Don't be naughty.";

        private static LicenseState? CachedLicenseState;

        internal static LicenseState GetLicenseState()
        {
            if (CachedLicenseState.HasValue) return CachedLicenseState.Value;

            CachedLicenseState = CheckLicense();

            return CachedLicenseState.Value;
        }

        internal static void ClearCache()
        {
            CachedLicenseState = null;
        }

        private static LicenseState CheckLicense()
        {
            var licenses = AssetDatabase.FindAssets("t:AvrcLicenseAsset");
            if (licenses.Length == 0) return LicenseState.Unlicensed;

            if (licenses.Length > 1)
            {
                Debug.LogError("Found more than one AVRC license asset. Please delete all but one.");
                return LicenseState.Invalid;
            }

            var license = AssetDatabase.LoadAssetAtPath<AvrcLicenseAsset>(AssetDatabase.GUIDToAssetPath(licenses[0]));

            var projectSettings =
                AssetDatabase.LoadAssetAtPath<PlayerSettings>("ProjectSettings/ProjectSettings.asset");
            var serializedObject = new SerializedObject(projectSettings);
            var guidProp = serializedObject.FindProperty("productGUID");

            var guid = "";
            guidProp.Next(true);
            guid += new string($"{guidProp.longValue:X08}".Reverse().ToArray());
            guidProp.Next(true);
            guid += new string($"{guidProp.longValue:X08}".Reverse().ToArray());
            guidProp.Next(true);
            guid += new string($"{guidProp.longValue:X08}".Reverse().ToArray());
            guidProp.Next(true);
            guid += new string($"{guidProp.longValue:X08}".Reverse().ToArray());

            // SUPER SECRET HASH METHOD LOL
            var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.ASCII.GetBytes(ident));
            var initial = Convert.ToBase64String(hash);

            hash = sha256.ComputeHash(Encoding.ASCII.GetBytes(ident + "$" + guid));
            var personalized = Convert.ToBase64String(hash);

            if (license.secret == personalized) return LicenseState.Ok;
            if (license.secret == initial)
            {
                license.secret = personalized;
                EditorUtility.SetDirty(license);
                AssetDatabase.SaveAssets();
                return LicenseState.Ok;
            }

            return LicenseState.Invalid;
        }

        internal static List<string> MungeContactTag(string contactTag, bool isSender)
        {
            var mySuffix = (char) (isSender ? 1 : 0);
            var theirSuffix = (char) (isSender ? 0 : 1);

            var tags = new List<string>();

            var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.ASCII.GetBytes(contactTag + mySuffix));
            tags.Add("_AVRCI_" + Convert.ToBase64String(hash));

            if (GetLicenseState() == LicenseState.Ok)
            {
                hash = sha256.ComputeHash(Encoding.ASCII.GetBytes(contactTag + theirSuffix));
                tags.Add("_AVRCI_" + Convert.ToBase64String(hash));
            }

            return tags;
        }
    }
}