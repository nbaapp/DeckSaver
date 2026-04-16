using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws a line between two canvas points by stretching and rotating a UI Image.
/// Attach to a UI GameObject with an Image component.
/// Call Set() to position and size the line between two anchored positions.
/// </summary>
[RequireComponent(typeof(Image))]
public class MapEdgeView : MonoBehaviour
{
    [SerializeField] private float _lineThickness = 4f;

    private RectTransform _rectTransform;

    private void Awake() => _rectTransform = GetComponent<RectTransform>();

    /// <summary>
    /// Position this edge between two points in the parent's local coordinate space.
    /// posA and posB should be anchored positions within the same parent panel.
    /// </summary>
    public void Set(Vector2 posA, Vector2 posB, Color color)
    {
        GetComponent<Image>().color = color;

        Vector2 direction = posB - posA;
        float   distance  = direction.magnitude;
        float   angle     = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        _rectTransform.anchoredPosition = (posA + posB) * 0.5f;
        _rectTransform.sizeDelta        = new Vector2(distance, _lineThickness);
        _rectTransform.localRotation    = Quaternion.Euler(0f, 0f, angle);
    }
}
