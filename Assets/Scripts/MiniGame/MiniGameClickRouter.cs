using UnityEngine;

/// <summary>Resolves overlap priority: receiver → playfield (drone move). Buffer pad is non-interactive by default.</summary>
public sealed class MiniGameClickRouter : MonoBehaviour
{
    [SerializeField] Camera worldCamera;
    [SerializeField] float gameplayPlaneZ;

    static int SortingOrderForCollider(Collider2D col)
    {
        if (col == null)
            return int.MinValue;
        var sr = col.attachedRigidbody != null
            ? col.attachedRigidbody.GetComponent<SpriteRenderer>()
            : col.GetComponent<SpriteRenderer>();
        return sr != null ? sr.sortingOrder : int.MinValue;
    }

    void Update()
    {
        if (!Input.GetMouseButtonDown(0))
            return;
        if (MiniGameWorldPointer.IsPointerOverBlockingUi())
            return;

        var cam = worldCamera != null ? worldCamera : Camera.main;
        if (cam == null)
            return;

        var world = MiniGameWorldPointer.ScreenToWorldOnPlane(cam, gameplayPlaneZ);
        var hits = Physics2D.OverlapPointAll(world);

        MiniGameReceiver recvHit = null;
        int recvOrder = int.MinValue;
        var hasPlayfield = false;

        foreach (var col in hits)
        {
            if (col == null)
                continue;

            if (col.GetComponent<MiniGamePlayfield>() != null)
                hasPlayfield = true;

            var r = col.GetComponent<MiniGameReceiver>()
                    ?? col.GetComponentInParent<MiniGameReceiver>();
            if (r == null || !r.isActiveAndEnabled)
                continue;

            int o = SortingOrderForCollider(col);
            if (o >= recvOrder)
            {
                recvOrder = o;
                recvHit = r;
            }
        }

        if (recvHit != null)
        {
            recvHit.ActivateFromClick();
            return;
        }

        if (!hasPlayfield)
            return;

        var session = MiniGameSession.Instance;
        if (session != null && session.Drone != null)
            session.Drone.MoveTo(world);
        session?.PlayClickSound();
    }
}
