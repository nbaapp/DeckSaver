using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Shows the count for one pile and opens the overlay when clicked.
/// Type and label text are set in the scene. Call Init() at runtime.
/// </summary>
public class PileButton : MonoBehaviour, IPointerClickHandler
{
    public enum PileType { Draw, Discard }

    [Header("Settings (set by builder)")]
    [SerializeField] private PileType _type;

    [Header("References (set by builder)")]
    [SerializeField] private TMP_Text _countText;

    private PileOverlay _overlay;

    // -------------------------------------------------------------------------

    public void Init(PileOverlay overlay)
    {
        _overlay = overlay;
        BattleDeck.Instance.OnHandChanged  += UpdateCount;
        BattleDeck.Instance.OnDeckShuffled += UpdateCount;
        UpdateCount();
    }

    private void OnDestroy()
    {
        if (BattleDeck.Instance == null) return;
        BattleDeck.Instance.OnHandChanged  -= UpdateCount;
        BattleDeck.Instance.OnDeckShuffled -= UpdateCount;
    }

    // -------------------------------------------------------------------------

    private void UpdateCount()
    {
        if (_countText == null) return;
        int count = _type == PileType.Draw
            ? BattleDeck.Instance.DrawPile.Count
            : BattleDeck.Instance.DiscardPile.Count;
        _countText.text = count.ToString();
    }

    public void OnPointerClick(PointerEventData _)
    {
        var cards = _type == PileType.Draw
            ? BattleDeck.Instance.DrawPile
            : BattleDeck.Instance.DiscardPile;
        _overlay.Show(cards, _type == PileType.Draw ? "Draw Pile" : "Discard Pile");
    }
}
