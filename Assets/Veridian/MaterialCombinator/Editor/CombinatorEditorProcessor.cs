#if UNITY_EDITOR
// File: Editor/Combinator/Processing/CombinatorEditorProcessor.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using System; // Required for Exception handling

namespace Veridian.Perspective.Combinator.Editor
{
    /// <summary>
    /// Editor-specific wrapper for CombinatorCore. Handles asset management, UI communication, and orchestration.
    /// This class is stateless and operates based on the parameters passed to its methods.
    /// </summary>
    public class CombinatorEditorProcessor
    {
        // Events for UI communication (Callbacks)
        public delegate void ProgressCallback(string status, float progress);
        public event ProgressCallback OnProgress;
        public delegate void CompletionCallback(bool success, string message, string outputPath);
        public event CompletionCallback OnComplete;

        private string outputPath; // Runtime variable for the current operation

        // Data structure for processor configuration
        public struct ProcessorConfiguration
        {
            public string SaveDirectory;
            public string AssetSuffix;
            public int Padding;
            public bool AllowRotation;
            public bool ProcessLODs;
            public float ResizeScale;

            public AtlasSizingMode SizingMode;
            public int CustomAtlasWidth;
            public int CustomAtlasHeight;

            public MaterialGenerationMode GenerationMode;

            public FreeRectChoiceHeuristic PackingHeuristic;
            public int MaxAtlasSize;
            public int MinAtlasSize;
            public int FallbackTextureSize;

            public bool AutoDownscaleLargeInputs;

            // ADDED
            public bool CombineSubmeshes;
        }

        #region Asset Pre-Processor (Texture Import Handling)

        /// <summary>
        /// Helper class responsible for backing up, modifying, and restoring texture import settings.
        /// </summary>
        private class AssetPreProcessor
        {
            // Stores the original settings for restoration
            private class ImporterBackupState
            {
                public TextureImporter Importer;
                public bool OriginalSRGBTexture;
                public TextureImporterType OriginalTextureType;
                public bool SettingsChanged;
            }

            private readonly Dictionary<string, ImporterBackupState> backupStates = new();

