#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using System.Linq;
using System;
using System.Collections.Generic;

namespace Veridian.Perspective.Combinator.Editor
{
    public class MaterialsCombinatorWindow : EditorWindow
    {
        #region Serialized State (Persistence)

        [SerializeField, Tooltip("Prefabs whose materials will be combined and meshes merged.")]
        private List<GameObject> inputPrefabs = new();

        [SerializeField] private bool showSettings = true;
        [SerializeField] private bool showInputs = true;
        [SerializeField] private bool showAnalysis = true;

        [SerializeField, Tooltip("Directory where the generated assets will be saved.")]
        private string saveDirectory = "Assets/VeridianData/MaterialsCombinator";

        [SerializeField, Tooltip("Suffix appended to the generated prefabs and assets.")]
        private string assetSuffix = "_Atlased";

        [SerializeField, Tooltip("Padding in pixels between textures in the atlas to prevent bleeding.")]
        private int padding = 0;

        [SerializeField, Tooltip("Whether to include and process LODGroups.")]
        private bool processLODs = true;

        [SerializeField, Range(0.1f, 1.0f), Tooltip("Scale down factor for the final generated atlases (e.g. 0.5 for half resolution).")]
        private float resizeScale = 1.0f;

        [SerializeField, Tooltip("Sizing mode for the generated atlases.")]
        private AtlasSizingMode sizingMode = AtlasSizingMode.AutomaticRectangularPOT;

        [SerializeField, Tooltip("Custom width of the atlas. Must be a multiple of 8.")]
        private int customAtlasWidth = 1024;

        [SerializeField, Tooltip("Custom height of the atlas. Must be a multiple of 8.")]
        private int customAtlasHeight = 1024;

        [SerializeField, Tooltip("Allow packer to rotate textures 90 degrees to save space. Turn off to strictly preserve directionality.")]
        private bool allowRotation = true;

        [SerializeField, Tooltip("Auto-Generate: Creates a combined material.\nOutput Raw Atlases Only: Packs textures but leaves output material slots empty for custom shader setup.")]
        private MaterialGenerationMode generationMode = MaterialGenerationMode.AutoGenerate;

        [SerializeField, Tooltip("Heuristic to determine how textures are arranged within the atlas bounds.")]
        private FreeRectChoiceHeuristic packingHeuristic = FreeRectChoiceHeuristic.BestShortSideFit;

        [SerializeField, Tooltip("Maximum allowed atlas size when using Auto Power of Two.")]
        private int maxAtlasSize = 8192;

        [SerializeField, Tooltip("Minimum allowed atlas size when using Auto Power of Two.")]
        private int minAtlasSize = 128;

        [SerializeField, Tooltip("Optionally merge submeshes that share the same material atlas. Disabled by default to preserve original mesh structure.")]
        private bool combineSubmeshes = false;

        [Min(1)]
        [SerializeField, Tooltip("Resolution (in pixels) of the fallback texture generated when a map is missing.")]
        private int fallbackTextureSize = 4;

        [SerializeField, Tooltip("Temporarily downscale large textures before packing to fit within max atlas size and prevent hard failures.")]
        private bool autoDownscaleLargeInputs = true;

        [SerializeField]
        private List<ShaderAnalysisState> analysisResults = new();

        [NonSerialized]
        private SerializedProperty analysisResultsProperty;

        #endregion

        #region Transient State (Runtime Only)

        [NonSerialized]
        private CombinatorEditorProcessor combinatorProcessor;

        [NonSerialized] private bool isProcessing = false;
        [NonSerialized] private bool isAnalyzing = false;

        [NonSerialized] private Vector2 scrollPosition;
        [NonSerialized] private Vector2 analysisScrollPosition;



        private static string lastCreatedFolderPath
        {
            get => SessionState.GetString("Veridian_Combinator_LastPath", null);
            set
            {
                if (string.IsNullOrEmpty(value)) SessionState.EraseString("Veridian_Combinator_LastPath");
                else SessionState.SetString("Veridian_Combinator_LastPath", value);
            }
        }

        public static readonly Color ColorGenerate = new Color(0.4f, 0.9f, 0.4f, 1f);

        [NonSerialized]
        private SerializedObject serializedWindow;
        [NonSerialized]
        private SerializedProperty inputPrefabsProperty;
      
        #endregion


