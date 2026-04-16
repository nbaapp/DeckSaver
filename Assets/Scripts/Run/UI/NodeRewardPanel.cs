using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shown after winning a battle. Displays every reward available for the node
/// so the player can claim them in any order before continuing.
///
/// Rewards by node type:
///   Standard Conflict — fragment swap + gold
///   Hard Conflict     — boon + gold
///   Boss              — boon + fragment swap + gold
///
/// Each reward row has a "Claim" button that opens the relevant sub-panel.
/// Once claimed the button is disabled and a "Claimed" label appears.
/// The player can click "Continue" at any time — unclaimed rewards are forfeited.
///
/// Wire _fragmentSwapPanel and _boonRewardPanel in the inspector;
/// both panels should be siblings higher in the canvas so they render on top.
/// </summary>
public class NodeRewardPanel : MonoBehaviour
{
    [Header("Header")]
    [SerializeField] private TextMeshProUGUI _headerText;
    [SerializeField] private TextMeshProUGUI _goldText;

    [Header("Fragment Swap Row")]
    [SerializeField] private GameObject      _swapRow;
    [SerializeField] private TextMeshProUGUI _swapStatusText;
    [SerializeField] private Button          _swapClaimButton;

    [Header("Boon Row")]
    [SerializeField] private GameObject      _boonRow;
    [SerializeField] private TextMeshProUGUI _boonStatusText;
    [SerializeField] private Button          _boonClaimButton;

    [Header("Continue")]
    [SerializeField] private Button _continueButton;

    [Header("Sub-panels (must render above this panel)")]
    [SerializeField] private FragmentSwapPanel _fragmentSwapPanel;
    [SerializeField] private BoonRewardPanel   _boonRewardPanel;

    // ── State ─────────────────────────────────────────────────────────────────

    private bool   _hasSwap, _hasBoon, _isBoss;
    private bool   _swapClaimed, _boonClaimed;
    private Action _onContinue;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Show the reward panel for the node the player just cleared.
    /// header: e.g. "Battle Complete" or "Boss Defeated!"
    /// goldEarned: already added to RunState; displayed here for feedback only.
    /// hasFragmentSwap / hasBoon: which rewards this node type grants.
    /// isBoss: if true, uses the boss boon pool instead of the general pool.
    /// onContinue: called when the player presses Continue.
    /// </summary>
    public void Show(string header, int goldEarned,
        bool hasFragmentSwap, bool hasBoon, bool isBoss,
        Action onContinue)
    {
        _hasSwap     = hasFragmentSwap;
        _hasBoon     = hasBoon;
        _isBoss      = isBoss;
        _swapClaimed = false;
        _boonClaimed = false;
        _onContinue  = onContinue;

        gameObject.SetActive(true);

        if (_headerText) _headerText.text = header;
        if (_goldText)   _goldText.text   = goldEarned > 0 ? $"+{goldEarned} Gold" : string.Empty;

        _swapRow?.SetActive(_hasSwap);
        _boonRow?.SetActive(_hasBoon);

        _continueButton?.onClick.RemoveAllListeners();
        _continueButton?.onClick.AddListener(Continue);

        RefreshRewardButtons();
    }

    public void Hide() => gameObject.SetActive(false);

    // ── Claim actions ─────────────────────────────────────────────────────────

    private void ClaimSwap()
    {
        var run = RunCarrier.CurrentRun;
        if (run == null) return;

        var choices = run.GenerateFragmentChoices();

        if (_fragmentSwapPanel == null || choices.Count == 0)
        {
            // Pool not configured — mark claimed and continue
            Debug.LogWarning("[NodeRewardPanel] No fragment choices available (fragmentSwapPool may be empty).");
            _swapClaimed = true;
            RefreshRewardButtons();
            return;
        }

        _fragmentSwapPanel.Show(choices, () =>
        {
            _swapClaimed = true;
            RefreshRewardButtons();
        });
    }

    private void ClaimBoon()
    {
        var run = RunCarrier.CurrentRun;
        if (run == null) return;

        var pool = (_isBoss && run.Config.bossBoonPool.Count > 0)
            ? run.Config.bossBoonPool
            : run.Config.boonPool;

        var choices = run.GenerateBoonChoices(pool);

        if (_boonRewardPanel == null || choices.Count == 0)
        {
            Debug.LogWarning("[NodeRewardPanel] No boon choices available (boonPool may be empty).");
            _boonClaimed = true;
            RefreshRewardButtons();
            return;
        }

        _boonRewardPanel.Show("Choose a Boon", choices, () =>
        {
            _boonClaimed = true;
            RefreshRewardButtons();
        });
    }

    // ── UI ────────────────────────────────────────────────────────────────────

    private void RefreshRewardButtons()
    {
        if (_swapClaimButton != null)
        {
            _swapClaimButton.interactable = _hasSwap && !_swapClaimed;
            _swapClaimButton.onClick.RemoveAllListeners();
            if (!_swapClaimed)
                _swapClaimButton.onClick.AddListener(ClaimSwap);
        }
        if (_swapStatusText)
            _swapStatusText.text = _swapClaimed ? "Claimed" : "Available";

        if (_boonClaimButton != null)
        {
            _boonClaimButton.interactable = _hasBoon && !_boonClaimed;
            _boonClaimButton.onClick.RemoveAllListeners();
            if (!_boonClaimed)
                _boonClaimButton.onClick.AddListener(ClaimBoon);
        }
        if (_boonStatusText)
            _boonStatusText.text = _boonClaimed ? "Claimed" : "Available";
    }

    private void Continue()
    {
        Hide();
        _onContinue?.Invoke();
    }
}
