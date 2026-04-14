namespace Gley.TrafficSystem.Editor
{
    public class GridSetupWindow : UrbanSystem.Editor.GridSetupWindowBase
    {
        public override void DrawInScene()
        {
            if (_viewGrid)
            {
                _gridDrawer.DrawGrid(true);
            }
            base.DrawInScene();
        }
    }
}
