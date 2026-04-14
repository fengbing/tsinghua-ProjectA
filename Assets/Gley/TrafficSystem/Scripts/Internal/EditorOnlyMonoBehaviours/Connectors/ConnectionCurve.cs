using UnityEngine;

using ConnectionCurveBase = Gley.UrbanSystem.Internal.ConnectionCurveBase;
using Path = Gley.UrbanSystem.Internal.Path;

namespace Gley.TrafficSystem.Internal
{
    /// <summary>
    /// Store connection curve parameters
    /// </summary>
    [System.Serializable]
    public class ConnectionCurve : ConnectionCurveBase
    {
        [HideInInspector]
        public string name;
        public Transform holder;
        public Path curve;
        public Road fromRoad;
        public Road toRoad;
        public int fromIndex;
        public int toIndex;

        public bool draw;
        public bool drawWaypoints;
        public Vector3 inPosition;
        public Vector3 outPosition;
        public bool inView;


        public ConnectionCurve(Path curve, Road fromRoad, int fromIndex, Road toRoad, int toIndex, bool draw, Transform holder)
        {
            name = holder.name;
            this.fromIndex = fromIndex;
            this.toIndex = toIndex;
            this.curve = curve;
            this.fromRoad = fromRoad;
            this.toRoad = toRoad;
            this.draw = draw;
            this.holder = holder;
        }

        public bool VerifyAssignments()
        {
            if (holder == null)
                return false;

            if (fromRoad == null)
                return false;

            if (toRoad == null)
                return false;

            if (fromIndex < 0)
                return false;

            if (toIndex < 0)
                return false;

            if (fromRoad.lanes == null)
                return false;

            if (fromRoad.lanes.Count <= fromIndex)
                return false;

            if (toRoad.lanes == null)
                return false;

            if (toRoad.lanes.Count <= toIndex)
                return false;

            return true;
        }


        public WaypointSettings GetOutConnector()
        {
            if (fromRoad == null ||
                fromRoad.lanes == null ||
                fromIndex < 0 ||
                fromIndex >= fromRoad.lanes.Count)
            {
                Debug.LogWarning($"Invalid fromIndex {fromIndex} for road {fromRoad?.name}");
                return null;
            }

            var lane = fromRoad.lanes[fromIndex];

            if (lane.laneEdges.outConnector == null)
            {
                Debug.LogWarning($"No outConnector for lane {fromIndex} in road {fromRoad.name}");
                return null;
            }

            return lane.laneEdges.outConnector as WaypointSettings;
        }



        public WaypointSettings GetInConnector()
        {
            if (toRoad == null ||
                toRoad.lanes == null ||
                toIndex < 0 ||
                toIndex >= toRoad.lanes.Count)

            {
                Debug.LogWarning($"Invalid toIndex {toIndex} for road {toRoad?.name}");
                return null;
            }

            var lane = toRoad.lanes[toIndex];
            if (lane.laneEdges.inConnector == null)
            {
                Debug.LogWarning($"No inConnector for lane {toIndex} in road {toRoad.name}");
                return null;
            }

            return lane.laneEdges.inConnector as WaypointSettings;
        }


        public string GetName()
        {
            return name;
        }


        public Path GetCurve()
        {
            return curve;
        }


        public Vector3 GetOffset()
        {
            return fromRoad.positionOffset;
        }


        public Transform GetHolder()
        {
            return holder;
        }


        public bool ContainsRoad(Road road)
        {
            if (toRoad == road || fromRoad == road)
            {
                return true;
            }
            return false;
        }


        public bool ContainsLane(Road road, int laneIndex)
        {
            if ((fromRoad == road && fromIndex == laneIndex) || toRoad == road && toIndex == laneIndex)
            {
                return true;
            }
            return false;
        }
    }
}