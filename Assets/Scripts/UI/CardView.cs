using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;

/// <summary>
/// Visual representation of a single card.
///
/// The outer RectTransform (this object) is a transparent hitbox that stays
/// at the stable fan position — pointer events fire here, and the pointer can
/// never "fall off" the card due to hover animation.
///
/// The inner _visual child RectTransform holds all artwork/text and is the
/// only thing that lifts or scales on hover.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class CardView : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("References (set by prefab)")]
    [SerializeField] private RectTransform _visual;       // inner child — animates on hover
    [SerializeField] private Image         _background;   // on _visual child
    [SerializeField] private Image         _artPlaceholder;
    [SerializeField] private TMP_Text      _nameText;
    [SerializeField] private TMP_Text      _descText;
    [SerializeField] private TMP_Text      _costText;

    public CardData Data { get; private set; }

    private HandDisplay   _owner;
    private RectTransform _rect;

    private Vector2 _fanPos;
    private float   _fanRot;

    public bool IsHovered  { get; private set; }
    public bool IsSelected { get; private set; }

    private static readonly Color DefaultBg      = new(0.95f, 0.92f, 0.85f, 1f);
    private static readonly Color HoverBg        = new(1.00f, 0.98f, 0.90f, 1f);
    private static readonly Color SelectedBg     = new(0.80f, 0.92f, 1.00f, 1f);
    private static readonly Color UnaffordableBg = new(0.65f, 0.58f, 0.58f, 1f);

    private bool _affordable = true;

    private const float HoverLift    = 40f;
    private const float HoverScale   = 1.08f;
    private const float AnimDuration = 0.15f;

    // -------------------------------------------------------------------------

    public void Init(CardData data, HandDisplay owner)
    {
        Data   = data;
        _owner = owner;
        _rect  = GetComponent<RectTransform>();

        // Reset hover/select state in case this CardView is being reused
        IsHovered   = false;
        IsSelected  = false;
        _affordable = true;
        if (_background != null) _background.color = DefaultBg;
        _rect?.DOKill();
        transform.DOKill();
        if (_visual != null)
        {
            _visual.DOKill();
            _visual.anchoredPosition = Vector2.zero;
            _visual.localScale       = Vector3.one;
        }

        Refresh();
    }

    public void Refresh()
    {
        if (Data == null) return;
        _nameText.text        = Data.CardName;
        _descText.text        = Data.CondensedDescription;
        _artPlaceholder.color = Data.effectFragment != null
            ? Data.effectFragment.effectColor
            : Color.gray;
        if (_costText != null)
            _costText.text = Data.ManaCost.ToString();
    }

    private void OnDestroy()
    {
        _rect?.DOKill();
        _visual?.DOKill();
        transform.DOKill();
    }

    // -------------------------------------------------------------------------
    // Fan layout — called by HandDisplay

    public void SetFanTransform(Vector2 pos, float rotDeg, bool animate)
    {
        _fanPos = pos;
        _fanRot = rotDeg;
        ApplyLayout(animate);
    }

    private void ApplyLayout(bool animate)
    {
        // Outer hitbox: fan position + rotation only, no lift/scale
        // This never moves due to hover, so the pointer stays over it.
        float liftY = IsHovered ? HoverLift : 0f;
        float scale = IsHovered ? HoverScale : 1f;

        if (animate)
        {
            _rect.DOKill();
            transform.DOKill();
            _visual.DOKill();
            _rect.DOAnchorPos(_fanPos, AnimDuration).SetEase(Ease.OutCubic);
            transform.DOLocalRotate(new Vector3(0, 0, _fanRot), AnimDuration).SetEase(Ease.OutCubic);
            // Inner visual lifts and scales
            _visual.DOAnchorPos(new Vector2(0f, liftY), AnimDuration).SetEase(Ease.OutCubic);
            _visual.DOScale(scale, AnimDuration).SetEase(Ease.OutCubic);
        }
        else
        {
            _rect.DOKill();
            _visual.DOKill();
            _rect.anchoredPosition   = _fanPos;
            transform.localRotation  = Quaternion.Euler(0, 0, _fanRot);
            transform.localScale     = Vector3.one;
            _visual.anchoredPosition = new Vector2(0f, liftY);
            _visual.localScale       = Vector3.one * scale;
        }
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        if (!IsHovered)
            _background.color = selected ? SelectedBg : (_affordable ? DefaultBg : UnaffordableBg);
    }

    public void SetAffordable(bool affordable)
    {
        _affordable = affordable;
        if (!IsHovered && !IsSelected)
            _background.color = affordable ? DefaultBg : UnaffordableBg;
    }

    // -------------------------------------------------------------------------
    // Pointer events

    public void OnPointerEnter(PointerEventData _)
    {
        if (_owner == null) return;
        IsHovered = true;
        _background.color = HoverBg;
        _owner.OnCardHoverBegin(this);
        CardTooltip.Instance?.Show(Data, _visual);
    }

    public void OnPointerExit(PointerEventData _)
    {
        if (_owner == null) return;
        IsHovered = false;
        _background.color = IsSelected ? SelectedBg : DefaultBg;
        _owner.OnCardHoverEnd(this);
        CardTooltip.Instance?.Hide();
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (_owner == null) return;
        if (e.button == PointerEventData.InputButton.Left)
            _owner.OnCardClicked(this);
    }
}
