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

    public void NotifyObjectivesChanged()
    {
        ObjectivesChanged?.Invoke();
    }
}
