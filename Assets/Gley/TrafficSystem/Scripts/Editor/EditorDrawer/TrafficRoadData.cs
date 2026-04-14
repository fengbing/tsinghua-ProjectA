using Gley.TrafficSystem.Internal;

namespace Gley.TrafficSystem.Editor
{
    public class TrafficRoadData : UrbanSystem.Editor.RoadEditorData<Road>
    {
        public override Road[] GetAllRoads()
        {
            return _allRoads;
        }
    }
}
