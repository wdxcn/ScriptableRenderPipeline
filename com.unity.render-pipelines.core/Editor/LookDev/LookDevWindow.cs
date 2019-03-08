namespace UnityEditor.Rendering.LookDev
{
    internal class LookDevWindow : EditorWindow
    {
        void OnEnable()
        {
            titleContent = LookDevStyle.WindowTitleAndIcon;
        }

        private void OnGUI()
        {
            BeginWindows();
            EndWindows();
        }
    }
}
