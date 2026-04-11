using UnityEngine;
using TMPro;

/// <summary>
/// Full-description tooltip shown when hovering a card, positioned to the left.
/// Must start ACTIVE in the scene so Awake() sets Instance before any card hovers.
/// Start() hides it immediately after initialisation — no visible flash.
/// </summary>
public class CardTooltip : MonoBehaviour
{
    public static CardTooltip Instance { get; private set; }

    [Header("References (set by builder)")]
    [SerializeField] private Canvas   _canvas;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _bodyText;

    private RectTransform _rect;

    private void Awake()
    {
        Instance = this;
        _rect    = GetComponent<RectTransform>();
        // pivot (1, 0.5): right edge anchors to the card's left side, grows leftward
        _rect.pivot = new Vector2(1f, 0.5f);
    }

    // Hide after Awake so Instance is set, but before any input can fire
    private void Start() => Hide();

    // -------------------------------------------------------------------------

    public void Show(CardData card, RectTransform cardRect)
    {
        if (card == null) return;

        _titleText.text = card.CardName;
        _bodyText.text  = card.FullDescription;
        gameObject.SetActive(true);

        // Position to the left of the card, vertically centred on it
        Vector3[] corners = new Vector3[4];
        cardRect.GetWorldCorners(corners);
        // corners[0] = bottom-left, corners[1] = top-left
        Vector3 leftCenter = (corners[0] + corners[1]) * 0.5f;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvas.GetComponent<RectTransform>(),
            new Vector2(leftCenter.x, leftCenter.y),
            _canvas.worldCamera,
            out Vector2 localPos);

        _rect.anchoredPosition = localPos - Vector2.right * 10f;
        ClampToCanvas();
    }

    public void Hide() => gameObject.SetActive(false);

    private void ClampToCanvas()
    {
        var   canvasRect  = _canvas.GetComponent<RectTransform>();
        var   pos         = _rect.anchoredPosition;
        float w           = _rect.sizeDelta.x;
        float h           = _rect.sizeDelta.y;
        float canvasHalfW = canvasRect.rect.width  * 0.5f;
        float canvasHalfH = canvasRect.rect.height * 0.5f;
        // pivot is (1, 0.5): right edge at pos.x, left edge at pos.x - w, centre-y at pos.y
        pos.x = Mathf.Clamp(pos.x, -canvasHalfW + w, canvasHalfW);
        pos.y = Mathf.Clamp(pos.y, -canvasHalfH + h * 0.5f, canvasHalfH - h * 0.5f);
        _rect.anchoredPosition = pos;
    }
}
