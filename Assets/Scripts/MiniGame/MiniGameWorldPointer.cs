using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Screen → world on Z plane and UI hit test for the 2D mini-game.</summary>
public static class MiniGameWorldPointer
{
    /// <summary>True if the pointer is over a Graphic that can block raycasts (overlay UI).</summary>
    public static bool IsPointerOverBlockingUi()
    {
        if (EventSystem.current == null)
            return false;
        return EventSystem.current.IsPointerOverGameObject();
    }

    /// <param name="planeZ">World Z for gameplay (sprites on this plane).</param>
    public static Vector3 ScreenToWorldOnPlane(Camera cam, float planeZ)
    {
        if (cam == null)
            return Vector3.zero;

        Vector3 m = Input.mousePosition;
        m.z = Mathf.Abs(cam.transform.position.z - planeZ);
        Vector3 w = cam.ScreenToWorldPoint(m);
        w.z = planeZ;
        return w;
    }
}