        /// <summary>
        /// Populates the tool's input list programmatically.
        /// Primarily used by the Demo Environment to seamlessly load test prefabs.
        /// </summary>
        public void InjectDemoPrefabs(List<GameObject> prefabs)
        {
            if (prefabs == null || prefabs.Count == 0) return;

            Undo.RecordObject(this, "Inject Demo Prefabs");

            if (serializedWindow == null) InitializeSerialization();

            if (serializedWindow != null && inputPrefabsProperty != null)
            {
                serializedWindow.Update();
                inputPrefabsProperty.ClearArray();
                for (int i = 0; i < prefabs.Count; i++)
                {
                    inputPrefabsProperty.InsertArrayElementAtIndex(i);
                    inputPrefabsProperty.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
                }

                if (analysisResultsProperty != null)
                {
                    analysisResultsProperty.ClearArray();
                }

                serializedWindow.ApplyModifiedProperties();
            }
            else
            {
                inputPrefabs.Clear();
                inputPrefabs.AddRange(prefabs);
                analysisResults.Clear();
            }

            showInputs = true;
            showAnalysis = true;
            Repaint();
        }


        [MenuItem("Tools/Veridian/Materials Combinator/CombinatorWindow", false, 51)]
        public static void ShowWindow()
        {
            GetWindow<MaterialsCombinatorWindow>("Materials Combinator");
        }

        #region Unity Lifecycle

        private void OnEnable()
        {
            InitializeProcessor();
            InitializeSerialization();

            isProcessing = false;
            isAnalyzing = false;
        }

        private void OnDisable()
        {
            CleanupProcessor();
        }

        private void OnGUI()
        {
            if (combinatorProcessor == null) InitializeProcessor();
            if (serializedWindow == null || serializedWindow.targetObject == null) InitializeSerialization();

            if (serializedWindow == null || inputPrefabsProperty == null || analysisResultsProperty == null)
            {
                EditorGUILayout.HelpBox("Failed to initialize editor serialization. Please try reopening the window.", MessageType.Error);
                return;
            }

            serializedWindow.Update();

            DrawHeader();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            DrawQuickStartGuide();

            bool uiDisabled = isProcessing || isAnalyzing;
            EditorGUI.BeginDisabledGroup(uiDisabled);

            DrawInputsUI();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            DrawSettingsUI();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            DrawAnalysisUI();
            EditorGUILayout.Space(20);
            DrawExecutionUI();
            EditorGUILayout.Space(10);
            DrawUndoUI();

            EditorGUI.EndDisabledGroup();

            if (uiDisabled)
            {
                EditorGUILayout.HelpBox(isProcessing ? "Combinator process is running..." : "Analysis is running...", MessageType.Info);
            }

            DrawPromotionalFooter();

            EditorGUILayout.EndScrollView();

            serializedWindow.ApplyModifiedProperties();
        }

        #endregion

        #region Initialization and Cleanup

        private void InitializeProcessor()
        {
            combinatorProcessor ??= new CombinatorEditorProcessor();

            CleanupProcessor();
            combinatorProcessor.OnProgress += OnCombinatorProgress;
            combinatorProcessor.OnComplete += OnCombinatorComplete;
        }

        private void CleanupProcessor()
        {
            if (combinatorProcessor != null)
            {
                combinatorProcessor.OnProgress -= OnCombinatorProgress;
                combinatorProcessor.OnComplete -= OnCombinatorComplete;
            }
        }

        private void InitializeSerialization()
        {
            try
            {
                serializedWindow = new SerializedObject(this);

                inputPrefabsProperty = serializedWindow.FindProperty(nameof(inputPrefabs));
                analysisResultsProperty = serializedWindow.FindProperty(nameof(analysisResults));

                if (inputPrefabsProperty == null || analysisResultsProperty == null)
                {
                    Debug.LogError("[MaterialsCombinatorWindow] Failed to bind essential properties. Check field names and serialization.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MaterialsCombinatorWindow] Error during serialization initialization: {ex.Message}");
                serializedWindow = null;
            }
        }

        #endregion

        #region UI Brand Standardization

        private void DrawHeader()
        {
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Materials Combinator", new GUIStyle(EditorStyles.largeLabel) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter });
            EditorGUILayout.LabelField("Procedural Texture Atlasing & Mesh Fusion", new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter });
            GUILayout.Space(10);

            Rect rect = EditorGUILayout.GetControlRect(false, 1);
            rect.height = 1;
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1));
            GUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUI.BeginDisabledGroup(isProcessing || isAnalyzing);
            if (GUILayout.Button("Reset Tool", GUILayout.Width(100)))
            {
                ResetTool();
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(5);
        }

