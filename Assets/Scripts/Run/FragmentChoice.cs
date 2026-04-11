/// <summary>
/// One fragment that can be offered in a fragment-swap panel.
/// Exactly one of effectFragment or modifierFragment will be set.
/// </summary>
public class FragmentChoice
{
    public bool isEffect;
    public EffectFragmentData effectFragment;
    public ModifierFragmentData modifierFragment;

    public string FragmentName => isEffect
        ? effectFragment?.fragmentName ?? "?"
        : modifierFragment?.fragmentName ?? "?";

    public static FragmentChoice ForEffect(EffectFragmentData frag) =>
        new() { isEffect = true, effectFragment = frag };

    public static FragmentChoice ForModifier(ModifierFragmentData frag) =>
        new() { isEffect = false, modifierFragment = frag };
}
