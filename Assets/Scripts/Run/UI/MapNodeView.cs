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

    [Header("State Colors")]
    [SerializeField] private Color _colorReachable  = Color.white;
    [SerializeField] private Color _colorUnreachable = new Color(0.5f, 0.5f, 0.5f);
    [SerializeField] private Color _colorVisited    = new Color(0.3f, 0.3f, 0.3f);
    [SerializeField] private Color _colorCurrent    = Color.yellow;
    [SerializeField] private Color _colorBoss       = new Color(1f, 0.3f, 0.3f);

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

        // Color
        Color bg = node.Visited  ? _colorVisited
            : isCurrent          ? _colorCurrent
            : node.Type == NodeType.Boss ? _colorBoss
            : isReachable        ? _colorReachable
                                 : _colorUnreachable;

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
}
