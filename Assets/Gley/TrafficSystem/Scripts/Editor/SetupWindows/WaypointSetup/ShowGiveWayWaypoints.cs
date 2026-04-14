using Gley.TrafficSystem.Internal;
using UnityEditor;
using UnityEngine;

using SettingsWindowBase = Gley.UrbanSystem.Editor.SettingsWindowBase;
using SetupWindowBase = Gley.UrbanSystem.Editor.SetupWindowBase;
using WindowProperties = Gley.UrbanSystem.Editor.WindowProperties;

namespace Gley.TrafficSystem.Editor
{
    public class ShowGiveWayWaypoints : ShowWaypointsTrafficBase
    {
        private readonly float _scrollAdjustment = 221;

        public override SetupWindowBase Initialize(WindowProperties windowProperties, SettingsWindowBase window)
        {
            base.Initialize(windowProperties, window);
            _waypointsOfInterest = _trafficWaypointData.GetGiveWayWaypoints();
            _showDeleteButton = true;
            return this;
        }

        public override void DrawInScene()
        {
            _trafficWaypointDrawer.ShowGiveWayWaypoints(_editorSave.EditorColors.WaypointColor);
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
            waypoint.giveWay = false;
            EditorUtility.SetDirty(waypoint);
            RefreshWaypointsOfInterest();
        }

        protected void RefreshWaypointsOfInterest()
        {
            _trafficWaypointData.LoadAllData();
            _waypointsOfInterest = _trafficWaypointData.GetGiveWayWaypoints();
            SceneView.RepaintAll();
        }
    }
}