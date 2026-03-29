using UnityEngine;

/// <summary>Holds shared references for the BuiltinMiniGame scene (drone, click SFX).</summary>
public sealed class MiniGameSession : MonoBehaviour
{
    public static MiniGameSession Instance { get; private set; }

    [SerializeField] MiniGameDrone drone;
    [SerializeField] AudioSource clickAudio;
    [SerializeField] AudioClip clickClip;

    public MiniGameDrone Drone => drone;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void PlayClickSound()
    {
        if (clickClip == null || clickAudio == null)
            return;
        clickAudio.PlayOneShot(clickClip);
    }
}
