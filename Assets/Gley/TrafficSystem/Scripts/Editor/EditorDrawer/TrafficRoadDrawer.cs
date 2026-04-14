using Gley.TrafficSystem.Internal;

namespace Gley.TrafficSystem.Editor
{
    public class TrafficRoadDrawer : UrbanSystem.Editor.RoadDrawer<TrafficRoadData, Road>
    {
        public TrafficRoadDrawer (TrafficRoadData data):base(data) 
        {
        }
    }
}
