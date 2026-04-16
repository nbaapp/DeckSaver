using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shop node UI. Displays a random selection of effect fragments, modifier fragments,
/// and boons for purchase. Each item can only be purchased once per visit.
///
/// Buying a fragment opens FragmentSwapPanel's apply-only flow so the player can
/// choose which card to apply it to.
/// Buying a boon adds it directly to ActiveBoons.
///
/// Wire all inspector references; set shopItemsPerCategory in RunConfig.
/// </summary>
public class ShopPanel : MonoBehaviour
{
    [Header("Slot Prefab")]
    [Tooltip("Prefab with: TextMeshProUGUI (item name), TextMeshProUGUI (price label), Button (buy).")]
    [SerializeField] private GameObject _shopSlotPrefab;

    [Header("Category Parents")]
    [SerializeField] private Transform _effectParent;
    [SerializeField] private Transform _modifierParent;
    [SerializeField] private Transform _boonParent;

    [Header("Category Headers (optional)")]
    [SerializeField] private TextMeshProUGUI _effectHeader;
    [SerializeField] private TextMeshProUGUI _modifierHeader;
    [SerializeField] private TextMeshProUGUI _boonHeader;

    [Header("Shared")]
    [SerializeField] private TextMeshProUGUI  _moneyText;
    [SerializeField] private Button           _leaveButton;

    [Header("Fragment Swap Sub-panel")]
    [SerializeField] private FragmentSwapPanel _fragmentSwapPanel;

    // ── State ─────────────────────────────────────────────────────────────────

    private Action _onLeave;

    private readonly List<EffectFragmentData>   _offeredEffects    = new();
    private readonly List<ModifierFragmentData> _offeredModifiers  = new();
    private readonly List<BoonData>             _offeredBoons      = new();

    // Tracks which items have been purchased this visit (by index within their list)
    private readonly HashSet<int> _purchasedEffects   = new();
    private readonly HashSet<int> _purchasedModifiers = new();
    private readonly HashSet<int> _purchasedBoons     = new();

    private readonly List<GameObject> _allSlots = new();

    // ── Public API ────────────────────────────────────────────────────────────

    public void Show(Action onLeave)
    {
        _onLeave = onLeave;
        gameObject.SetActive(true);

        _purchasedEffects.Clear();
        _purchasedModifiers.Clear();
        _purchasedBoons.Clear();

        GenerateInventory();
        BuildSlots();

        _leaveButton?.onClick.RemoveAllListeners();
        _leaveButton?.onClick.AddListener(Leave);

        RefreshMoneyDisplay();
    }

    public void Hide() => gameObject.SetActive(false);

    // ── Inventory generation ──────────────────────────────────────────────────

    private void GenerateInventory()
    {
        var run = RunCarrier.CurrentRun;
        if (run == null) return;

        int n = run.Config.shopItemsPerCategory;

        _offeredEffects.Clear();
        _offeredModifiers.Clear();
        _offeredBoons.Clear();

        _offeredEffects.AddRange(PickRandom(run.Config.shopEffectFragments, n));
        _offeredModifiers.AddRange(PickRandom(run.Config.shopModifierFragments, n));
        _offeredBoons.AddRange(PickRandom(run.Config.shopBoons, n));
    }

    private static List<T> PickRandom<T>(List<T> source, int count)
    {
        var shuffled = new List<T>(source);
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }
        int take = Mathf.Min(count, shuffled.Count);
        return shuffled.GetRange(0, take);
    }

    // ── Slot building ─────────────────────────────────────────────────────────

    private void BuildSlots()
    {
        foreach (var slot in _allSlots) Destroy(slot);
        _allSlots.Clear();

        if (_effectHeader)   _effectHeader.text   = "Effect Fragments";
        if (_modifierHeader) _modifierHeader.text = "Modifier Fragments";
        if (_boonHeader)     _boonHeader.text     = "Boons";

        for (int i = 0; i < _offeredEffects.Count; i++)
        {
            int captured = i;
            var frag     = _offeredEffects[i];
            CreateSlot(_effectParent, frag.fragmentName, frag.shopPrice,
                () => BuyEffect(captured));
        }

        for (int i = 0; i < _offeredModifiers.Count; i++)
        {
            int captured = i;
            var frag     = _offeredModifiers[i];
            CreateSlot(_modifierParent, frag.fragmentName, frag.shopPrice,
                () => BuyModifier(captured));
        }

        for (int i = 0; i < _offeredBoons.Count; i++)
        {
            int captured = i;
            var boon     = _offeredBoons[i];
            CreateSlot(_boonParent, boon.boonName, boon.shopPrice,
                () => BuyBoon(captured));
        }
    }

    private void CreateSlot(Transform parent, string itemName, int price, Action onBuy)
    {
        if (_shopSlotPrefab == null || parent == null) return;

        var slot   = Instantiate(_shopSlotPrefab, parent);
        _allSlots.Add(slot);

        // Expect two TMP children: first = name, second = price
        var tmps = slot.GetComponentsInChildren<TextMeshProUGUI>();
        if (tmps.Length > 0) tmps[0].text = itemName;
        if (tmps.Length > 1) tmps[1].text = price > 0 ? $"{price}g" : "Free";

        var btn = slot.GetComponentInChildren<Button>();
        if (btn != null) btn.onClick.AddListener(() => onBuy());
    }

    // ── Purchases ─────────────────────────────────────────────────────────────

    private void BuyEffect(int index)
    {
        if (_purchasedEffects.Contains(index)) return;
        var run  = RunCarrier.CurrentRun;
        var frag = _offeredEffects[index];
        if (run == null || !run.SpendMoney(frag.shopPrice)) return;

        _purchasedEffects.Add(index);
        RefreshMoneyDisplay();

        var choice = FragmentChoice.ForEffect(frag);
        OpenApplyFlow(choice);
    }

    private void BuyModifier(int index)
    {
        if (_purchasedModifiers.Contains(index)) return;
        var run  = RunCarrier.CurrentRun;
        var frag = _offeredModifiers[index];
        if (run == null || !run.SpendMoney(frag.shopPrice)) return;

        _purchasedModifiers.Add(index);
        RefreshMoneyDisplay();

        var choice = FragmentChoice.ForModifier(frag);
        OpenApplyFlow(choice);
    }

    private void BuyBoon(int index)
    {
        if (_purchasedBoons.Contains(index)) return;
        var run  = RunCarrier.CurrentRun;
        var boon = _offeredBoons[index];
        if (run == null || !run.SpendMoney(boon.shopPrice)) return;

        _purchasedBoons.Add(index);
        run.AddBoon(boon);
        RefreshMoneyDisplay();
        RefreshButtonStates();
    }

    private void OpenApplyFlow(FragmentChoice choice)
    {
        if (_fragmentSwapPanel == null)
        {
            RefreshButtonStates();
            return;
        }

        _fragmentSwapPanel.ShowApplyStep(choice, () =>
        {
            RefreshButtonStates();
        });
    }

    // ── UI helpers ────────────────────────────────────────────────────────────

    private void RefreshMoneyDisplay()
    {
        var run = RunCarrier.CurrentRun;
        if (_moneyText && run != null)
            _moneyText.text = $"Gold: {run.Money}";
    }

    private void RefreshButtonStates()
    {
        var run = RunCarrier.CurrentRun;
        if (run == null) return;

        // Refresh all slot buttons — re-evaluating affordability and purchase status
        // Rebuild is simpler than tracking individual buttons
        BuildSlots();
        RefreshMoneyDisplay();
    }

    private void Leave()
    {
        Hide();
        _onLeave?.Invoke();
    }
}
