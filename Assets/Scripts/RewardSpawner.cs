using UnityEngine;

public class RewardSpawner : MonoBehaviour
{
    [Header("奖励配置")]
    [SerializeField] private GameObject rewardPrefab;

    [SerializeField] private Transform spawnPoint;

    [Header("生成选项")]
    [SerializeField] private bool keepReference = true;

    [Header("视觉效果")]
    [SerializeField] private bool spawnWithParticles = true;
    [SerializeField] private ParticleSystem spawnParticles;

    private GameObject _spawnedReward;
    private DecryptPuzzleSystem _puzzleSystem;

    void Start()
    {
        if (spawnPoint == null)
            spawnPoint = transform;

        _puzzleSystem = GetComponent<DecryptPuzzleSystem>();
        if (_puzzleSystem != null)
        {
            _puzzleSystem.OnPuzzleSolved += OnPuzzleSolved;
        }
    }

    void OnDestroy()
    {
        if (_puzzleSystem != null)
        {
            _puzzleSystem.OnPuzzleSolved -= OnPuzzleSolved;
        }
    }

    void OnPuzzleSolved()
    {
        SpawnReward();
    }

    public void SpawnReward()
    {
        if (rewardPrefab == null)
        {
            Debug.LogWarning("[RewardSpawner] 未指定奖励预设体！", this);
            return;
        }

        if (_spawnedReward != null && keepReference)
        {
            Debug.Log("[RewardSpawner] 奖励已经生成过，跳过。", this);
            return;
        }

        Vector3 position = spawnPoint.position;
        Quaternion rotation = spawnPoint.rotation;

        if (spawnWithParticles && spawnParticles != null)
        {
            var ps = Instantiate(spawnParticles, position, rotation);
            ps.Play();
            Destroy(ps.gameObject, ps.main.duration + ps.main.startLifetime.constantMax);
        }

        _spawnedReward = Instantiate(rewardPrefab, position, rotation);

        Debug.Log($"[RewardSpawner] 奖励已生成: {rewardPrefab.name}", this);
    }

    public GameObject SpawnedReward => _spawnedReward;
    public bool HasSpawned => _spawnedReward != null;
}
