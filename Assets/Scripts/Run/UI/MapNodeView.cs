using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Visual representation of one map node.
/// Displays the node type, reachability state, and visited state.
/// Fires an onClick callback when the player clicks a reachable node.
/// </summary>
public class MapNodeView : MonoBehaviour
{
    [SerializeField] private Button          _button;
    [SerializeField] private TextMeshProUGUI _typeLabel;
    [SerializeField] private Image           _background;
    [SerializeField] private Image           _icon;

    [Header("Node Type Icons")]
    [SerializeField] private Sprite _iconStart;
    [SerializeField] private Sprite _iconStandardConflict;
    [SerializeField] private Sprite _iconHardConflict;
    [SerializeField] private Sprite _iconBoss;
    [SerializeField] private Sprite _iconCamp;
    [SerializeField] private Sprite _iconShop;
    [SerializeField] private Sprite _iconEvent;

    [Header("Node Type Colors")]
    [SerializeField] private Color _colorStart            = new Color(0.6f, 0.6f, 0.6f);
    [SerializeField] private Color _colorStandardConflict = new Color(0.3f, 0.6f, 1.0f);
    [SerializeField] private Color _colorHardConflict     = new Color(1.0f, 0.5f, 0.1f);
    [SerializeField] private Color _colorBoss             = new Color(1.0f, 0.2f, 0.2f);
    [SerializeField] private Color _colorCamp             = new Color(0.3f, 0.8f, 0.4f);
    [SerializeField] private Color _colorShop             = new Color(0.9f, 0.8f, 0.2f);
    [SerializeField] private Color _colorEvent            = new Color(0.7f, 0.3f, 0.9f);

    [Header("State Overrides")]
    [SerializeField] private Color _colorVisited = new Color(0.25f, 0.25f, 0.25f);
    [SerializeField] private Color _colorCurrent = Color.yellow;

    private Action _onClick;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Configure this view for the given node.
    /// isReachable: player can click it.
    /// isCurrent: the node the player is currently standing on.
    /// </summary>
    public void Setup(MapNode node, bool isReachable, bool isCurrent, Action onClick)
    {
        _onClick = onClick;

        // Label
        if (_typeLabel)
            _typeLabel.text = NodeLabel(node.Type);

        // Icon — use type sprite if assigned, otherwise hide
        if (_icon != null)
        {
            var sprite = NodeIcon(node.Type);
            _icon.sprite  = sprite;
            _icon.enabled = sprite != null;
        }

        // Color — visited/current override type color; reachability does not affect color
        Color bg = node.Visited ? _colorVisited
            : isCurrent         ? _colorCurrent
                                : NodeTypeColor(node.Type);

        if (_background) _background.color = bg;

        // Interactability
        if (_button != null)
        {
            _button.interactable = isReachable && !node.Visited && !isCurrent;
            _button.onClick.RemoveAllListeners();
            if (isReachable && !node.Visited && !isCurrent)
                _button.onClick.AddListener(() => _onClick?.Invoke());
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string NodeLabel(NodeType type) => type switch
    {
        NodeType.Start            => "Start",
        NodeType.StandardConflict => "Battle",
        NodeType.HardConflict     => "Hard",
        NodeType.Boss             => "Boss",
        NodeType.Camp             => "Camp",
        NodeType.Shop             => "Shop",
        NodeType.Event            => "Event",
        _                         => "?",
    };

    private Sprite NodeIcon(NodeType type) => type switch
    {
        NodeType.Start            => _iconStart,
        NodeType.StandardConflict => _iconStandardConflict,
        NodeType.HardConflict     => _iconHardConflict,
        NodeType.Boss             => _iconBoss,
        NodeType.Camp             => _iconCamp,
        NodeType.Shop             => _iconShop,
        NodeType.Event            => _iconEvent,
        _                         => null,
    };

    private Color NodeTypeColor(NodeType type) => type switch
    {
        NodeType.Start            => _colorStart,
        NodeType.StandardConflict => _colorStandardConflict,
        NodeType.HardConflict     => _colorHardConflict,
        NodeType.Boss             => _colorBoss,
        NodeType.Camp             => _colorCamp,
        NodeType.Shop             => _colorShop,
        NodeType.Event            => _colorEvent,
        _                         => Color.white,
    };
}
