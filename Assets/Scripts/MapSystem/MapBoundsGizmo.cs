using UnityEngine;

public class MapBoundsGizmo : MonoBehaviour
{
    [SerializeField] private MinimapConfig config;
    [SerializeField] private Color gizmoColor = Color.yellow;

    private void OnDrawGizmosSelected()
    {
        if (config == null) return;
        Vector2 min = config.WorldMin;
        Vector2 max = config.WorldMax;
        Vector3 center = new Vector3((min.x + max.x) * 0.5f, transform.position.y, (min.y + max.y) * 0.5f);
        Vector3 size = new Vector3(max.x - min.x, 0.1f, max.y - min.y);
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireCube(center, size);
    }
}
