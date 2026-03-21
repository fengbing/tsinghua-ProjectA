using UnityEngine;

/// <summary>
/// Returns a fixed distance; useful for layout tests or cutscenes.
/// </summary>
public class ConstantDistanceHudSource : MonoBehaviour, IDistanceHudSource
{
    [SerializeField] float distanceMeters = 400f;

    public float GetDistanceMeters() => distanceMeters;
}