        private void DrawQuickStartGuide()
        {
            string prefKey = "Veridian_Combinator_QS";
            bool showHelp = EditorPrefs.GetBool(prefKey, true);

            GUILayout.Space(5);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUI.BeginChangeCheck();
            GUIStyle foldoutStyle = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
            bool newShowHelp = EditorGUILayout.Foldout(showHelp, " Quick Start Guide & Limitations", true, foldoutStyle);

            if (EditorGUI.EndChangeCheck())
                EditorPrefs.SetBool(prefKey, newShowHelp);

            if (newShowHelp)
            {
                GUILayout.Space(5);
                EditorGUILayout.LabelField("1. Add Prefabs:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Drag and drop GameObjects from your Project view into the Input Prefabs list.", EditorStyles.wordWrappedLabel);
                GUILayout.Space(5);
                EditorGUILayout.LabelField("2. Analyze & Select:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Click 'Analyze Inputs' to dynamically scan for properties, then select the channels to atlas.", EditorStyles.wordWrappedLabel);
                GUILayout.Space(5);
                EditorGUILayout.LabelField("3. Generate:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Configure your output settings and click 'Generate Optimized Prefabs' to combine meshes and materials.", EditorStyles.wordWrappedLabel);
                GUILayout.Space(5);

                // ADDED: Best Practices / Limitations
                EditorGUILayout.LabelField("Best Practices:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("• Static Props: Ideal for environment assets, modular buildings, and props.\n• Skinned Meshes: You may atlas a single character, but do NOT attempt to combine multiple different animated characters together.", EditorStyles.wordWrappedLabel);
            }
            EditorGUILayout.EndVertical();
            GUILayout.Space(5);
        }

        private void DrawEducationalUpsell()
        {
            string eduPrefKey = "Veridian_Combinator_Edu";
            bool showEdu = EditorPrefs.GetBool(eduPrefKey, true);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.BeginChangeCheck();
            GUIStyle eduFoldoutStyle = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
            showEdu = EditorGUILayout.Foldout(showEdu, " Optimization Trade-offs", true, eduFoldoutStyle);
            if (EditorGUI.EndChangeCheck()) EditorPrefs.SetBool(eduPrefKey, showEdu);

            if (showEdu)
            {
                EditorGUILayout.LabelField("Atlasing drastically reduces Draw Calls by batching materials together. However, depending on atlas size and padding, it can increase overall VRAM usage. Use judiciously.", EditorStyles.wordWrappedLabel);
                GUILayout.Space(5);
                EditorGUI.BeginDisabledGroup(true);
                GUILayout.Button("Pro Version: True Structural Mesh Fusion (Coming Soon)", GUILayout.Height(25));
                EditorGUI.EndDisabledGroup();
            }
            EditorGUILayout.EndVertical();
            GUILayout.Space(10);
        }

        private void DrawPromotionalFooter()
        {
            GUILayout.Space(15);
            bool isPro = EditorGUIUtility.isProSkin;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            titleStyle.normal.textColor = isPro ? new Color(0.6f, 0.8f, 1f) : new Color(0.1f, 0.3f, 0.5f);

            GUIStyle descStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true, fontSize = 11 };
            descStyle.normal.textColor = isPro ? new Color(0.8f, 0.8f, 0.8f) : new Color(0.2f, 0.2f, 0.2f);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Enjoying the Veridian Materials Combinator?", titleStyle);
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Search for 'Veridian' on the Asset Store for our premium developer utilities!", descStyle);
            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            Color oldColor = GUI.backgroundColor;
            GUI.backgroundColor = isPro ? new Color(0.2f, 0.5f, 0.8f) : new Color(0.3f, 0.6f, 0.9f);

            if (GUILayout.Button("View Publisher Page", new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, fontSize = 12 }, GUILayout.Height(30), GUILayout.Width(200)))
            {
                Application.OpenURL("https://assetstore.unity.com/publishers/120204");
            }

