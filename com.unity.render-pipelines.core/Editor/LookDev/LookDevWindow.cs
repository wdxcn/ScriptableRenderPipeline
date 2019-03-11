using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace UnityEditor.Rendering.LookDev
{

    public enum Layout { ViewA, ViewB, HorizontalSplit, VerticalSplit, CustomSplit, CustomCircular }

    internal class LookDevWindow : EditorWindow
    {
        VisualElement views;
        VisualElement environment;
        
        const string oneViewClass = "oneView";
        const string twoViewsClass = "twoViews";
        
        LookDevContext.Display layout
        {
            get => LookDev.currentContext.displayLayout;
            set
            {
                if (LookDev.currentContext.displayLayout != value)
                {
                    if (value == LookDevContext.Display.HorizontalSplit || value == LookDevContext.Display.VerticalSplit)
                    {
                        if (views.ClassListContains(oneViewClass))
                        {
                            views.RemoveFromClassList(oneViewClass);
                            views.AddToClassList(twoViewsClass);
                        }
                    }
                    else
                    {
                        if (views.ClassListContains(twoViewsClass))
                        {
                            views.RemoveFromClassList(twoViewsClass);
                            views.AddToClassList(oneViewClass);
                        }
                    }

                    if (views.ClassListContains(LookDev.currentContext.displayLayout.ToString()))
                        views.RemoveFromClassList(LookDev.currentContext.displayLayout.ToString());
                    views.AddToClassList(value.ToString());

                    var tmpContext = LookDev.currentContext;
                    tmpContext.displayLayout = value;
                    LookDev.currentContext = tmpContext;
                }
            }
        }



        void OnEnable()
        {
            //titleContent = LookDevStyle.WindowTitleAndIcon;
            
            rootVisualElement.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(LookDevStyle.k_uss));

            views = new VisualElement() { name = "viewContainers" };
            views.AddToClassList(layout == LookDevContext.Display.HorizontalSplit || layout == LookDevContext.Display.VerticalSplit ? "twoViews" : "oneView");
            views.AddToClassList("container");
            rootVisualElement.Add(views);
            var viewA = new VisualElement() { name = "viewA" };
            views.Add(viewA);
            views.Add(new VisualElement() { name = "viewB" });

            rootVisualElement.Add(new Button(() =>
            {
                if (layout  == LookDevContext.Display.HorizontalSplit)
                    layout = LookDevContext.Display.FullA;
                else if (layout == LookDevContext.Display.FullA)
                    layout = LookDevContext.Display.HorizontalSplit;
            }) { text = "One/Two views" });
        }
    }
}
