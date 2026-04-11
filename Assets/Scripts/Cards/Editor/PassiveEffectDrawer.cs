using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom inspector drawer for PassiveEffect.
/// Only shows fields relevant to the selected trigger type.
/// Enum fields are drawn with EditorGUI.Popup (never PropertyField) to avoid
/// foldout arrows that appear on enums inside ReorderableList elements.
/// </summary>
[CustomPropertyDrawer(typeof(PassiveEffect))]
public class PassiveEffectDrawer : PropertyDrawer
{
    private const float LineH    = 18f;
    private const float Pad      = 2f;
    private const float LineStep = LineH + Pad;

    // ── Height ────────────────────────────────────────────────────────────────

    public override float GetPropertyHeight(SerializedProperty prop, GUIContent label)
    {
        float h = LineStep; // trigger dropdown always shown

        switch (Trigger(prop))
        {
            case PassiveTrigger.StatModifier:
                h += LineStep * 2; // statType + statValue
                break;

            case PassiveTrigger.StatusImmunity:
                h += LineStep; // specificStatus
                break;

            case PassiveTrigger.Special:
                h += LineH + Pad * 4; // help box
                break;

            default:
                h += LineStep * 2; // target + valueSource
                h += FieldH(prop, "effects");

                if (Trigger(prop) == PassiveTrigger.OnStatusApplied)
                {
                    h += LineStep; // statusCondition
                    switch (Condition(prop))
                    {
                        case StatusConditionType.Specific: h += LineStep;                 break;
                        case StatusConditionType.AnyOf:    h += FieldH(prop, "statusSet"); break;
                    }
                }
                break;
        }

        return h;
    }

    // ── Drawing ───────────────────────────────────────────────────────────────

    public override void OnGUI(Rect pos, SerializedProperty prop, GUIContent label)
    {
        EditorGUI.BeginProperty(pos, label, prop);

        var r = Line(pos);

        // Trigger — always first (enum popup, no foldout arrow)
        DrawEnum(ref r, prop, "trigger", "Trigger");

        switch (Trigger(prop))
        {
            // ── Stat modifier ─────────────────────────────────────────────────
            case PassiveTrigger.StatModifier:
                DrawEnum (ref r, prop, "statType",  "Stat");
                DrawInt  (ref r, prop, "statValue", "Value");
                break;

            // ── Status immunity ───────────────────────────────────────────────
            case PassiveTrigger.StatusImmunity:
                DrawEnum(ref r, prop, "specificStatus", "Immune To");
                break;

            // ── Special: no fields, just a note ───────────────────────────────
            case PassiveTrigger.Special:
                EditorGUI.HelpBox(
                    new Rect(r.x, r.y, r.width, LineH + Pad * 4),
                    "Handled in code by Commander name / ID.", MessageType.Info);
                break;

            // ── Event-based triggers ──────────────────────────────────────────
            default:
                DrawEnum(ref r, prop, "target",      "Target");
                DrawEnum(ref r, prop, "valueSource", "Value Source");
                DrawList(ref r, prop, "effects");

                if (Trigger(prop) == PassiveTrigger.OnStatusApplied)
                {
                    DrawEnum(ref r, prop, "statusCondition", "Status Condition");

                    switch (Condition(prop))
                    {
                        case StatusConditionType.Specific:
                            DrawEnum(ref r, prop, "specificStatus", "Specific Status");
                            break;
                        case StatusConditionType.AnyOf:
                            DrawList(ref r, prop, "statusSet", "Status Set");
                            break;
                    }
                }
                break;
        }

        EditorGUI.EndProperty();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Draw an enum child field as a flat Popup (no foldout arrow) and advance the rect.</summary>
    private static void DrawEnum(ref Rect r, SerializedProperty parent, string childName, string displayName)
    {
        var prop = parent.FindPropertyRelative(childName);
        EditorGUI.BeginChangeCheck();
        int idx = EditorGUI.Popup(r, displayName, prop.enumValueIndex, prop.enumDisplayNames);
        if (EditorGUI.EndChangeCheck())
            prop.enumValueIndex = idx;
        r.y += LineStep;
    }

    /// <summary>Draw an integer child field and advance the rect.</summary>
    private static void DrawInt(ref Rect r, SerializedProperty parent, string childName, string displayName)
    {
        var prop = parent.FindPropertyRelative(childName);
        EditorGUI.BeginChangeCheck();
        int val = EditorGUI.IntField(r, displayName, prop.intValue);
        if (EditorGUI.EndChangeCheck())
            prop.intValue = val;
        r.y += LineStep;
    }

    /// <summary>Draw a variable-height list/array child field and advance the rect.</summary>
    private static void DrawList(ref Rect r, SerializedProperty parent, string childName, string displayName = null)
    {
        var child  = parent.FindPropertyRelative(childName);
        var gc     = new GUIContent(displayName ?? child.displayName);
        float h    = EditorGUI.GetPropertyHeight(child, gc, true);
        r.height   = h;
        EditorGUI.PropertyField(r, child, gc, true);
        r.y       += h + Pad;
        r.height   = LineH;
    }

    /// <summary>Returns the height of a child property.</summary>
    private static float FieldH(SerializedProperty parent, string childName) =>
        EditorGUI.GetPropertyHeight(parent.FindPropertyRelative(childName), true) + Pad;

    /// <summary>Returns a line-height rect at the top of the full position rect.</summary>
    private static Rect Line(Rect pos) => new Rect(pos.x, pos.y, pos.width, LineH);

    private static PassiveTrigger      Trigger  (SerializedProperty p) =>
        (PassiveTrigger)     p.FindPropertyRelative("trigger")         .enumValueIndex;

    private static StatusConditionType Condition(SerializedProperty p) =>
        (StatusConditionType)p.FindPropertyRelative("statusCondition") .enumValueIndex;
}