            GUI.backgroundColor = oldColor;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
            EditorGUILayout.EndVertical();
        }
        #endregion

        #region UI Drawing Sections

        private void DrawInputsUI()
        {
            var showInputsProp = serializedWindow.FindProperty(nameof(showInputs));
            showInputsProp.boolValue = EditorGUILayout.Foldout(showInputsProp.boolValue, "Input Prefabs", true, EditorStyles.foldoutHeader);

            if (!showInputsProp.boolValue) return;

            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Add prefabs whose materials you want to combine.", EditorStyles.wordWrappedLabel);

            DrawPrefabDropZone();

            if (inputPrefabsProperty != null)
            {
                EditorGUILayout.Space();

                EditorGUI.BeginChangeCheck();

                EditorGUILayout.PropertyField(inputPrefabsProperty, true);

                // ADDED: Dynamic Skinned Mesh Warning
                if (DetectUnsupportedSkinnedMeshSetup())
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.HelpBox("UNSUPPORTED SETUP DETECTED:\nYou have added multiple different prefabs containing Skinned Mesh Renderers. \n\nCombining distinct animated rigs will corrupt their bone weight indices. You may use this tool to optimize the materials of a SINGLE Skinned Mesh prefab, but combining multiple different characters is not supported.", MessageType.Error);
                }

                if (EditorGUI.EndChangeCheck())
                {
                    analysisResultsProperty.ClearArray();
                }
            }

            EditorGUI.indentLevel--;
        }

        private void DrawSettingsUI()
        {
            var showSettingsProp = serializedWindow.FindProperty(nameof(showSettings));
            showSettingsProp.boolValue = EditorGUILayout.Foldout(showSettingsProp.boolValue, "Combinator Settings", true, EditorStyles.foldoutHeader);

            if (!showSettingsProp.boolValue) return;

            EditorGUI.indentLevel++;

            DrawEducationalUpsell();

            EditorGUILayout.PropertyField(serializedWindow.FindProperty(nameof(saveDirectory)), new GUIContent("Save Directory"));
            EditorGUILayout.PropertyField(serializedWindow.FindProperty(nameof(assetSuffix)), new GUIContent("Asset Suffix"));
            EditorGUILayout.PropertyField(serializedWindow.FindProperty(nameof(processLODs)));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Mesh Topology Optimization", EditorStyles.boldLabel);

            var combineProp = serializedWindow.FindProperty(nameof(combineSubmeshes));
            EditorGUILayout.PropertyField(combineProp, new GUIContent("Combine Submeshes (Optional)", "Merges submeshes sharing the same material or atlas. Leave disabled to strictly preserve the original mesh structure."));

            // ADDED: Dynamic Warning Box
            if (combineProp.boolValue)
            {
                EditorGUILayout.HelpBox("WARNING: Destructive Operation.\nCombining submeshes permanently removes individual material slots and merges topology. This may cause issues with Skinned Mesh Renderers, interactable objects, or hero assets.", MessageType.Warning);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Packing Bounds", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("The packer uses original input texture sizes and will fail if they exceed the maximum bounds.", MessageType.Info);

            var sizingModeProp = serializedWindow.FindProperty(nameof(sizingMode));
            EditorGUILayout.PropertyField(sizingModeProp, new GUIContent("Sizing Mode", "Scales the canvas to standard game-ready resolutions or custom dimensions."));

            if (sizingModeProp.intValue == (int)AtlasSizingMode.CustomDimensions)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(serializedWindow.FindProperty(nameof(customAtlasWidth)));
                EditorGUILayout.PropertyField(serializedWindow.FindProperty(nameof(customAtlasHeight)));

                if (!IsCustomSizeValid())
                {
                    EditorGUILayout.HelpBox("Warning: Custom dimensions must be positive multiples of 8 for texture compression compatibility. Generation will be disabled.", MessageType.Warning);
                }

                EditorGUI.indentLevel--;
            }
            else
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedWindow.FindProperty(nameof(minAtlasSize)), new GUIContent("Minimum Atlas Size"));
                EditorGUILayout.PropertyField(serializedWindow.FindProperty(nameof(maxAtlasSize)), new GUIContent("Maximum Atlas Size"));
                EditorGUI.indentLevel--;
            }

            var paddingProp = serializedWindow.FindProperty(nameof(padding));
            EditorGUILayout.IntSlider(paddingProp, 0, 16, new GUIContent("Atlas Padding (Pixels)", "Padding in pixels added around each packed texture to prevent bleeding."));

            if (paddingProp.intValue > 0)
            {
                EditorGUILayout.HelpBox("Dynamic Padding Warning: Padding increases the pixel footprint of each texture. This may force the overall atlas to jump to the next Power of Two size or result in non-standard dimensions.", MessageType.Warning);
            }

            EditorGUILayout.PropertyField(serializedWindow.FindProperty(nameof(allowRotation)));
            EditorGUILayout.PropertyField(serializedWindow.FindProperty(nameof(packingHeuristic)), new GUIContent("Packing Heuristic"));
            EditorGUILayout.PropertyField(serializedWindow.FindProperty(nameof(fallbackTextureSize)), new GUIContent("Fallback Texture Size"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Pre-Pack Bounds Management", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedWindow.FindProperty(nameof(autoDownscaleLargeInputs)), new GUIContent("Auto-Downscale Large Inputs", "Temporarily downscales inputs that exceed Max Atlas Size to prevent packing failures."));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Post-Process Downscale", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Note: This scales the final output texture after it has been packed.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(2);

            var resizeScaleProp = serializedWindow.FindProperty(nameof(resizeScale));
            EditorGUILayout.PropertyField(resizeScaleProp, new GUIContent("Final Atlas Downscale Factor", "Scale down factor for the final generated atlases (e.g., 0.5x reduces a 2K atlas to 1K)."));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Material Output Configuration", EditorStyles.boldLabel);

            var generationModeProp = serializedWindow.FindProperty(nameof(generationMode));
            EditorGUILayout.PropertyField(generationModeProp);

            EditorGUI.indentLevel--;
        }
        private void DrawAnalysisUI()
        {
            var showAnalysisProp = serializedWindow.FindProperty(nameof(showAnalysis));
            showAnalysisProp.boolValue = EditorGUILayout.Foldout(showAnalysisProp.boolValue, "Texture Analysis & Selection", true, EditorStyles.foldoutHeader);

            if (!showAnalysisProp.boolValue) return;

            EditorGUI.indentLevel++;

            bool inputsValid = inputPrefabs.Any(p => p != null);

            EditorGUI.BeginDisabledGroup(!inputsValid || isAnalyzing);
            if (GUILayout.Button(isAnalyzing ? "Analyzing..." : "Analyze Inputs", GUILayout.Height(25)))
            {
                serializedWindow.ApplyModifiedProperties();
                StartAnalysisProcess();
            }
            EditorGUI.EndDisabledGroup();
            if (analysisResultsProperty != null && analysisResultsProperty.arraySize > 0)
            {
                // FORCE the scroll view to expand and demand at least 150 pixels of vertical space
                analysisScrollPosition = EditorGUILayout.BeginScrollView(
                    analysisScrollPosition,
                    GUILayout.ExpandHeight(true),
                    GUILayout.MinHeight(150f)
                );

                EditorGUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(EditorGUI.indentLevel * 15f + 20f);
                EditorGUILayout.LabelField("Property Name", EditorStyles.miniBoldLabel, GUILayout.ExpandWidth(true));
                EditorGUILayout.LabelField(new GUIContent("Map Type", "Ensure correct color space handling (Auto usually works)."), EditorStyles.miniBoldLabel, GUILayout.Width(100f));
                EditorGUILayout.EndHorizontal();

                for (int i = 0; i < analysisResultsProperty.arraySize; i++)
                {
                    SerializedProperty analysisStateProp = analysisResultsProperty.GetArrayElementAtIndex(i);

                    SerializedProperty shaderNameProp = analysisStateProp.FindPropertyRelative("ShaderName");
                    SerializedProperty propertiesListProp = analysisStateProp.FindPropertyRelative("Properties");
                    SerializedProperty isExpandedProp = analysisStateProp.FindPropertyRelative("IsExpanded");

                    isExpandedProp.boolValue = EditorGUILayout.Foldout(isExpandedProp.boolValue, shaderNameProp.stringValue, true, EditorStyles.foldoutHeader);

                    if (isExpandedProp.boolValue)
                    {
                        EditorGUI.indentLevel++;

                        float viewWidth = EditorGUIUtility.currentViewWidth;
                        float indentPadding = EditorGUI.indentLevel * 15f;
                        float margins = 40f;
                        float totalAvailableWidth = viewWidth - indentPadding - margins;

                        float toggleWidth = 20f;
                        float hintWidth = 100f;

                        float nameWidth = Mathf.Max(100f, totalAvailableWidth - toggleWidth - hintWidth);

                        for (int j = 0; j < propertiesListProp.arraySize; j++)
                        {
                            SerializedProperty propertyStateProp = propertiesListProp.GetArrayElementAtIndex(j);
                            SerializedProperty propNameProp = propertyStateProp.FindPropertyRelative("PropertyName");
                            SerializedProperty isEnabledProp = propertyStateProp.FindPropertyRelative("IsEnabled");
                            SerializedProperty hintProp = propertyStateProp.FindPropertyRelative("Hint");

                            EditorGUILayout.BeginHorizontal();

                            EditorGUILayout.PropertyField(isEnabledProp, GUIContent.none, GUILayout.Width(toggleWidth));
                            EditorGUILayout.LabelField(propNameProp.stringValue, GUILayout.Width(nameWidth));
                            EditorGUILayout.PropertyField(hintProp, GUIContent.none, GUILayout.Width(hintWidth));

                            EditorGUILayout.EndHorizontal();
                        }
                        EditorGUI.indentLevel--;
                    }
                }
                EditorGUILayout.EndScrollView();
            }
            else if (inputsValid && !isAnalyzing)
            {
                EditorGUILayout.HelpBox("Click 'Analyze Inputs' to detect texture properties.", MessageType.Info);
            }
            else if (!inputsValid)
            {
                EditorGUILayout.HelpBox("Add input prefabs before analysis.", MessageType.Info);
            }

            EditorGUI.indentLevel--;
        }

        private void DrawExecutionUI()
        {
            bool inputsValid = inputPrefabs.Any(p => p != null);

            bool analysisPerformed = analysisResultsProperty != null && analysisResultsProperty.arraySize > 0;

            bool customDimensionsValid = true;
            if (sizingMode == AtlasSizingMode.CustomDimensions)
            {
                customDimensionsValid = IsCustomSizeValid();
            }

            EditorGUI.BeginDisabledGroup(!inputsValid || !analysisPerformed || isProcessing || !customDimensionsValid);
            Color oldColor = GUI.backgroundColor;
            GUI.backgroundColor = ColorGenerate;

            string buttonText = isProcessing ? "Processing..." : "Generate Optimized Prefabs";

            if (GUILayout.Button(buttonText, GUILayout.Height(40)))
            {
                if (EditorUtility.DisplayDialog("Confirm Materials Combination",
                    "This will combine materials based on the selected properties and generate new optimized prefabs. This process may temporarily modify and reimport texture assets. Continue?",
                    "Generate", "Cancel"))
                {
                    serializedWindow.ApplyModifiedProperties();
                    StartCombinatorProcess();
                }
            }

            GUI.backgroundColor = oldColor;
            EditorGUI.EndDisabledGroup();

            if (inputsValid && !analysisPerformed)
            {
                EditorGUILayout.HelpBox("Please click 'Analyze Inputs' before generating optimized prefabs.", MessageType.Warning);
            }
        }

        private void DrawUndoUI()
        {
            string currentPath = lastCreatedFolderPath;
            bool assetCreationOccurred = !string.IsNullOrEmpty(currentPath) && System.IO.Directory.Exists(currentPath);
            if (!assetCreationOccurred) return;

            EditorGUILayout.HelpBox("Last created asset path:\n" + currentPath, MessageType.Info);

            GUI.backgroundColor = new Color(1f, 0.7f, 0.7f);
            if (GUILayout.Button("Undo / Delete Last Creation"))
            {
                if (EditorUtility.DisplayDialog("Delete Last Creation?",
                    "Are you sure you want to delete the folder and all its assets?\n\n" + currentPath + "\n\nThis action cannot be undone.",
                    "Yes, Delete", "Cancel"))
                {
                    if (AssetDatabase.DeleteAsset(currentPath))
                    {
                        AssetDatabase.Refresh();
                        lastCreatedFolderPath = null;
                    }
                    else
                    {
                        Debug.LogError($"[Combinator] Failed to delete folder: {currentPath}");
                    }
                    Repaint();
                }
            }
            GUI.backgroundColor = Color.white;
        }

        #endregion

        #region Drag and Drop Handling

        private void DrawPrefabDropZone()
        {
            Rect dropArea = GUILayoutUtility.GetRect(0.0f, 50.0f, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "Drag & Drop Prefab Assets Here", EditorStyles.helpBox);

            Event currentEvent = Event.current;
            if (!dropArea.Contains(currentEvent.mousePosition)) return;

            switch (currentEvent.type)
            {
                case EventType.DragUpdated:
                    bool isDragValid = DragAndDrop.objectReferences.All(obj =>
                        obj is GameObject go && PrefabUtility.IsPartOfPrefabAsset(go)
                    );

                    DragAndDrop.visualMode = isDragValid ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
                    currentEvent.Use();
                    break;

                case EventType.DragPerform:
                    DragAndDrop.AcceptDrag();
                    List<GameObject> droppedPrefabs = DragAndDrop.objectReferences
                        .OfType<GameObject>()
                        .Where(obj => PrefabUtility.IsPartOfPrefabAsset(obj))
                        .ToList();

                    if (droppedPrefabs.Any())
                    {
                        AddPrefabs(droppedPrefabs);
                        currentEvent.Use();
                    }
                    break;
            }
        }

        private void AddPrefabs(List<GameObject> droppedPrefabs)
        {
            if (serializedWindow == null || inputPrefabsProperty == null) return;

            serializedWindow.Update();

            var existingPrefabs = new HashSet<GameObject>(inputPrefabs);
            List<GameObject> eligiblePrefabs = droppedPrefabs
                .Where(p => p != null && !existingPrefabs.Contains(p))
                .Distinct()
                .ToList();

            if (!eligiblePrefabs.Any()) return;

            foreach (var prefabToAdd in eligiblePrefabs)
            {
                int newIndex = inputPrefabsProperty.arraySize;
                inputPrefabsProperty.InsertArrayElementAtIndex(newIndex);
                var newElementProp = inputPrefabsProperty.GetArrayElementAtIndex(newIndex);
                newElementProp.objectReferenceValue = prefabToAdd;
            }

            analysisResultsProperty.ClearArray();
            serializedWindow.ApplyModifiedProperties();
            Repaint();
        }

        #endregion

        #region Process Execution and Callbacks

        private void StartAnalysisProcess()
        {
            isAnalyzing = true;

            try
            {
                EditorUtility.DisplayProgressBar("Analyzing Assets", "Inspecting prefabs and materials...", 0.5f);

                var newAnalysisResults = combinatorProcessor.AnalyzeInputs(inputPrefabs, processLODs);

                if (newAnalysisResults == null)
                {
                    Undo.RecordObject(this, "Clear Analysis (Validation Failed)");
                    analysisResults.Clear();
                }
                else
                {
                    Undo.RecordObject(this, "Update Combinator Analysis");
                    analysisResults.Clear();
                    analysisResults.AddRange(newAnalysisResults);

                    if (newAnalysisResults.Count == 0)
                    {
                        Debug.LogWarning("[Combinator Analysis] Analysis completed, but no combinable materials or textures were found.");
                    }
                }

                if (serializedWindow != null && serializedWindow.targetObject != null)
                {
                    serializedWindow.Update();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Combinator Analysis] An error occurred: {ex.Message}\n{ex.StackTrace}");
                EditorUtility.DisplayDialog("Analysis Error", $"An unexpected error occurred: {ex.Message}", "OK");
            }
            finally
            {
                isAnalyzing = false;
                EditorUtility.ClearProgressBar();
                Repaint();
            }
        }

        private void StartCombinatorProcess()
        {
            isProcessing = true;

            var config = new CombinatorEditorProcessor.ProcessorConfiguration
            {
                SaveDirectory = saveDirectory,
                AssetSuffix = assetSuffix,
                Padding = padding,
                AllowRotation = allowRotation,
                ProcessLODs = processLODs,
                ResizeScale = resizeScale,

                SizingMode = sizingMode,
                CustomAtlasWidth = customAtlasWidth,
                CustomAtlasHeight = customAtlasHeight,
                GenerationMode = generationMode,

                PackingHeuristic = packingHeuristic,
                MaxAtlasSize = maxAtlasSize,
                MinAtlasSize = minAtlasSize,
                FallbackTextureSize = fallbackTextureSize,
                AutoDownscaleLargeInputs = autoDownscaleLargeInputs,
                CombineSubmeshes = combineSubmeshes // ADDED
            };

            try
            {
                combinatorProcessor.Process(inputPrefabs, analysisResults, config);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Combinator] An error occurred during process setup: {ex.Message}\n{ex.StackTrace}");
                OnCombinatorComplete(false, $"An unexpected error occurred during setup: {ex.Message}", null);
            }
        }

        private void OnCombinatorProgress(string status, float progress)
        {
            EditorApplication.delayCall += () =>
            {
                if (isProcessing)
                {
                    EditorUtility.DisplayProgressBar("Optimizing Assets (Combinator)", status, progress);
                }
            };
        }

        private void OnCombinatorComplete(bool success, string message, string outputPath)
        {
            EditorApplication.delayCall += () =>
            {
                isProcessing = false;
                isAnalyzing = false;

                EditorUtility.ClearProgressBar();

                if (success)
                {
                    Undo.RecordObject(this, "Clear Analysis Post-Process");
                    analysisResults.Clear();

                    if (serializedWindow != null && serializedWindow.targetObject != null)
                    {
                        serializedWindow.Update();
                    }

                    Debug.Log($"[Combinator Success] {message}");
                    lastCreatedFolderPath = outputPath;

                    if (!string.IsNullOrEmpty(outputPath))
                    {
                        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(outputPath));
                    }
                }
                else
                {
                    string displayMessage = message;
                    string title = "Optimization Error";

                    const string ERROR_CUSTOM_SIZE_TOO_SMALL = "ERROR_CUSTOM_SIZE_TOO_SMALL";

                    if (message == ERROR_CUSTOM_SIZE_TOO_SMALL)
                    {
                        title = "Packing Error";
                        displayMessage = "Packing Error: The specified custom dimensions are too small for the input textures.";
                    }
                    else if (message.StartsWith("Validation Failed"))
                    {
                        title = "Validation Failed";
                    }

                    Debug.LogError($"[Combinator Failed] {message}");
                    EditorUtility.DisplayDialog(title, displayMessage, "OK");
                }

                Repaint();
            };
        }

        #endregion

        #region Utility Methods

        private bool IsCustomSizeValid()
        {
            return customAtlasWidth > 0 && customAtlasHeight > 0 &&
                   customAtlasWidth % 8 == 0 && customAtlasHeight % 8 == 0;
        }

        private void ResetTool()
        {
            if (EditorUtility.DisplayDialog("Reset Materials Combinator?", "Are you sure? This will reset all settings, clear the input list, and clear analysis results.", "Yes, Reset", "Cancel"))
            {
                Undo.RecordObject(this, "Reset Combinator Tool");

                inputPrefabs.Clear();
                showSettings = true;
                showInputs = true;
                showAnalysis = true;

                saveDirectory = "Assets/VeridianData/MaterialsCombinator";
                assetSuffix = "_Atlased";
                padding = 0;
                allowRotation = true;
                processLODs = true;
                resizeScale = 1.0f;
                combineSubmeshes = false; // ADDED

                sizingMode = AtlasSizingMode.AutomaticRectangularPOT;
                customAtlasWidth = 1024;
                customAtlasHeight = 1024;
                generationMode = MaterialGenerationMode.AutoGenerate;

                packingHeuristic = FreeRectChoiceHeuristic.BestShortSideFit;
                maxAtlasSize = 8192;
                minAtlasSize = 128;
                fallbackTextureSize = 4;
                autoDownscaleLargeInputs = true;

                analysisResults.Clear();

                serializedWindow?.Update();
                Repaint();
            }
        }
        private bool DetectUnsupportedSkinnedMeshSetup()
        {
            if (inputPrefabs == null || inputPrefabs.Count <= 1) return false;

            int prefabsWithSkinnedMeshes = 0;

            foreach (var prefab in inputPrefabs)
            {
                if (prefab != null)
                {
                    // Check if this specific prefab has any SkinnedMeshRenderers
                    if (prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length > 0)
                    {
                        prefabsWithSkinnedMeshes++;
                    }
                }
            }

            // If more than one DISTINCT prefab has a skinned mesh, the bone indices will corrupt if fused.
            return prefabsWithSkinnedMeshes > 1;
        }
        #endregion
    }
}
#endif