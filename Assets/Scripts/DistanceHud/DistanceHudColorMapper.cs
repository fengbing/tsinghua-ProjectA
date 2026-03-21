using UnityEngine;

/// <summary>
/// Maps distance (m) to a base tint: white when far, yellow mid, red close. Thresholds default to 300 / 150 / 50 m.
/// </summary>
public static class DistanceHudColorMapper
{
    public static Color Evaluate(
        float distanceMeters,
        float whiteAboveMeters,
        float yellowBelowMeters,
        float redBelowMeters,
        Color whiteTone,
        Color yellowTone,
        Color redTone)
    {
        float d = distanceMeters;
        if (d >= whiteAboveMeters)
            return whiteTone;

        if (d > yellowBelowMeters)
        {
            float t = (d - yellowBelowMeters) / (whiteAboveMeters - yellowBelowMeters);
            return Color.Lerp(yellowTone, whiteTone, t);
        }

        if (d > redBelowMeters)
        {
            float t = (d - redBelowMeters) / (yellowBelowMeters - redBelowMeters);
            return Color.Lerp(redTone, yellowTone, t);
        }

        return redTone;
    }
}
