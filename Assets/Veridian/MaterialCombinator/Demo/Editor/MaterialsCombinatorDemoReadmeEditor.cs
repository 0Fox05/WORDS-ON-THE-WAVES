#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace Veridian.Perspective.Combinator.Demo.Editor
{
    [CustomEditor(typeof(MaterialsCombinatorDemoReadme))]
    public class MaterialsCombinatorDemoReadmeEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            GUILayout.Space(10);

            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            GUILayout.Label("Veridian Materials Combinator | Welcome Manual", headerStyle);
            GUILayout.Space(15);

            DrawStep("Welcome to the Materials Combinator", "This tool generates highly optimized texture atlases and fused meshes to drastically reduce draw calls. We've provided an interactive demo environment to help you stress test the packer's bounding algorithms and multi-material fusion without needing massive project files.");

            GUILayout.Space(10);

            GUI.backgroundColor = new Color(0.4f, 0.9f, 0.4f);
            if (GUILayout.Button("Open Demo Control Panel", GUILayout.Height(40)))
            {
                MaterialsCombinatorDemoGenerator.ShowWindow();
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(15);

            DrawStep("Workflow 1: Asset Generation & Synthesis", "Use the Control Panel to procedurally synthesize multi-material LOD-equipped test prefabs paired with dynamically sized Power-of-Two textures. These act as authentic assets to properly test the atlas parameters.");

            DrawStep("Workflow 2: Combinator Injection", "Click 'Push Assets to Combinator' to seamlessly inject your generated prefabs into the main tool. Run the analysis and press generate to see submeshes merge and draw calls plummet.");

            GUILayout.Space(10);
        }

        private void DrawStep(string title, string description)
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label(title, EditorStyles.boldLabel);

            GUIStyle wrapStyle = new GUIStyle(EditorStyles.label) { wordWrap = true };
            wrapStyle.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.8f, 0.8f, 0.8f) : new Color(0.2f, 0.2f, 0.2f);

            GUILayout.Label(description, wrapStyle);
            GUILayout.EndVertical();
            GUILayout.Space(5);
        }
    }
}
#endif