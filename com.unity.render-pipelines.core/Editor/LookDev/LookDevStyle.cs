using System;
using UnityEngine;

namespace UnityEditor.Rendering.LookDev
{
    internal class LookDevStyle
    {
        const string k_IconFolder = @"Packages/com.unity.render-pipelines.core/Editor/LookDev/Icons/";
        internal const string k_uss = @"Packages/com.unity.render-pipelines.core/Editor/LookDev/LookDevWindow.uss";

        public static readonly GUIContent WindowTitleAndIcon = EditorGUIUtility.TrTextContentWithIcon("Look Dev", CoreEditorUtils.LoadIcon(k_IconFolder, "LookDevMainIcon"));
    }
}
