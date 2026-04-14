namespace Gley.TrafficSystem.Internal
{
    public class GridEvents
    {
        public delegate void ActiveGridCellsChanged(UrbanSystem.Internal.CellData[] activeCells);
        public static event ActiveGridCellsChanged OnActiveGridCellsChanged;
        public static void TriggerActiveGridCellsChangedEvent(UrbanSystem.Internal.CellData[] activeCells)
        {
            OnActiveGridCellsChanged?.Invoke(activeCells);
        }
    }
}