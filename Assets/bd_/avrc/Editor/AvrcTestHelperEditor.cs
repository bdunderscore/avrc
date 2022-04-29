using System;
using System.Reflection;
using UnityEditor;

namespace net.fushizen.avrc
{
    [CustomEditor(typeof(AvrcTestHelper))]
    public class AvrcTestHelperEditor : Editor
    {
        private static Localizations L => Localizations.Inst;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            // Check for Av3 emulator version
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            Type emulator = null;
            foreach (var assembly in assemblies)
            {
                var klass = assembly.GetType("LyumaAv3Emulator");
                if (klass != null)
                {
                    emulator = klass;
                    break;
                }
            }

            if (emulator == null)
            {
                EditorGUILayout.HelpBox(L.TEST_NO_AV3EMU, MessageType.Info);
                return;
            }

            var field = emulator.GetField("EMULATOR_VERSION", BindingFlags.Static | BindingFlags.NonPublic);
            if (field == null || (ulong) field.GetValue(null) < 0x2_09_08_00)
            {
                EditorGUILayout.HelpBox(L.TEST_OLD_AV3EMU, MessageType.Error);
            }
        }
    }
}