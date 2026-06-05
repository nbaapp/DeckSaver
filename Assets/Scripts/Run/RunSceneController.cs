using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Drives the Run scene — the space between battles where the player navigates the map.
///
/// Flow each time this scene loads:
///   Fresh run (no map)  → show front selection → generate map → show map
///   Battle won          → give node rewards → show map (or advance act / end run)
///   Boss won (act 1-2)  → advance act → show front selection → generate map → show map
///   Boss won (act 3)    → show run-complete panel
///   Shift boss won      → show run-complete panel (shifted)
///   Battle lost         → show run-over panel
///
/// Non-combat nodes (Camp, Shop, Event) are handled entirely within this scene.
/// The Shift node triggers the alternate-ending path.
/// </summary>
public class RunSceneController : MonoBehaviour
{
    [Header("Scene Names")]
    [SerializeField] private string _battleSceneName = "Battle";
    [SerializeField] private string _hubSceneName    = "Hub";

    [Header("Panels")]
    [SerializeField] private MapView              _mapView;
    [SerializeField] private NodeRewardPanel      _nodeRewardPanel;
    [SerializeField] private BoonRewardPanel      _boonRewardPanel;
    [SerializeField] private FragmentSwapPanel    _fragmentSwapPanel;
    [SerializeField] private CampPanel            _campPanel;
    [SerializeField] private ShopPanel            _shopPanel;
    [SerializeField] private RunOverPanel         _runOverPanel;
    [SerializeField] private FrontSelectionPanel  _frontSelectionPanel;

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
                // Fresh run or new act — check if map exists
                if (run.Map == null)
                    ShowFrontSelection(run);
                else
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

    // ── Front selection ──────────────────────────────────────────────────────

    private void ShowFrontSelection(RunState run)
    {
        var actConfig = run.GetCurrentActConfig();
        if (actConfig == null)
        {
            Debug.LogError($"[RunSceneController] No ActConfig for act {run.CurrentAct}.");
            ReturnToHub();
            return;
        }

        _frontSelectionPanel?.Show(actConfig, run.CurrentAct, front =>
        {
            run.SetFront(front);
            ShowMap();
        });
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

        bool isBoss = node.Type == NodeType.Boss;

        // Shift path: give shards instead of normal rewards
        if (run.IsShifted)
        {
            if (isBoss)
            {
                int shards = run.Config.shardsPerShiftEncounter;
                run.EarnShards(shards);
                ShowRunOver(won: true, run);
            }
            else
            {
                int shards = run.Config.shardsPerShiftEncounter;
                run.EarnShards(shards);
                // Simple reward display for shift path — just continue to map
                _nodeRewardPanel?.Show("Battle Complete", goldEarned, hasFragmentSwap: false, hasBoon: false, isBoss: false, onContinue: ShowMap);
            }
            return;
        }

        // Normal path rewards
        bool hasFragmentSwap = node.Type == NodeType.StandardConflict || isBoss;
        bool hasBoon         = node.Type == NodeType.HardConflict     || isBoss;
        string header        = isBoss ? "Boss Defeated!" : "Battle Complete";

        _nodeRewardPanel?.Show(header, goldEarned, hasFragmentSwap, hasBoon, isBoss, onContinue: () =>
        {
            if (isBoss)
                HandleBossDefeated(run);
            else
                ShowMap();
        });
    }

    private void HandleBossDefeated(RunState run)
    {
        if (run.CurrentAct >= 3)
        {
            // Act 3 boss beaten — run won!
            ShowRunOver(won: true, run);
        }
        else
        {
            // Advance to next act
            run.AdvanceAct();
            ShowFrontSelection(run);
        }
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

            case NodeType.Shift:
                HandleShiftNode(run);
                break;
        }
    }

    // ── Shift ─────────────────────────────────────────────────────────────────

    private void HandleShiftNode(RunState run)
    {
        run.Map.MarkCurrentNodeVisited();

        // Give a burst of rewards before entering the shift path
        // For now: a large gold bonus
        int burstGold = run.Config.moneyPerBoss * 2;
        run.EarnMoney(burstGold);
        UpdateMoneyDisplay();

        // Enter the shift — generates the linear shift map
        run.EnterShift();

        _nodeRewardPanel?.Show("Shifted!", burstGold, hasFragmentSwap: false, hasBoon: false, isBoss: false, onContinue: ShowMap);
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
        _frontSelectionPanel?.Hide();
    }

    private static int MoneyForNode(NodeType type, RunConfig config) => type switch
    {
        NodeType.StandardConflict => config.moneyPerStandard,
        NodeType.HardConflict     => config.moneyPerHard,
        NodeType.Boss             => config.moneyPerBoss,
        _                         => 0,
    };
}
