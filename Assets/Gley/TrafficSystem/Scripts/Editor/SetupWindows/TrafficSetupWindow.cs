using UnityEditor;

using SettingsWindowBase = Gley.UrbanSystem.Editor.SettingsWindowBase;
using SetupWindowBase = Gley.UrbanSystem.Editor.SetupWindowBase;
using WindowProperties = Gley.UrbanSystem.Editor.WindowProperties;
using SettingsLoader = Gley.UrbanSystem.Editor.SettingsLoader;

namespace Gley.TrafficSystem.Editor
{
    public class TrafficSetupWindow : SetupWindowBase
    {
        protected TrafficSettingsWindowData _editorSave;


        public override SetupWindowBase Initialize(WindowProperties windowProperties, SettingsWindowBase window)
        {
            base.Initialize(windowProperties, window);
            _editorSave = new SettingsLoader(TrafficSystemConstants.windowSettingsPath).LoadSettingsAsset<TrafficSettingsWindowData>();
            return this;
        }


        public override void DestroyWindow()
        {
            EditorUtility.SetDirty(_editorSave);
            base.DestroyWindow();
        }
    }
}