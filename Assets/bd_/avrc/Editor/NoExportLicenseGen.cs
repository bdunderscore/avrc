using System;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace net.fushizen.avrc
{
    public static class NoExportLicenseGen
    {
        [MenuItem("bd_/AVRC Dev/Generate License")]
        private static void GenerateLicense()
        {
            var license = ScriptableObject.CreateInstance<AvrcLicenseAsset>();

            var ident = "Copyright (c) bd_ : AVRC. Don't be naughty.";
            var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.ASCII.GetBytes(ident));
            var initial = Convert.ToBase64String(hash);

            license.secret = initial;

            AssetDatabase.CreateAsset(license, "Assets/bd_/avrc/License.asset");
        }
    }
}