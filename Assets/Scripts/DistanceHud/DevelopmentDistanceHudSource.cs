using UnityEngine;

/// <summary>
/// Dev-only distance provider: optional auto drift and keyboard tuning ([ / ] hold, PageUp/PageDown).
/// </summary>
public class DevelopmentDistanceHudSource : MonoBehaviour, IDistanceHudSource
{
    [SerializeField] float distanceMeters = 220f;
    [SerializeField] float metersPerSecondDrift = 0f;
    [SerializeField] bool enableKeyboardTuning = true;
    [SerializeField] float keyboardMetersPerSecond = 80f;

    public float GetDistanceMeters() => Mathf.Max(0f, distanceMeters);

    void Update()
    {
        distanceMeters += metersPerSecondDrift * Time.deltaTime;
        if (!enableKeyboardTuning)
            return;
        float step = keyboardMetersPerSecond * Time.deltaTime;
        if (Input.GetKey(KeyCode.LeftBracket) || Input.GetKey(KeyCode.PageDown))
            distanceMeters -= step;
        if (Input.GetKey(KeyCode.RightBracket) || Input.GetKey(KeyCode.PageUp))
            distanceMeters += step;
    }
}
