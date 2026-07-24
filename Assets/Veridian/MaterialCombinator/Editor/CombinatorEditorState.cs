#if UNITY_EDITOR
// File: Editor/Combinator/Data/CombinatorEditorData.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Veridian.Perspective.Combinator.Editor
{
    // New Enum: Defines hints for texture color space and type interpretation.
    public enum TextureTypeHint
    {
        Auto,
        [InspectorName("Base Color")]
        BaseColor,
        [InspectorName("Normal Map")]
        NormalMap
    }

    // Serializable structure for individual property state
    [Serializable]
    public class ShaderPropertyState
    {
        public string PropertyName;
        public bool IsEnabled;

        public TextureTypeHint Hint = TextureTypeHint.Auto;
    }

    // Serializable structure for analysis results per shader
    [Serializable]
    public class ShaderAnalysisState
    {

        public string ShaderGUID;

        public string ShaderName;

        public bool IsExpanded = true;

        public List<ShaderPropertyState> Properties = new();
    }


}
#endif