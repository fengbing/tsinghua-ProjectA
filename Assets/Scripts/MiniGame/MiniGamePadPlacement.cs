using UnityEngine;

/// <summary>How <see cref="MiniGameReceiver"/> positions its paired buffer pad when shown.</summary>
public enum MiniGamePadPlacement
{
    [Tooltip("Pad as child: localPosition = value. If pad is not a child, same as world offset interpreted in receiver local axes.")]
    LocalAsChild = 0,
    [Tooltip("World position = receiver.position + value (all axes in world space).")]
    WorldOffsetFromReceiver = 1,
    [Tooltip("World position = value exactly (ignores receiver movement).")]
    FixedWorldPosition = 2,
}
