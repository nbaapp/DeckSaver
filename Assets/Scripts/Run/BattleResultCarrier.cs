/// <summary>
/// Carries the result of a battle across a scene load so the Run scene
/// knows whether the player won or lost when it wakes up.
/// </summary>
public static class BattleResultCarrier
{
    public enum Result { None, Win, Loss }

    public static Result LastResult { get; private set; } = Result.None;

    public static void Set(Result result) => LastResult = result;
    public static void Clear()            => LastResult = Result.None;
}
