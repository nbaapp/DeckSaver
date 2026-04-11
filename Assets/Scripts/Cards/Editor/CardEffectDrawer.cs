using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(CardEffect))]
public class CardEffectDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var typeProp = property.FindPropertyRelative("type");
        bool isStatus = typeProp.enumValueIndex == (int)EffectType.Status;
        int lines = isStatus ? 4 : 3; // type + baseValue + hits [+ statusType]
        return EditorGUIUtility.singleLineHeight * lines + EditorGUIUtility.standardVerticalSpacing * (lines - 1);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        float lineH   = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;
        float step    = lineH + spacing;

        var typeProp   = property.FindPropertyRelative("type");
        var valueProp  = property.FindPropertyRelative("baseValue");
        var hitsProp   = property.FindPropertyRelative("hits");
        var statusProp = property.FindPropertyRelative("statusType");

        EditorGUI.PropertyField(new Rect(position.x, position.y,           position.width, lineH), typeProp);
        EditorGUI.PropertyField(new Rect(position.x, position.y + step,    position.width, lineH), valueProp);
        EditorGUI.PropertyField(new Rect(position.x, position.y + step * 2, position.width, lineH), hitsProp);

        if (typeProp.enumValueIndex == (int)EffectType.Status)
            EditorGUI.PropertyField(new Rect(position.x, position.y + step * 3, position.width, lineH), statusProp);

        EditorGUI.EndProperty();
    }
}
