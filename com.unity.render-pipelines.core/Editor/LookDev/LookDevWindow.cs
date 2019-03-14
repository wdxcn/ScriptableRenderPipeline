using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine;
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
        const string showHDRIClass = "showHDRI";

        LayoutContext.Layout layout
        {
            get => LookDev.currentContext.layout.viewLayout;
            set
            {
                if (LookDev.currentContext.layout.viewLayout != value)
                {
                    if (value == LayoutContext.Layout.HorizontalSplit || value == LayoutContext.Layout.VerticalSplit)
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

                    if (views.ClassListContains(LookDev.currentContext.layout.viewLayout.ToString()))
                        views.RemoveFromClassList(LookDev.currentContext.layout.viewLayout.ToString());
                    views.AddToClassList(value.ToString());

                    LookDev.currentContext.layout.viewLayout = value;
                }
            }
        }


        bool showHDRI
        {
            get => LookDev.currentContext.layout.showHDRI;
            set
            {
                if (LookDev.currentContext.layout.showHDRI != value)
                {
                    if (value)
                    {
                        if (!views.ClassListContains(showHDRIClass))
                            views.AddToClassList(showHDRIClass);
                    }
                    else
                    {
                        if (views.ClassListContains(showHDRIClass))
                            views.RemoveFromClassList(showHDRIClass);
                    }

                    LookDev.currentContext.layout.showHDRI = value;
                }
            }
        }

        void OnEnable()
        {
            rootVisualElement.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(LookDevStyle.k_uss));

            var toolbar = new Toolbar() { name = "toolBar" };
            rootVisualElement.Add(toolbar);
            var radiobar = new Toolbar();
            radiobar.Add(new ToolbarButton() { text = "test1" });
            radiobar.Add(new ToolbarButton() { text = "test2" });
            radiobar.Add(new ToolbarButton() { text = "test3" });
            toolbar.Add(radiobar);
            toolbar.Add(new ToolbarSpacer());
            toolbar.Add(new ToolbarButton() { text = "testqlegu" });
            toolbar.Add(new ToolbarSpacer());

            var trueRadioBar = new ToolbarRadio() { name = "toolBar" };
            trueRadioBar.AddRadios(new[] { "0", "1", "2" });
            trueRadioBar.RegisterCallback((ChangeEvent<int> evt) => Debug.Log(evt.newValue));
            toolbar.Add(trueRadioBar);

            views = new VisualElement() { name = "viewContainers" };
            views.AddToClassList(LookDev.currentContext.layout.isMultiView ? twoViewsClass : oneViewClass);
            views.AddToClassList("container");
            if (showHDRI)
                views.AddToClassList(showHDRIClass);
            rootVisualElement.Add(views);

            var viewA = new VisualElement() { name = "viewA" };
            views.Add(viewA);
            var viewB = new VisualElement() { name = "viewB" };
            views.Add(viewB);
            var hdri = new VisualElement() { name = "HDRI" };
            views.Add(hdri);

            viewA.Add(new Image() { image = UnityEngine.Texture2D.whiteTexture, scaleMode = UnityEngine.ScaleMode.ScaleToFit });

            rootVisualElement.Add(new Button(() =>
            {
                if (layout == LayoutContext.Layout.HorizontalSplit)
                    layout = LayoutContext.Layout.FullA;
                else if (layout == LayoutContext.Layout.FullA)
                    layout = LayoutContext.Layout.HorizontalSplit;
            })
            { text = "One/Two views" });

            rootVisualElement.Add(new Button(() => showHDRI ^= true)
            { text = "Show HDRI" });

        }
    }
    
}
