using UnityEngine;

[CreateAssetMenu(menuName = "Map System/Minimap Config", fileName = "MinimapConfig")]
public class MinimapConfig : ScriptableObject
{
    [Header("Map Art")]
    [SerializeField] private Sprite mapSprite;
    [SerializeField] private Sprite circularMaskSprite;

    [Header("World Bounds (X/Z)")]
    [SerializeField] private Vector2 worldMin = new Vector2(-250f, -250f);
    [SerializeField] private Vector2 worldMax = new Vector2(250f, 250f);

    [Header("Alignment Calibration")]
    [Tooltip("Rotate map alignment around center to match world forward.")]
    [SerializeField] private float mapRotationDegrees;
    [Tooltip("Shift normalized marker positions after rotation/flip.")]
    [SerializeField] private Vector2 normalizedOffset = Vector2.zero;
    [SerializeField] private bool flipX;
    [SerializeField] private bool flipY;

    public Sprite MapSprite => mapSprite;
    public Sprite CircularMaskSprite => circularMaskSprite;
    public Vector2 WorldMin => worldMin;
    public Vector2 WorldMax => worldMax;
    public float MapRotationDegrees => mapRotationDegrees;
    public Vector2 NormalizedOffset => normalizedOffset;
    public bool FlipX => flipX;
    public bool FlipY => flipY;

    public Vector2 WorldToNormalized(Vector3 worldPosition)
    {
        float width = Mathf.Max(0.0001f, worldMax.x - worldMin.x);
        float height = Mathf.Max(0.0001f, worldMax.y - worldMin.y);
        Vector2 normalized = new Vector2(
            (worldPosition.x - worldMin.x) / width,
            (worldPosition.z - worldMin.y) / height
        );

        // Transform in normalized center space so users can calibrate without changing scene geometry.
        Vector2 centered = normalized - new Vector2(0.5f, 0.5f);
        if (flipX) centered.x = -centered.x;
        if (flipY) centered.y = -centered.y;

        float rad = mapRotationDegrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        Vector2 rotated = new Vector2(
            centered.x * cos - centered.y * sin,
            centered.x * sin + centered.y * cos
        );

        Vector2 calibrated = rotated + new Vector2(0.5f, 0.5f) + normalizedOffset;
        return new Vector2(Mathf.Clamp01(calibrated.x), Mathf.Clamp01(calibrated.y));
    }
}
