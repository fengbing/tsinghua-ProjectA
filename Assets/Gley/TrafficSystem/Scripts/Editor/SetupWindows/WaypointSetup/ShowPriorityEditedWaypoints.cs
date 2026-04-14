using Gley.TrafficSystem.Internal;
using UnityEditor;
using UnityEngine;

using SettingsWindowBase = Gley.UrbanSystem.Editor.SettingsWindowBase;
using SetupWindowBase = Gley.UrbanSystem.Editor.SetupWindowBase;
using WindowProperties = Gley.UrbanSystem.Editor.WindowProperties;

namespace Gley.TrafficSystem.Editor
{
    public class ShowPriorityEditedWaypoints : ShowWaypointsTrafficBase
    {
        private readonly float _scrollAdjustment = 221;

        public override SetupWindowBase Initialize(WindowProperties windowProperties, SettingsWindowBase window)
        {
            base.Initialize(windowProperties, window);
            _waypointsOfInterest = _trafficWaypointData.GetPriorityEditedWaypoints();
            _showDeleteButton = true;
            return this;
        }

        protected override void TopPart()
        {
            base.TopPart();
            if (GUILayout.Button("Delete all priority edited waypoints"))
            {
                if (EditorUtility.DisplayDialog("Delete All Waypoints", "Are you sure you want to delete all priority edited waypoints?", "Yes", "No"))
                {
                    foreach (var waypoint in _waypointsOfInterest)
                    {
                        waypoint.priorityLocked = false;
                        EditorUtility.SetDirty(waypoint);
                    }
                    RefreshWaypointsOfInterest();
                }
            }
        }

        public override void DrawInScene()
        {
            _trafficWaypointDrawer.ShowPriorityEditedWaypoints(_editorSave.EditorColors.WaypointColor, _editorSave.EditorColors.PriorityColor);
            base.DrawInScene();
        }


        protected override void ScrollPart(float width, float height)
        {
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, false, false, GUILayout.Width(width - SCROLL_SPACE), GUILayout.Height(height - _scrollAdjustment));
            base.ScrollPart(width, height);
            GUILayout.EndScrollView();
        }

        protected override void DeleteWaypoint(WaypointSettings waypoint)
        {
            base.DeleteWaypoint(waypoint);
            waypoint.priorityLocked = false;
            EditorUtility.SetDirty(waypoint);
            RefreshWaypointsOfInterest();
        }

        protected void RefreshWaypointsOfInterest()
        {
            _trafficWaypointData.LoadAllData();
            _waypointsOfInterest = _trafficWaypointData.GetPriorityEditedWaypoints();
            SceneView.RepaintAll();
        }
    }
}