using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Drives the Run scene — the hub between battles.
/// This scene is loaded after each battle (win or loss) and at the start of a run.
///
/// Flow:
///   Fresh run  → first encounter → load Battle scene
///   Battle won → reward panel (unless post-save) → advance → next encounter
///   Boss beaten → reward panel → save prompt → (save or continue)
///   Battle lost → run over panel → return to Hub
///
/// === Scene Setup ===
/// 1. Create a "Run" scene and add it to Build Settings.
/// 2. Place a GameObject with this component.
/// 3. Wire all panel references in the inspector.
/// 4. The encounter info area (encounter name, enemy count) is populated automatically.
/// 5. Story content can be placed in this scene freely — the flow will show panels
///    on top of or alongside whatever scene content you add later.
/// </summary>
public class RunSceneController : MonoBehaviour
{
    [Header("Scene Names")]
    [Tooltip("The battle scene to load for combat encounters.")]
    [SerializeField] private string _battleSceneName = "Battle";
    [Tooltip("The hub scene to load when the run ends.")]
    [SerializeField] private string _hubSceneName = "Hub";

    [Header("Panels")]
    [SerializeField] private RewardPanel    _rewardPanel;
    [SerializeField] private SavePromptPanel _savePromptPanel;
    [SerializeField] private RunOverPanel    _runOverPanel;

    [Header("Encounter Info (optional)")]
    [Tooltip("Assign a TextMeshProUGUI to display the current encounter name.")]
    [SerializeField] private TMPro.TextMeshProUGUI _encounterNameText;
    [Tooltip("Assign a TextMeshProUGUI to display the enemy count.")]
    [SerializeField] private TMPro.TextMeshProUGUI _enemyCountText;
    [Tooltip("A button or panel the player clicks to begin the battle. Hide it for reward-only encounters.")]
    [SerializeField] private UnityEngine.UI.Button _beginEncounterButton;

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

        switch (result)
        {
            case BattleResultCarrier.Result.None:
                // Fresh run start — head straight to the first encounter
                PresentCurrentEncounter();
                break;

            case BattleResultCarrier.Result.Win:
                HandleBattleWon(run);
                break;

            case BattleResultCarrier.Result.Loss:
                HandleBattleLost(run);
                break;
        }
    }

    // ── Win/loss handling ─────────────────────────────────────────────────────

    private void HandleBattleWon(RunState run)
    {
        bool wasBoss       = run.IsAtBoss;
        bool wasFinalBoss  = run.IsAtFinalBoss;

        if (wasFinalBoss)
        {
            // Post-save final boss defeated — run complete
            ShowRunOver(won: true, run);
            return;
        }

        if (run.IsPostSave)
        {
            // Post-save: no rewards, just advance to next encounter
            run.AdvanceEncounter();
            PresentCurrentEncounter();
            return;
        }

        // Generate reward options and show the panel
        var options = run.GenerateRewardOptions(wasBoss);
        string header = wasBoss ? "Boss Defeated!" : "Encounter Complete";

        _rewardPanel?.Show(header, options, onChosen: () =>
        {
            if (wasBoss)
                ShowSavePrompt(run);
            else
            {
                run.AdvanceEncounter();
                PresentCurrentEncounter();
            }
        });
    }

    private void HandleBattleLost(RunState run) => ShowRunOver(won: false, run);

    // ── Save prompt ───────────────────────────────────────────────────────────

    private void ShowSavePrompt(RunState run)
    {
        if (run.HasSaved)
        {
            // Already saved this run (shouldn't normally reach here) — just continue
            run.StartNextSegment();
            PresentCurrentEncounter();
            return;
        }

        _savePromptPanel?.Show(
            onSave: () =>
            {
                run.Save(); // Enters post-save mode; EncounterIndex reset to 0
                PresentCurrentEncounter();
            },
            onContinue: () =>
            {
                run.StartNextSegment();
                PresentCurrentEncounter();
            }
        );
    }

    // ── Encounter presentation ────────────────────────────────────────────────

    private void PresentCurrentEncounter()
    {
        var run       = RunCarrier.CurrentRun;
        var encounter = run?.CurrentEncounter();

        if (encounter == null)
        {
            Debug.LogError("[RunSceneController] CurrentEncounter is null.");
            ReturnToHub();
            return;
        }

        // Update encounter info text
        if (_encounterNameText)
            _encounterNameText.text = encounter.encounterName;

        if (_enemyCountText)
        {
            _enemyCountText.text = encounter.type == EncounterType.Battle
                ? $"{encounter.enemySpawns.Count} enemies"
                : "No combat";
        }

        if (encounter.type == EncounterType.RewardOnly)
        {
            // Reward-only encounter: skip battle, go straight to reward
            // The reward pool for this encounter applies as normal
            var options = run.GenerateRewardOptions(wasBoss: false);
            _rewardPanel?.Show("Free Reward!", options, onChosen: () =>
            {
                run.AdvanceEncounter();
                PresentCurrentEncounter();
            });
        }
        else
        {
            // Normal battle encounter: let player confirm before loading
            if (_beginEncounterButton != null)
            {
                _beginEncounterButton.gameObject.SetActive(true);
                _beginEncounterButton.onClick.RemoveAllListeners();
                _beginEncounterButton.onClick.AddListener(LaunchBattle);
            }
            else
            {
                // No button wired — launch immediately
                LaunchBattle();
            }
        }
    }

    // ── Scene loading ─────────────────────────────────────────────────────────

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
            won:             won,
            segmentsCleared: run.Segment,
            boonsEarned:     run.ActiveBoons.Count,
            onReturn:        ReturnToHub
        );
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void HideAllPanels()
    {
        _rewardPanel?.Hide();
        _savePromptPanel?.Hide();
        _runOverPanel?.Hide();
        _beginEncounterButton?.gameObject.SetActive(false);
    }
}
