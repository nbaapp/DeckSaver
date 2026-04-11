/// <summary>
/// One thing the player can be offered as a reward after an encounter.
///
/// After a regular encounter the player picks from N of these.
/// After a boss they pick from M, then the save prompt appears.
///
/// FragmentSwap is a meta-option: selecting it opens the FragmentSwapPanel,
/// which then generates and shows the three specific fragment choices.
/// This keeps the two-level structure (pick reward type → pick specific fragment)
/// extensible for future shop-style rewards without redesigning the offer flow.
/// </summary>
public enum RewardOptionType
{
    Boon,             // A specific boon to claim immediately
    FragmentSwap,     // Enter the fragment-swap flow (3 fragments will be generated)
    FragmentUpgrade,  // Enter the fragment-upgrade flow (pick a card, then a fragment half)
}

public class RewardOption
{
    public RewardOptionType type;

    // Populated when type == Boon
    public BoonData boon;

    // Human-readable label shown on the offer button when type == FragmentSwap
    // (e.g. "Swap a Fragment"). The panel itself shows the specific fragments.
    public string fragmentSwapLabel = "Swap a Fragment";

    // ── Factories ─────────────────────────────────────────────────────────────

    public static RewardOption ForBoon(BoonData boon) =>
        new() { type = RewardOptionType.Boon, boon = boon };

    public static RewardOption ForFragmentSwap() =>
        new() { type = RewardOptionType.FragmentSwap };

    public static RewardOption ForFragmentUpgrade() =>
        new() { type = RewardOptionType.FragmentUpgrade, fragmentSwapLabel = "Upgrade a Fragment" };
}
