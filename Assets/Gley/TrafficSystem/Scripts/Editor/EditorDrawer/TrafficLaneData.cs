using Gley.TrafficSystem.Internal;

namespace Gley.TrafficSystem.Editor
{
    public class TrafficLaneData : UrbanSystem.Editor.LaneEditorData<Road, WaypointSettings>
    {
        public TrafficLaneData(UrbanSystem.Editor.RoadEditorData<Road> roadData) : base(roadData)
        {
        }
    }
}