            /// <summary>
            /// Collects textures, backups settings, and applies modifications based on hints.
            /// </summary>
            public bool PrepareAssets(List<ShaderAnalysisState> analysisResults, List<EditorSourceObjectData> sourceObjects)
            {
                // 1. Identify all unique textures and their corresponding hints.
                var textureHints = CollectTextureHints(analysisResults, sourceObjects);

                if (textureHints.Count == 0)
                {
                    return true; // Nothing to process
                }

                // 2. Backup and Modify settings
                bool requiresReimport = false;
                // Use AssetDatabase.StartAssetEditing() to batch the importer modifications.
                AssetDatabase.StartAssetEditing();
                try
                {
                    foreach (var kvp in textureHints)
                    {
                        Texture2D texture = kvp.Key;
                        TextureTypeHint hint = kvp.Value;

                        if (texture == null) continue;

                        string path = AssetDatabase.GetAssetPath(texture);
                        // Skip built-in or dynamic textures
                        if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets")) continue;

                        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                        if (importer == null) continue;

                        // Backup original state
                        var backup = new ImporterBackupState
                        {
                            Importer = importer,
                            OriginalSRGBTexture = importer.sRGBTexture,
                            OriginalTextureType = importer.textureType,
                            SettingsChanged = false
                        };

                        // The hint collected in CollectTextureHints is already resolved (Auto-detection applied if necessary).
                        TextureTypeHint determinedType = hint;

                        // Apply modifications required for CombinatorCore (which expects linear input)
                        if (determinedType == TextureTypeHint.BaseColor)
                        {
                            // If the texture is determined to be BaseColor, set its importer's sRGBTexture to false.
                            if (importer.sRGBTexture)
                            {
                                importer.sRGBTexture = false;
                                backup.SettingsChanged = true;
                            }
                            // Ensure type is default if it was perhaps incorrectly set to Normal Map
                            if (importer.textureType != TextureImporterType.Default)
                            {
                                importer.textureType = TextureImporterType.Default;
                                backup.SettingsChanged = true;
                            }
                        }
                        else if (determinedType == TextureTypeHint.NormalMap)
                        {
                            // If the texture is determined to be NormalMap, set its textureType to TextureImporterType.Default.
                            if (importer.textureType != TextureImporterType.Default)
                            {
                                importer.textureType = TextureImporterType.Default;
                                backup.SettingsChanged = true;
                            }
                            // Ensure sRGB is also off for Normal Maps.
                            if (importer.sRGBTexture)
                            {
                                importer.sRGBTexture = false;
                                backup.SettingsChanged = true;
                            }
                        }

                        if (backup.SettingsChanged)
                        {
                            backupStates[path] = backup;
                            // Mark the asset for reimport with the new settings.
                            importer.SaveAndReimport();
                            requiresReimport = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Combinator Pre-Processor] Error during asset preparation: {ex.Message}\n{ex.StackTrace}");
                    // If preparation fails catastrophically, try to restore immediately before stopping asset editing.
                    RestoreAssets(showProgress: false);
                    return false;
                }
                finally
                {
                    // Stop editing. This triggers the batched reimport if SaveAndReimport() was called.
                    AssetDatabase.StopAssetEditing();
                }

                if (requiresReimport)
                {
                    Debug.Log("[Combinator Pre-Processor] Temporarily modified texture import settings. Reimporting assets...");
                    // Ensure the AssetDatabase is fully refreshed after the changes.
                    AssetDatabase.Refresh();
                }

                return true;
            }

            /// <summary>
            /// Restores all backed-up settings. Must be called in a finally block.
            /// </summary>
            public void RestoreAssets(bool showProgress)
            {
                if (backupStates.Count == 0) return;

                // Use a dedicated progress bar for restoration if requested (e.g., if the main process completed successfully).
                if (showProgress)
                {
                    EditorUtility.DisplayProgressBar("Restoring Assets", "Reverting texture import settings...", 0.0f);
                }

                bool requiresReimport = false;
                // Batch the restoration process.
                AssetDatabase.StartAssetEditing();
                try
                {
                    int index = 0;
                    foreach (var backup in backupStates.Values)
                    {
                        if (showProgress)
                        {
                            EditorUtility.DisplayProgressBar("Restoring Assets", $"Reverting: {System.IO.Path.GetFileName(AssetDatabase.GetAssetPath(backup.Importer))}", (float)index / backupStates.Count);
                        }

                        // Check if the importer still exists and settings were actually changed.
                        if (backup.Importer != null && backup.SettingsChanged)
                        {
                            backup.Importer.sRGBTexture = backup.OriginalSRGBTexture;
                            backup.Importer.textureType = backup.OriginalTextureType;
                            // Mark for reimport back to the original state.
                            backup.Importer.SaveAndReimport();
                            requiresReimport = true;
                        }
                        index++;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Combinator Pre-Processor] CRITICAL ERROR during asset restoration: {ex.Message}\n{ex.StackTrace}. Project texture settings might be corrupted.");
                }
                finally
                {
                    // Stop editing, triggering the batched restoration reimport.
                    AssetDatabase.StopAssetEditing();
                    if (showProgress)
                    {
                        EditorUtility.ClearProgressBar();
                    }
                }

                if (requiresReimport)
                {
                    Debug.Log("[Combinator Pre-Processor] Restored original texture import settings. Reimporting assets...");
                    // Final refresh after restoration.
                    AssetDatabase.Refresh();
                }

                backupStates.Clear();
            }

            // Helper to collect all textures and determine the definitive hint for each.
            private Dictionary<Texture2D, TextureTypeHint> CollectTextureHints(List<ShaderAnalysisState> analysisResults, List<EditorSourceObjectData> sourceObjects)
            {
                // 1. Create a map from (Shader GUID, Property Name) to the user-selected Hint for enabled properties.
                var hintMap = new Dictionary<(string ShaderGUID, string PropertyName), TextureTypeHint>();
                foreach (var analysis in analysisResults)
                {
                    foreach (var prop in analysis.Properties)
                    {
                        if (prop.IsEnabled)
                        {
                            hintMap[(analysis.ShaderGUID, prop.PropertyName)] = prop.Hint;
                        }
                    }
                }

                // 2. Iterate through all materials and textures, applying hints and resolving conflicts.
                var textureHints = new Dictionary<Texture2D, TextureTypeHint>();

                foreach (var sourceObject in sourceObjects)
                {
                    foreach (var material in sourceObject.SharedMaterials)
                    {
                        if (material == null || material.shader == null) continue;

                        string shaderPath = AssetDatabase.GetAssetPath(material.shader);
                        string shaderGUID = AssetDatabase.AssetPathToGUID(shaderPath);

                        // Iterate through all texture properties of the material.
                        int propertyCount = material.shader.GetPropertyCount();
                        for (int i = 0; i < propertyCount; i++)
                        {
                            if (material.shader.GetPropertyType(i) == UnityEngine.Rendering.ShaderPropertyType.Texture)
                            {
                                string propertyName = material.shader.GetPropertyName(i);

                                // Check if this property is enabled and included in the analysis results.
                                if (hintMap.TryGetValue((shaderGUID, propertyName), out TextureTypeHint userHint))
                                {
                                    Texture2D texture = material.GetTexture(propertyName) as Texture2D;
                                    if (texture != null)
                                    {
                                        // Determine the actual hint to use.
                                        TextureTypeHint determinedHint = userHint;
                                        if (userHint == TextureTypeHint.Auto)
                                        {
                                            // If Auto, guess based on the property name first.
                                            determinedHint = GuessTextureType(propertyName);

                                            // If the property name guess is inconclusive (BaseColor), try the texture asset name as a fallback.
                                            // This improves detection for assets named e.g., "Texture_Nrm" used in a generic "_MainTex" property.
                                            if (determinedHint == TextureTypeHint.BaseColor)
                                            {
                                                TextureTypeHint assetNameGuess = GuessTextureType(texture.name);
                                                if (assetNameGuess == TextureTypeHint.NormalMap)
                                                {
                                                    determinedHint = TextureTypeHint.NormalMap;
                                                }
                                            }
                                        }

                                        // Conflict resolution: Ensure a texture has a consistent hint across all materials using it.
                                        if (textureHints.TryGetValue(texture, out TextureTypeHint existingHint))
                                        {
                                            if (existingHint != determinedHint)
                                            {
                                                // If hints differ (e.g., used as BaseColor in one material and NormalMap in another),
                                                // we prioritize BaseColor (linearization) as it's generally safer than misinterpreting a NormalMap.
                                                Debug.LogWarning($"[Combinator Pre-Processor] Conflicting texture type hints detected for texture '{texture.name}' (Property: {propertyName}). Defaulting to BaseColor (Linear).");
                                                textureHints[texture] = TextureTypeHint.BaseColor;
                                            }
                                        }
                                        else
                                        {
                                            textureHints[texture] = determinedHint;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                return textureHints;
            }

            // Heuristic guessing based on name (property name or texture asset name).
            private static TextureTypeHint GuessTextureType(string name)
            {
                string lowerName = name.ToLower();

                // Check for Normal Map indicators
                // (e.g., property names containing "bump," "normal," or "nrm" are NormalMap;
                if (lowerName.Contains("bump") || lowerName.Contains("normal") || lowerName.Contains("nrm"))
                {
                    return TextureTypeHint.NormalMap;
                }

                // Check for Base Color indicators
                // names like _MainTex, "albedo," "base," or "diffuse" are BaseColor).
                if (lowerName.Contains("_maintex") || lowerName.Contains("albedo") || lowerName.Contains("base") || lowerName.Contains("diffuse"))
                {
                    return TextureTypeHint.BaseColor;
                }

                // Default assumption: if it's not explicitly a normal map, treat it as BaseColor/Data.
                // CombinatorCore expects linear input, so disabling sRGB on import (the action for BaseColor) is correct for both sRGB textures and data maps (like masks, metallic).
                return TextureTypeHint.BaseColor;
            }
        }

        #endregion

        // Entry point for the analysis feature. Returns the analysis results.
        // Returns null if a validation error occurred that required user notification (e.g., scene object input).
        public List<ShaderAnalysisState> AnalyzeInputs(List<GameObject> inputPrefabs, bool processLODs)
        {
            List<ShaderAnalysisState> analysisResults = new();

            if (!ValidateInputs(inputPrefabs))
            {
                Debug.LogWarning("[Combinator Analysis] Input validation failed: List is empty or contains nulls.");
                return analysisResults; // Return empty list
            }

            var editorSourceObjects = AnalyzeAndExtractData(inputPrefabs, processLODs);

            // Check if validation failed during extraction (e.g., scene objects or LOD issues)
            if (editorSourceObjects == null)
            {
                // Error message already handled by AnalyzeAndExtractData via OnComplete (which bubbles up to the UI)
                // Return null to indicate a failure that required user notification.
                return null;
            }

            if (editorSourceObjects.Count == 0)
            {
                Debug.LogWarning("[Combinator Analysis] No valid MeshRenderers or MeshFilters found in the input prefabs.");
                return analysisResults; // Return empty list
            }

            var tempGroups = GroupMaterialsForAnalysis(editorSourceObjects);
            foreach (var kvp in tempGroups)
            {
                Shader shader = kvp.Key;
                if (shader == null) continue;

                // Get the shader's path and GUID for robust serialization.
                string assetPath = AssetDatabase.GetAssetPath(shader);
                if (string.IsNullOrEmpty(assetPath)) continue; // Ignore built-in/dynamic shaders that don't have an asset path
                string guid = AssetDatabase.AssetPathToGUID(assetPath);

                // Store the GUID and name.
                ShaderAnalysisState analysisState = new()
                {
                    ShaderGUID = guid,
                    ShaderName = shader.name
                };

                MaterialGroup group = kvp.Value;
                foreach (var propName in group.TextureProperties)
                {
                    // By default, enable properties that are actively used (have textures assigned).
                    bool isUsed = group.UsedTextureProperties.Contains(propName);
                    // The Hint defaults to Auto, as defined in ShaderPropertyState.
                    analysisState.Properties.Add(new ShaderPropertyState { PropertyName = propName, IsEnabled = isUsed });
                }

                analysisState.Properties = analysisState.Properties.OrderBy(p => p.PropertyName).ToList();
                if (analysisState.Properties.Count > 0)
                {
                    analysisResults.Add(analysisState);
                }
            }

            analysisResults = analysisResults.OrderBy(s => s.ShaderName).ToList();
            return analysisResults;
        }

        // Main processing entry point. Refactored to wrap execution with AssetPreProcessor.
        public void Process(List<GameObject> inputPrefabs, List<ShaderAnalysisState> analysisResults, ProcessorConfiguration config)
        {
            if (inputPrefabs == null || analysisResults == null)
            {
                OnComplete?.Invoke(false, "Inputs or Analysis data is invalid.", null);
                return;
            }

            if (!ValidateInputs(inputPrefabs))
            {
                OnComplete?.Invoke(false, "Input list is empty or invalid.", null);
                return;
            }

            if (analysisResults.Count == 0)
            {
                OnComplete?.Invoke(false, "Analysis has not been performed or yielded no results. Please analyze inputs first.", null);
                return;
            }

            AssetPreProcessor preProcessor = new();
            bool processingSucceeded = false;
            CombinationResult combinationResult = null;

            try
            {
                OnProgress?.Invoke("Analyzing inputs and extracting data...", 0.0f);

                var editorSourceObjects = AnalyzeAndExtractData(inputPrefabs, config.ProcessLODs);
                if (editorSourceObjects == null) return;

                if (editorSourceObjects.Count == 0)
                {
                    OnComplete?.Invoke(false, "No valid objects found to process.", null);
                    return;
                }

                OnProgress?.Invoke("Preparing assets (Backup and Re-import)...", 0.05f);
                if (!preProcessor.PrepareAssets(analysisResults, editorSourceObjects))
                {
                    OnComplete?.Invoke(false, "Failed to prepare assets for processing. Check console for errors.", null);
                    return;
                }

                var coreSourceObjects = ConvertToCoreSourceObjects(editorSourceObjects);

                var coreSettings = new CombinatorSettings
                {
                    AtlasPadding = config.Padding,
                    AllowRotation = config.AllowRotation,
                    SizingMode = config.SizingMode,
                    CustomAtlasWidth = config.CustomAtlasWidth,
                    CustomAtlasHeight = config.CustomAtlasHeight,
                    GenerationMode = config.GenerationMode,
                    PackingHeuristic = config.PackingHeuristic,
                    MaxAtlasSize = config.MaxAtlasSize,
                    MinAtlasSize = config.MinAtlasSize,
                    FallbackTextureSize = config.FallbackTextureSize,
                    AutoDownscaleLargeInputs = config.AutoDownscaleLargeInputs,
                    CombineSubmeshes = config.CombineSubmeshes // ADDED
                };

                Dictionary<Shader, HashSet<string>> propertiesToInclude = PrepareInclusionList(analysisResults);

                string parentDir = string.IsNullOrEmpty(config.SaveDirectory) ? "Assets/VeridianData/MaterialsCombinator" : config.SaveDirectory.TrimEnd('/');
                string rootName = editorSourceObjects.Count > 0 && editorSourceObjects[0].RootPrefab != null ? editorSourceObjects[0].RootPrefab.name : "Optimized";

                outputPath = AssetUtils.CreateCombinatorOutputFolder(parentDir, rootName);

                if (string.IsNullOrEmpty(outputPath))
                {
                    OnComplete?.Invoke(false, "Failed to create isolated output directory.", null);
                    return;
                }

                var core = new CombinatorCore(coreSettings);
                core.OnProgress += (status, progress) => OnProgress?.Invoke(status, progress);

                combinationResult = core.Process(coreSourceObjects, propertiesToInclude);

                if (!combinationResult.Success)
                {
                    OnComplete?.Invoke(false, combinationResult.Message, null);
                    CleanupOnError();
                    return;
                }

                OnProgress?.Invoke("Saving assets and generating prefabs...", 0.8f);

                GenerateOutputAssetsAndPrefabs(combinationResult, editorSourceObjects, config);

                AssetDatabase.SaveAssets();

                OnProgress?.Invoke("Complete.", 1.0f);

                string sizeSummary = GetAtlasSizeSummary(combinationResult.AtlasResults.Values.ToList());
                OnComplete?.Invoke(true, $"Successfully generated combined prefab in {outputPath}. {sizeSummary}", outputPath);
                processingSucceeded = true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[CombinatorEditorProcessor] Error: {ex.Message}\n{ex.StackTrace}");
                OnComplete?.Invoke(false, $"An error occurred: {ex.Message}", null);
                CleanupOnError();
            }
            finally
            {
                preProcessor.RestoreAssets(showProgress: processingSucceeded);
            }
        }

        private bool ValidateInputs(List<GameObject> inputPrefabs)
        {
            if (inputPrefabs == null || inputPrefabs.Count == 0 || !inputPrefabs.Any(p => p != null))
            {
                return false;
            }
            return true;
        }

        #region Analysis and Data Extraction (Editor Specific)

        // Helper struct for editor processing
        private class EditorSourceObjectData
        {
            // Store direct references to the assets, not the components from the temporary instance
            public Material[] SharedMaterials;
            public Mesh SharedMesh;
            public GameObject RootPrefab;
            public string TransformPath;
        }

        // Analyzes input prefabs by instantiating them temporarily.
        private List<EditorSourceObjectData> AnalyzeAndExtractData(List<GameObject> inputs, bool processLODs)
        {
            List<EditorSourceObjectData> extractedData = new();
            List<GameObject> instances = new();

            try
            {
                // Instantiate prefabs to analyze components and capture structure.
                foreach (var prefab in inputs)
                {
                    if (prefab == null) continue;

                    // Ensure inputs are prefab assets
                    if (!PrefabUtility.IsPartOfPrefabAsset(prefab))
                    {
                        string error = $"Validation Failed: Input '{prefab.name}' is not a Prefab Asset. Please use assets from the Project view, not objects from the scene.";
                        // We must invoke OnComplete here because this failure prevents the process from continuing and requires user attention.
                        OnComplete?.Invoke(false, error, null);
                        return null; // Fail fast
                    }

                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    instances.Add(instance);

                    // LODGroup Check
                    bool hasLODGroup = instance.GetComponentInChildren<LODGroup>() != null;

                    if (hasLODGroup && !processLODs)
                    {
                        string error = $"Validation Failed: Prefab '{prefab.name}' contains a LODGroup. Enable 'Process LOD Groups' or remove the LODGroup.";
                        // We must invoke OnComplete here for the same reason as above.
                        OnComplete?.Invoke(false, error, null);
                        return null; // Fail fast
                    }

                    // Find all valid renderers and filters.
                    var renderers = instance.GetComponentsInChildren<MeshRenderer>();
                    foreach (var renderer in renderers)
                    {
                        var filter = renderer.GetComponent<MeshFilter>();
                        // Check for valid mesh and materials on the components
                        if (filter != null && filter.sharedMesh != null && renderer.sharedMaterials != null && renderer.sharedMaterials.Length > 0)
                        {
                            extractedData.Add(new EditorSourceObjectData
                            {
                                // Store the assets directly
                                SharedMaterials = renderer.sharedMaterials,
                                SharedMesh = filter.sharedMesh,
                                RootPrefab = prefab,
                                // CalculateTransformPath determines the path relative to the root instance.
                                TransformPath = AnimationUtility.CalculateTransformPath(renderer.transform, instance.transform)
                            });
                        }
                    }
                }

                return extractedData;
            }
            finally
            {
                // Crucial: Clean up the temporary instances
                foreach (var instance in instances)
                {
                    if (instance != null) GameObject.DestroyImmediate(instance);
                }
            }
        }

        private List<SourceObject> ConvertToCoreSourceObjects(List<EditorSourceObjectData> editorData)
        {
            List<SourceObject> coreObjects = new();
            foreach (var data in editorData)
            {
                // The identifier used by the core logic is a tuple of (RootPrefab, TransformPath).
                var identifier = (data.RootPrefab, data.TransformPath);
                // Use the stored asset references
                coreObjects.Add(new SourceObject(data.SharedMaterials, data.SharedMesh, identifier));
            }
            return coreObjects;
        }

        private Dictionary<Shader, MaterialGroup> GroupMaterialsForAnalysis(List<EditorSourceObjectData> editorData)
        {
            var tempGroups = new Dictionary<Shader, MaterialGroup>();
            foreach (var data in editorData)
            {
                // Create a temporary SourceObject for analysis using the stored asset references.
                var tempSourceObject = new SourceObject(data.SharedMaterials, data.SharedMesh, null);
                // The rest of the logic remains the same, operating on tempSourceObject.SharedMaterials
                foreach (var material in tempSourceObject.SharedMaterials)
                {
                    if (material == null || material.shader == null) continue;
                    Shader shader = material.shader;
                    if (!tempGroups.ContainsKey(shader))
                    {
                        tempGroups[shader] = new MaterialGroup(shader);
                    }
                    tempGroups[shader].AddObject(material, tempSourceObject);
                }
            }
            return tempGroups;
        }

        // Prepares the inclusion list by resolving the stored GUIDs back into Shader objects.
        private Dictionary<Shader, HashSet<string>> PrepareInclusionList(List<ShaderAnalysisState> analysisResults)
        {
            var inclusionList = new Dictionary<Shader, HashSet<string>>();
            foreach (var analysisState in analysisResults)
            {
                // Convert the stored GUID back into a Shader object.
                if (string.IsNullOrEmpty(analysisState.ShaderGUID)) continue;
                string path = AssetDatabase.GUIDToAssetPath(analysisState.ShaderGUID);
                if (string.IsNullOrEmpty(path)) continue;

                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (shader == null)
                {
                    // Shader might have been deleted since analysis.
                    Debug.LogWarning($"[CombinatorEditorProcessor] Shader '{analysisState.ShaderName}' not found (GUID: {analysisState.ShaderGUID}). Skipping.");
                    continue;
                }

                HashSet<string> included = new();
                foreach (var propState in analysisState.Properties)
                {
                    if (propState.IsEnabled)
                    {
                        included.Add(propState.PropertyName);
                    }
                }

                if (included.Count > 0)
                {
                    inclusionList[shader] = included;
                }
            }
            return inclusionList;
        }
        #endregion

        #region Asset Saving and Prefab Generation (Editor Specific)

        private void GenerateOutputAssetsAndPrefabs(CombinationResult combinationResult, List<EditorSourceObjectData> editorSourceObjects, ProcessorConfiguration config)
        {
            SaveAssets(combinationResult, config.ResizeScale);
            GeneratePrefabs(combinationResult, editorSourceObjects, config.GenerationMode, config.AssetSuffix);
        }

        private void SaveAssets(CombinationResult combinationResult, float atlasResizeScale)
        {
            int groupIndex = 0;
            foreach (var kvp in combinationResult.AtlasResults)
            {
                AtlasResult atlasResult = kvp.Value;
                MaterialGroup group = kvp.Key;

                if (group.UniqueMaterials.Count <= 1)
                {
                    groupIndex++;
                    continue;
                }

                string safeShaderName = "UnknownShader";
                if (group.SourceShader != null)
                {
                    int lastSlash = group.SourceShader.name.LastIndexOf('/');
                    safeShaderName = lastSlash >= 0 ? group.SourceShader.name.Substring(lastSlash + 1) : group.SourceShader.name;
                    safeShaderName = string.Join("_", safeShaderName.Split(System.IO.Path.GetInvalidFileNameChars())).Replace(" ", "");
                }

                foreach (var generatedAtlasKvp in atlasResult.GeneratedAtlases)
                {
                    string propertyName = generatedAtlasKvp.Key;
                    Texture2D atlasTextureInMemory = generatedAtlasKvp.Value;

                    if (atlasTextureInMemory == null) continue;

                    string cleanPropName = propertyName.TrimStart('_');
                    string atlasName = $"Atlas_G{groupIndex}_{cleanPropName}";

                    Texture2D textureToSave = atlasTextureInMemory;

                    string lowerName = propertyName.ToLower();
                    bool isNormalMap = lowerName.Contains("normal") || lowerName.Contains("bump");

                    bool isLinear = isNormalMap || lowerName.Contains("metallic") || lowerName.Contains("mask") ||
                                    lowerName.Contains("occlusion") || lowerName.Contains("roughness") || lowerName.Contains("smoothness") ||
                                    TextureProcessor.IsHDR(atlasTextureInMemory.format);

                    if (atlasResizeScale < 1.0f)
                    {
                        OnProgress?.Invoke($"Resizing atlas {propertyName}...", 0.85f);

                        Texture2D resizedTexture = TextureProcessor.Resize(atlasTextureInMemory, atlasResizeScale, isLinear, isNormalMap);

                        if (resizedTexture != null)
                        {
                            textureToSave = resizedTexture;
                        }
                        else
                        {
                            Debug.LogError($"[CombinatorEditorProcessor] Failed to resize atlas for {propertyName}. Saving original size.");
                        }
                    }

                    Texture2D savedAtlasAsset = AssetUtils.SaveTexture(textureToSave, outputPath, atlasName, isNormalMap);

                    if (savedAtlasAsset != null && atlasResult.GeneratedMaterial != null && atlasResult.GeneratedMaterial.HasProperty(propertyName))
                    {
                        atlasResult.GeneratedMaterial.SetTexture(propertyName, savedAtlasAsset);
                    }

                    if (textureToSave != null && textureToSave != atlasTextureInMemory)
                    {
                        UnityEngine.Object.DestroyImmediate(textureToSave);
                    }
                    if (atlasTextureInMemory != null)
                    {
                        UnityEngine.Object.DestroyImmediate(atlasTextureInMemory);
                    }
                }

                if (atlasResult.GeneratedMaterial != null)
                {
                    string matName = $"Mat_G{groupIndex}_{safeShaderName}";
                    AssetUtils.SaveMaterial(atlasResult.GeneratedMaterial, outputPath, matName);
                }

                groupIndex++;
            }

            foreach (var kvp in combinationResult.GeneratedMeshes)
            {
                MeshResult meshResult = kvp.Value;
                Mesh mesh = meshResult.Mesh;
                if (mesh != null)
                {
                    string meshName = "OptimizedMesh";

                    if (kvp.Key is System.ValueTuple<GameObject, string> identifier && identifier.Item1 != null)
                    {
                        meshName = identifier.Item1.name + "_" + mesh.name.Replace("TempRemappedSubmesh_Combined", "Combined").Replace("_Combined", "");
                    }
                    else
                    {
                        meshName = mesh.name.Replace("TempRemappedSubmesh_Combined", "Combined").Replace("_Combined", "");
                    }
                    AssetUtils.SaveMesh(mesh, outputPath, meshName);
                }
            }
        }
        private void GeneratePrefabs(CombinationResult combinationResult, List<EditorSourceObjectData> editorSourceObjects, MaterialGenerationMode generationMode, string assetSuffix)
        {
            var objectsByPrefab = editorSourceObjects.GroupBy(s => s.RootPrefab).ToDictionary(g => g.Key, g => g.ToList());

            foreach (var prefabGroup in objectsByPrefab)
            {
                GameObject originalPrefab = prefabGroup.Key;
                List<EditorSourceObjectData> objectsInThisPrefab = prefabGroup.Value;

                GameObject outputInstance = (GameObject)PrefabUtility.InstantiatePrefab(originalPrefab);

                try
                {
                    outputInstance.name = originalPrefab.name + assetSuffix;

                    foreach (EditorSourceObjectData sourceData in objectsInThisPrefab)
                    {
                        Transform targetTransform = string.IsNullOrEmpty(sourceData.TransformPath) ? outputInstance.transform : outputInstance.transform.Find(sourceData.TransformPath);
                        if (targetTransform == null) continue;

                        MeshFilter targetFilter = targetTransform.GetComponent<MeshFilter>();
                        MeshRenderer targetRenderer = targetTransform.GetComponent<MeshRenderer>();

                        if (targetFilter == null || targetRenderer == null) continue;

                        var identifier = (sourceData.RootPrefab, sourceData.TransformPath);

                        if (combinationResult.GeneratedMeshes.TryGetValue(identifier, out MeshResult meshResult))
                        {
                            targetFilter.sharedMesh = meshResult.Mesh;
                            List<Material> newMaterials = new();

                            foreach (var key in meshResult.SubmeshKeys)
                            {
                                if (key is MaterialGroup group && combinationResult.AtlasResults.TryGetValue(group, out AtlasResult atlasResult))
                                {
                                    if (group.UniqueMaterials.Count > 1)
                                    {
                                        if (generationMode == MaterialGenerationMode.OutputRawAtlasesOnly)
                                            newMaterials.Add(null);
                                        else if (atlasResult.GeneratedMaterial != null)
                                            newMaterials.Add(atlasResult.GeneratedMaterial);
                                        else
                                            newMaterials.Add(group.UniqueMaterials.FirstOrDefault());
                                    }
                                    else
                                    {
                                        newMaterials.Add(group.UniqueMaterials.FirstOrDefault());
                                    }
                                }
                                else if (key is Material mat)
                                {
                                    newMaterials.Add(mat);
                                }
                                else
                                {
                                    newMaterials.Add(null);
                                }
                            }

                            targetRenderer.sharedMaterials = newMaterials.ToArray();
                        }
                    }

                    AssetUtils.SavePrefab(outputInstance, outputPath, outputInstance.name);
                }
                finally
                {
                    if (outputInstance != null)
                    {
                        UnityEngine.Object.DestroyImmediate(outputInstance);
                    }
                }
            }
        }

        #endregion

        #region Helpers

        private string GetAtlasSizeSummary(List<AtlasResult> results)
        {
            var validResults = results.Where(r => r.GeneratedAtlases.Count > 0).ToList();
            if (validResults.Count == 0) return "No texture atlases generated (materials might be untextured, unique, or all maps were deselected).";

            var summary = validResults.GroupBy(r => (r.AtlasWidth, r.AtlasHeight))
                                      .Select(g => $"{g.Count()} atlas group(s) at {g.Key.AtlasWidth}x{g.Key.AtlasHeight}");

            return "Generated " + string.Join(", ", summary) + ".";
        }

        private void CleanupOnError()
        {
            if (!string.IsNullOrEmpty(outputPath) && AssetDatabase.IsValidFolder(outputPath))
            {
                AssetUtils.DeleteFolder(outputPath);
                outputPath = null;
            }
        }
        #endregion
    }
}
#endif