using UnityEngine;

namespace UnityEditor.Rendering.LookDev
{
    public struct LookDevContext
    {
        public enum View { A, B };
        public enum Display { FullA, FullB, HorizontalSplit, VerticalSplit, CustomSplit, CustomCircular }

        public struct ViewContext
        {
            public readonly View view;
            public ViewContext(View view) => this.view = view;

            //[TODO: add object]
            //[TODO: add object position]
            //[TODO: add camera frustum]
            //[TODO: add HDRI]
        }

        public static LookDevContext @default = new LookDevContext
        {
            viewA = new ViewContext(View.A),
            viewB = new ViewContext(View.B)
        };

        public Display displayLayout;

        public ViewContext viewA { get; private set; }
        public ViewContext viewB { get; private set; }

        //[TODO: add tool position ?]
    }

    public class LookDevSave : ScriptableObject
    {
        public LookDevContext context;
    }
}
