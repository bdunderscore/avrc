using UnityEditor;
using UnityEngine;

namespace net.fushizen.avrc
{
    [CustomPropertyDrawer(typeof(AvrcSignalType))]
    public class FieldTypeDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            property.enumValueIndex = EditorGUI.Popup(position, label, property.enumValueIndex,
                Localizations.Inst.PROP_TYPE_NAMES);
        }
    }

    [CustomPropertyDrawer(typeof(NoSignalMode))]
    public class NoSignalDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            property.enumValueIndex = EditorGUI.Popup(position, label, property.enumValueIndex,
                Localizations.Inst.PROP_NOSIG_NAMES);
        }
    }
}