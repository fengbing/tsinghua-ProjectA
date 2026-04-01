using UnityEngine;

public class PuzzleTrigger : MonoBehaviour
{
    [Header("触发时显示的面板")]
    [SerializeField] private DecryptPuzzleUI puzzleUI;

    [Header("可选：触发完成后销毁自己")]
    [SerializeField] private bool destroyAfterTrigger = false;

    private bool _hasTriggered;

    private void OnTriggerEnter(Collider other)
    {
        if (_hasTriggered)
            return;

        if (other.CompareTag("Player") || other.GetComponent<PlaneController>() != null)
        {
            TriggerPuzzle();
        }
    }

    void TriggerPuzzle()
    {
        if (puzzleUI != null)
        {
            puzzleUI.Show();
            puzzleUI.OnDecryptPuzzleSolved += OnPuzzleSolved;
        }
    }

    void OnPuzzleSolved()
    {
        _hasTriggered = true;

        if (puzzleUI != null)
        {
            puzzleUI.OnDecryptPuzzleSolved -= OnPuzzleSolved;
        }

        if (destroyAfterTrigger)
        {
            Destroy(gameObject);
        }
    }
}
