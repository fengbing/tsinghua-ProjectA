using UnityEngine;

/// <summary>
/// Shows phase-based guide markers on both minimap and fullscreen map.
/// Phase 1: shown after autocruise route completes.
/// Phase 2: replaces phase 1 after fire mission extinguish + narration finish.
/// </summary>
public class MapGuidePhaseController : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private DroneAutocruiseController autocruiseController;
    [SerializeField] private WindowFireMission fireMission;
    [SerializeField] private MissionObjectiveProvider objectiveProvider;

    [Header("Phase 1 Guide (after cruise arrival)")]
    [SerializeField] private string phase1GuideId = "phase-guide";
    [SerializeField] private Transform phase1Target;
    [SerializeField] private Sprite phase1GuideSprite;

    [Header("Phase 2 Guide (after fire + narration)")]
    [SerializeField] private Transform phase2Target;
    [SerializeField] private Sprite phase2GuideSprite;

    private void Awake()
    {
        if (objectiveProvider == null)
            objectiveProvider = FindObjectOfType<MissionObjectiveProvider>();
        if (objectiveProvider == null)
        {
            var go = new GameObject("MissionObjectiveProvider0");
            objectiveProvider = go.AddComponent<MissionObjectiveProvider>();
        }
        if (autocruiseController == null)
            autocruiseController = FindObjectOfType<DroneAutocruiseController>();
        if (fireMission == null)
            fireMission = FindObjectOfType<WindowFireMission>();
    }

    private void OnEnable()
    {
        if (autocruiseController != null)
            autocruiseController.OnAutocruiseRouteCompleted += HandleCruiseRouteCompleted;
        if (fireMission != null)
            fireMission.OnFireExtinguishStarted += HandleFireExtinguishStarted;
        if (fireMission != null)
            fireMission.OnFireExtinguishAndNarrationFinished += HandleFireMissionStageCompleted;
    }

    private void OnDisable()
    {
        if (autocruiseController != null)
            autocruiseController.OnAutocruiseRouteCompleted -= HandleCruiseRouteCompleted;
        if (fireMission != null)
            fireMission.OnFireExtinguishStarted -= HandleFireExtinguishStarted;
        if (fireMission != null)
            fireMission.OnFireExtinguishAndNarrationFinished -= HandleFireMissionStageCompleted;
    }

    private void HandleFireExtinguishStarted()
    {
        if (objectiveProvider == null)
            return;
        objectiveProvider.SetObjectiveState(phase1GuideId, MissionObjectiveState.Hidden);
    }

    private void HandleCruiseRouteCompleted()
    {
        if (objectiveProvider == null || phase1Target == null)
            return;
        objectiveProvider.UpsertObjective(phase1GuideId, phase1Target, MissionObjectiveState.Active, phase1GuideSprite);
    }

    private void HandleFireMissionStageCompleted()
    {
        if (objectiveProvider == null || phase2Target == null)
            return;
        objectiveProvider.UpsertObjective(phase1GuideId, phase2Target, MissionObjectiveState.Active, phase2GuideSprite);
    }
}
