using UnityEngine.Rendering;
using UnityEngine.Rendering.LookDev;

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
            EditorWindow.GetWindow<LookDevWindow>();
        }
    }
}
