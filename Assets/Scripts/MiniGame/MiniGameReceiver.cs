using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Receiver: first orders drone here; only when drone is within <see cref="gateRadius"/> can the buffer pad open or close.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public sealed class MiniGameReceiver : MonoBehaviour
{
    [SerializeField] MiniGameBufferPad pad;
    [SerializeField] MiniGamePadPlacement padPlacement = MiniGamePadPlacement.LocalAsChild;
    [Tooltip("含义见 Pad Placement：本地偏移、相对接收器世界偏移、或固定世界坐标。")]
    [FormerlySerializedAs("padOffsetLocal")]
    [SerializeField] Vector3 padPlacementValue = new Vector3(2f, 0f, 0f);
    [Tooltip("无人机距接收器平面距离 ≤ 此值时，才允许开关缓冲垫。")]
    [SerializeField] float gateRadius = 1.25f;

    void Start()
    {
        if (pad == null)
            return;

        ApplyPadPlacement();
        pad.gameObject.SetActive(false);
    }

    /// <summary>将缓冲垫放到设定位置（显示前会再调用一次）。</summary>
    public void ApplyPadPlacement()
    {
        if (pad == null)
            return;

        switch (padPlacement)
        {
            case MiniGamePadPlacement.LocalAsChild:
                if (pad.transform.parent == transform)
                    pad.transform.localPosition = padPlacementValue;
                else
                    pad.transform.position = transform.TransformPoint(padPlacementValue);
                break;
            case MiniGamePadPlacement.WorldOffsetFromReceiver:
                pad.transform.position = transform.position + padPlacementValue;
                break;
            case MiniGamePadPlacement.FixedWorldPosition:
                pad.transform.position = padPlacementValue;
                break;
        }

        var p = pad.transform.position;
        p.z = transform.position.z;
        pad.transform.position = p;
    }

    public void ActivateFromClick()
    {
        var session = MiniGameSession.Instance;
        var drone = session != null ? session.Drone : null;
        bool droneBeside = drone != null && drone.IsWithinPlanarDistance(transform.position, gateRadius);

        if (pad != null && pad.gameObject.activeInHierarchy)
        {
            if (droneBeside)
            {
                pad.gameObject.SetActive(false);
                session?.PlayClickSound();
            }
            else
            {
                if (drone != null)
                    drone.MoveTo(transform.position);
                session?.PlayClickSound();
            }

            return;
        }

        if (!droneBeside)
        {
            if (drone != null)
                drone.MoveTo(transform.position);
            session?.PlayClickSound();
            return;
        }

        if (drone != null)
            drone.MoveTo(transform.position);

        if (pad != null)
        {
            ApplyPadPlacement();
            pad.gameObject.SetActive(true);
            pad.OnShownByReceiver();
        }

        session?.PlayClickSound();
    }
}
