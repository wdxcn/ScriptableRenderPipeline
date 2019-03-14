using UnityEngine.Rendering;
using UnityEngine.Rendering.LookDev;

using UnityEditor.UIElements;
using UnityEditorInternal;

namespace UnityEditor.Rendering.LookDev
{
    /// <summary>
    /// Main entry point for scripting LookDev
    /// </summary>
    public class LookDev
    {
        static ILookDevDataProvider dataProvider => RenderPipelineManager.currentPipeline as ILookDevDataProvider;

        /// <summary>
        /// Does LookDev is supported with the current render pipeline?
        /// </summary>
        public static bool supported => dataProvider != null;
        
        [MenuItem("Window/Experimental/NEW Look Dev", false, -1)]
        static void ShowLookDevTool()
        {
            LoadConfig();

            LookDevWindow window = EditorWindow.GetWindow<LookDevWindow>();
            window.titleContent = LookDevStyle.WindowTitleAndIcon;
        }

        const string lastContextSavePath = "Library/LookDevConfig.asset";

        static LookDevContext s_currentContext = UnityEngine.ScriptableObject.CreateInstance<LookDevContext>();

        public static LookDevContext currentContext
        {
            get => s_currentContext;
            set
            {
                s_currentContext = value;
                SaveConfig();
            }
        }

        public static void ResetConfig() => s_currentContext = UnityEngine.ScriptableObject.CreateInstance<LookDevContext>();

        public static void LoadConfig(string path = lastContextSavePath)
        {
            var last = InternalEditorUtility.LoadSerializedFileAndForget(path)?[0] as LookDevContext;
            if (last != null && !last.Equals(null))
                s_currentContext = ((LookDevContext)last);
        }

        public static void SaveConfig(string path = lastContextSavePath)
            => InternalEditorUtility.SaveToSerializedFileAndForget(new[] { s_currentContext }, path, true);
    }
}
