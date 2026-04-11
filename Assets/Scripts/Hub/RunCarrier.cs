/// <summary>
/// Carries all data needed for the current run between scenes.
/// Set by the Hub before loading the Run scene; read by the Run scene
/// and the Battle scene.
/// </summary>
public static class RunCarrier
{
    // ── Legacy field — kept for editor/test scenes that set a deck directly ───
    // In a full run, CurrentRun carries the deck; this is only used by the
    // battle scene's _testDeck fallback path.
    public static DeckData CurrentDeck { get; set; }

    // ── Run state ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Active RunState for the current run. Null outside of a run.
    /// Created by the Hub when the player confirms their deck and starts a run.
    /// </summary>
    public static RunState CurrentRun { get; set; }

    /// <summary>
    /// The RunConfig that governs this run's structure.
    /// Stored separately from RunState so UI can reference it without a live run.
    /// </summary>
    public static RunConfig CurrentConfig { get; set; }

    /// <summary>Clear everything when a run ends (win, loss, or return to hub).</summary>
    public static void ClearRun()
    {
        CurrentRun    = null;
        CurrentConfig = null;
        CurrentDeck   = null;
    }

    // ── Convenience helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Call this from the Hub once the player has confirmed their deck and commander.
    /// Creates a fresh RunState and loads the Run scene.
    /// </summary>
    public static void StartRun(RunConfig config, DeckData deck)
    {
        CurrentConfig = config;
        CurrentRun    = new RunState(config, deck);
        CurrentDeck   = deck; // kept for any legacy references
    }
}
