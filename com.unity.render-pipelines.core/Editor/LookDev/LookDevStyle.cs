using System;
using UnityEngine;

namespace UnityEditor.Rendering.LookDev
{
    internal class LookDevStyle
    {
        const string k_IconFolder = @"Packages/com.unity.render-pipelines.core/Editor/LookDev/Icons/";
        
        public static readonly GUIContent WindowTitleAndIcon = EditorGUIUtility.TrTextContentWithIcon("Look Dev", CoreEditorUtils.LoadIcon(k_IconFolder, "LookDevMainIcon"));
    }
}
