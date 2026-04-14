using UnityEditor;
using UnityEngine;


using SettingsWindowBase = Gley.UrbanSystem.Editor.SettingsWindowBase;
using SetupWindowBase = Gley.UrbanSystem.Editor.SetupWindowBase;
using WindowProperties = Gley.UrbanSystem.Editor.WindowProperties;

namespace Gley.TrafficSystem.Editor
{
    public class RoadSetupWindow : SetupWindowBase
    {
        private string _createRoad;
        private string _connectRoads;
        private string _viewRoads;

        public override SetupWindowBase Initialize(WindowProperties windowProperties, SettingsWindowBase window)
        {
            base.Initialize(windowProperties, window);
            _createRoad = "Create Road";
            _connectRoads = "Connect Roads";
            _viewRoads = "View Roads";
            return this;
        }


        protected override void TopPart()
        {
            base.TopPart();
            EditorGUILayout.LabelField("Select action:");
            EditorGUILayout.Space();

            if (GUILayout.Button(_createRoad))
            {
                _window.SetActiveWindow(typeof(CreateRoadWindow), true);
            }
            EditorGUILayout.Space();

            if (GUILayout.Button(_connectRoads))
            {
                _window.SetActiveWindow(typeof(ConnectRoadsWindow), true);
            }
            EditorGUILayout.Space();

            if (GUILayout.Button(_viewRoads))
            {
                _window.SetActiveWindow(typeof(ViewRoadsWindow), true);
            }
        }
    }
}