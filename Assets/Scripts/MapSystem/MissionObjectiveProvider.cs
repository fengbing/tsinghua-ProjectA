using System;
using System.Collections.Generic;
using UnityEngine;

public enum MissionObjectiveState
{
    Active,
    Completed,
    Hidden
}

[Serializable]
public class MissionObjectiveEntry
{
    public string id;
    public Transform target;
    public MissionObjectiveState state = MissionObjectiveState.Active;
    public Sprite markerSprite;
}

public class MissionObjectiveProvider : MonoBehaviour
{
    [SerializeField] private List<MissionObjectiveEntry> objectives = new List<MissionObjectiveEntry>();

    public IReadOnlyList<MissionObjectiveEntry> Objectives => objectives;
    public event Action ObjectivesChanged;

    public void SetObjectiveState(string id, MissionObjectiveState state)
    {
        for (int i = 0; i < objectives.Count; i++)
        {
            if (objectives[i].id == id)
            {
                objectives[i].state = state;
                ObjectivesChanged?.Invoke();
                return;
            }
        }
    }

    public void UpsertObjective(string id, Transform target, MissionObjectiveState state, Sprite markerSprite = null)
    {
        if (string.IsNullOrEmpty(id))
            return;

        for (int i = 0; i < objectives.Count; i++)
        {
            if (objectives[i].id == id)
            {
                objectives[i].target = target;
                objectives[i].state = state;
                objectives[i].markerSprite = markerSprite;
                ObjectivesChanged?.Invoke();
                return;
            }
        }

        objectives.Add(new MissionObjectiveEntry
        {
            id = id,
            target = target,
            state = state,
            markerSprite = markerSprite
        });
        ObjectivesChanged?.Invoke();
    }

    public void NotifyObjectivesChanged()
    {
        ObjectivesChanged?.Invoke();
    }
}
