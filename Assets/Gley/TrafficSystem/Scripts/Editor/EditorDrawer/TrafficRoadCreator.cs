using Gley.TrafficSystem.Internal;
using UnityEditor;
using UnityEngine;

using MonoBehaviourUtilities = Gley.UrbanSystem.Internal.MonoBehaviourUtilities;
using GleyUtilities = Gley.UrbanSystem.Editor.GleyUtilities;

namespace Gley.TrafficSystem.Editor
{
    public class TrafficRoadCreator : UrbanSystem.Editor.RoadCreator<Road, ConnectionPool, ConnectionCurve>
    {
        public TrafficRoadCreator(UrbanSystem.Editor.RoadEditorData<Road> data) : base(data)
        {
        }

        public Road Create(int nrOfLanes, float laneWidth, float waypointDistance, string prefix, Vector3 firstClick, Vector3 secondClick, int globalMaxSpeed, int nrOfAgents, bool leftSideTraffic, int otherLaneLinkDistance)
        {
            Transform roadParent = MonoBehaviourUtilities.GetOrCreateSceneInstance<ConnectionPool>(TrafficSystemConstants.EditorWaypointsHolder, true).transform;
            int roadNumber = GleyUtilities.GetFreeRoadNumber(roadParent);
            GameObject roadHolder = MonoBehaviourUtilities.CreateGameObject(prefix + "_" + roadNumber, roadParent, firstClick, true);
            roadHolder.transform.SetSiblingIndex(roadNumber);
            var road = roadHolder.AddComponent<Road>();
            road.SetDefaults(nrOfLanes, laneWidth, waypointDistance, otherLaneLinkDistance);
            road.CreatePath(firstClick, secondClick);
            road.SetRoadProperties(globalMaxSpeed, nrOfAgents, leftSideTraffic);
            road.justCreated = true;
            EditorUtility.SetDirty(road);
            AssetDatabase.SaveAssets();
            _data.TriggerOnModifiedEvent();
            return road;
        }
    }
}