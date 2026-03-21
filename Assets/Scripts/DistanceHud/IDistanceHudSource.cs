/// <summary>
/// Supplies the distance value (meters) shown by the bottom distance strip. Implement on a MonoBehaviour and assign from <see cref="DistanceHudView"/>.
/// </summary>
public interface IDistanceHudSource
{
    /// <summary>Returns the current distance to display, in meters.</summary>
    float GetDistanceMeters();
}
