using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Full-screen overlay showing the contents of a pile.
/// Hierarchy is set up in the scene by BattleUIBuilder. Hidden at Start.
/// Click anywhere on the backdrop to dismiss.
/// </summary>
public class PileOverlay : MonoBehaviour, IPointerClickHandler
{
    [Header("References (set by builder)")]
    [SerializeField] private TMP_Text      _titleText;
    [SerializeField] private RectTransform _cardContainer;
    [SerializeField] private CardView      _cardViewPrefab;

    private void Start() => gameObject.SetActive(false);

    // -------------------------------------------------------------------------

    public void Show(IReadOnlyList<CardData> cards, string title)
    {
        _titleText.text = $"{title}  ({cards.Count})";

        for (int i = _cardContainer.childCount - 1; i >= 0; i--)
            Destroy(_cardContainer.GetChild(i).gameObject);

        foreach (var card in cards)
        {
            var view = Instantiate(_cardViewPrefab, _cardContainer);
            view.Init(card, owner: null); // read-only — no hover/click handling
        }

        gameObject.SetActive(true);
    }

    public void OnPointerClick(PointerEventData _) => gameObject.SetActive(false);
}
