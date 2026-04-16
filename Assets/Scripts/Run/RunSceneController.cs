using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Drives the Run scene — the space between battles where the player navigates the map.
///
/// Flow each time this scene loads:
///   Fresh run  → show map (map was generated when RunState was created)
///   Battle won → give node rewards → show map
///   Boss won   → give boss rewards → show run-complete panel
///   Battle lost → show run-over panel
///
/// Non-combat nodes (Camp, Shop, Event) are handled entirely within this scene:
/// the map hides, the relevant panel shows, and when the player leaves the map
/// is shown again.
///
/// === Scene Setup ===
/// 1. Create a "Run" scene and add it to Build Settings.
/// 2. Place a GameObject with this component and wire all inspector references.
/// 3. MapView, BoonRewardPanel, FragmentSwapPanel, CampPanel, ShopPanel,
///    and RunOverPanel should all start hidden (inactive).
/// </summary>
public class RunSceneController : MonoBehaviour
{
    [Header("Scene Names")]
    [SerializeField] private string _battleSceneName = "Battle";
    [SerializeField] private string _hubSceneName    = "Hub";

    [Header("Panels")]
    [SerializeField] private MapView          _mapView;
    [SerializeField] private NodeRewardPanel  _nodeRewardPanel;
    [SerializeField] private BoonRewardPanel  _boonRewardPanel;
    [SerializeField] private FragmentSwapPanel _fragmentSwapPanel;
    [SerializeField] private CampPanel        _campPanel;
    [SerializeField] private ShopPanel        _shopPanel;
    [SerializeField] private RunOverPanel     _runOverPanel;

    [Header("HUD")]
    [Tooltip("TextMeshProUGUI to display the player's current gold.")]
    [SerializeField] private TextMeshProUGUI _moneyText;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        HideAllPanels();

        var result = BattleResultCarrier.LastResult;
        BattleResultCarrier.Clear();

        var run = RunCarrier.CurrentRun;
        if (run == null)
        {
            Debug.LogError("[RunSceneController] No active RunState — returning to Hub.");
            SceneManager.LoadScene(_hubSceneName);
            return;
        }

        UpdateMoneyDisplay();

        switch (result)
        {
            case BattleResultCarrier.Result.None:
                // Fresh run — map was generated in RunState constructor
                ShowMap();
                break;

            case BattleResultCarrier.Result.Win:
                HandleBattleWon(run);
                break;

            case BattleResultCarrier.Result.Loss:
                ShowRunOver(won: false, run);
                break;
        }
    }

    // ── Battle result handling ────────────────────────────────────────────────

    private void HandleBattleWon(RunState run)
    {
        var node = run.Map.CurrentNode;
        if (node == null)
        {
            Debug.LogError("[RunSceneController] CurrentNode is null after a win.");
            ShowMap();
            return;
        }

        // Mark visited and give money
        run.Map.MarkCurrentNodeVisited();
        int goldEarned = MoneyForNode(node.Type, run.Config);
        run.EarnMoney(goldEarned);
        UpdateMoneyDisplay();

        bool isBoss      = node.Type == NodeType.Boss;
        bool hasSwap     = node.Type == NodeType.StandardConflict || isBoss;
        bool hasBoon     = node.Type == NodeType.HardConflict     || isBoss;
        string header    = isBoss ? "Boss Defeated!" : "Battle Complete";

        _nodeRewardPanel?.Show(header, goldEarned, hasSwap, hasBoon, isBoss, onContinue: () =>
        {
            if (isBoss)
                ShowRunOver(won: true, run);
            else
                ShowMap();
        });
    }

    // ── Map ───────────────────────────────────────────────────────────────────

    private void ShowMap()
    {
        var run = RunCarrier.CurrentRun;
        if (run == null) { ReturnToHub(); return; }

        UpdateMoneyDisplay();
        _mapView?.Show(run.Map, run.Config, OnNodeSelected);
    }

    private void OnNodeSelected(MapNode node)
    {
        _mapView?.Hide();

        var run = RunCarrier.CurrentRun;
        if (run == null) { ReturnToHub(); return; }

        run.Map.EnterNode(node.Id);

        switch (node.Type)
        {
            case NodeType.Start:
                // Should never be selectable, but guard just in case
                ShowMap();
                return;

            case NodeType.StandardConflict:
            case NodeType.HardConflict:
            case NodeType.Boss:
                LaunchBattle();
                break;

            case NodeType.Camp:
                _campPanel?.Show(() =>
                {
                    run.Map.MarkCurrentNodeVisited();
                    ShowMap();
                });
                break;

            case NodeType.Shop:
                _shopPanel?.Show(() =>
                {
                    run.Map.MarkCurrentNodeVisited();
                    ShowMap();
                });
                break;

            case NodeType.Event:
                // Stub: events are not yet implemented; mark visited and return to map
                Debug.Log("[RunSceneController] Event node — not yet implemented.");
                run.Map.MarkCurrentNodeVisited();
                ShowMap();
                break;
        }
    }

    // ── Scene transitions ─────────────────────────────────────────────────────

    private void LaunchBattle() => SceneManager.LoadScene(_battleSceneName);

    private void ReturnToHub()
    {
        RunCarrier.ClearRun();
        SceneManager.LoadScene(_hubSceneName);
    }

    // ── Run over ──────────────────────────────────────────────────────────────

    private void ShowRunOver(bool won, RunState run)
    {
        _runOverPanel?.Show(
            won:          won,
            nodesVisited: run.Map.VisitedCount,
            boonsEarned:  run.ActiveBoons.Count,
            onReturn:     ReturnToHub
        );
    }

    // ── HUD ───────────────────────────────────────────────────────────────────

    private void UpdateMoneyDisplay()
    {
        var run = RunCarrier.CurrentRun;
        if (_moneyText && run != null)
            _moneyText.text = $"Gold: {run.Money}";
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void HideAllPanels()
    {
        _mapView?.Hide();
        _nodeRewardPanel?.Hide();
        _boonRewardPanel?.Hide();
        _fragmentSwapPanel?.Hide();
        _campPanel?.Hide();
        _shopPanel?.Hide();
        _runOverPanel?.Hide();
    }

    private static int MoneyForNode(NodeType type, RunConfig config) => type switch
    {
        NodeType.StandardConflict => config.moneyPerStandard,
        NodeType.HardConflict     => config.moneyPerHard,
        NodeType.Boss             => config.moneyPerBoss,
        _                         => 0,
    };
}
