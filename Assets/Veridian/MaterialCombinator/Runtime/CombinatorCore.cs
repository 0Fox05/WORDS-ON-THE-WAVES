
using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Veridian.Perspective.Combinator
{
    // Enum for the new sizing mode
    public enum AtlasSizingMode
    {
        [InspectorName("Auto Power of Two (Rectangular)")]
        AutomaticRectangularPOT,
        [InspectorName("Auto Power of Two (Square)")]
        AutomaticSquarePOT,
        [InspectorName("Custom Dimensions")]
        CustomDimensions
    }
    public enum MaterialGenerationMode
    {
        AutoGenerate,
        OutputRawAtlasesOnly
    }
    // Configuration settings for the combination process
    public class CombinatorSettings
    {
        public int AtlasPadding = 0;
        public bool AllowRotation = true;

        public AtlasSizingMode SizingMode = AtlasSizingMode.AutomaticRectangularPOT;
        public int CustomAtlasWidth = 1024;
        public int CustomAtlasHeight = 1024;

        public MaterialGenerationMode GenerationMode = MaterialGenerationMode.AutoGenerate;

        public FreeRectChoiceHeuristic PackingHeuristic = FreeRectChoiceHeuristic.BestShortSideFit;
        public int MaxAtlasSize = 8192;
        public int MinAtlasSize = 128;
        public int FallbackTextureSize = 4;

        public bool AutoDownscaleLargeInputs = true;

        // ADDED: Submesh topology control
        public bool CombineSubmeshes = false;
    }

    // Represents the complete result of the combination process (in memory)
    public class CombinationResult
    {
        public bool Success;
        public string Message;

        public Dictionary<MaterialGroup, AtlasResult> AtlasResults = new();

        public Dictionary<object, MeshResult> GeneratedMeshes = new();
    }
    public class MeshResult
    {
        public Mesh Mesh;
        public List<object> SubmeshKeys = new();
    }
    // --- Job Definitions ---

    /// <summary>
    /// Burst-compiled job for rotating pixel data 90 degrees Clockwise.
    /// Implements a "gather" approach: iterates over destination pixels and calculates the source pixel location.
    /// </summary>
    [BurstCompile]
    public struct RotatePixels90CWJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<Color32> InputPixels;
        [WriteOnly]
        public NativeArray<Color32> OutputPixels;

        public int SourceWidth;
        public int SourceHeight;

        // Execute runs in parallel for each pixel in the output image.
        public void Execute(int index)
        {
            // The new width of the rotated image is the height of the source.
            int newWidth = SourceHeight;

            // Calculate the coordinates (x, y) in the output (rotated) image
            int newX = index % newWidth;
            int newY = index / newWidth;

            // Calculate the corresponding coordinates in the input (source) image for CW rotation
            // CW Rotation Mapping: x_new = height - 1 - y_old, y_new = x_old
            // Inverse Mapping:     x_old = newY, y_old = height - 1 - newX

            int originalX = newY;
            int originalY = SourceHeight - 1 - newX;

            // Map back to the 1D input array index
            int originalIndex = originalY * SourceWidth + originalX;

            OutputPixels[index] = InputPixels[originalIndex];
        }
    }

    /// <summary>
        /// Burst-compiled job for remapping UV coordinates into an atlas layout.
        /// Uses Unity.Mathematics for optimal performance.
        /// </summary>
    [BurstCompile]
    public struct RemapUVsJob : IJobParallelFor
    {
        public NativeArray<float2> UVs;
        [ReadOnly]
        public NativeBitArray VerticesToProcess;

        public float AtlasX, AtlasY, AtlasWidth, AtlasHeight;
        public bool IsRotated;

        public void Execute(int i)
        {
            if (!VerticesToProcess.IsSet(i)) return;

            float2 uv = UVs[i];

            float u = math.frac(uv.x);
            float v = math.frac(uv.y);

            if (u < 0) u += 1.0f;
            if (v < 0) v += 1.0f;

            float remappedU, remappedV;

            if (IsRotated)
            {
                // MATHEMATICAL CORRECTION:
                // RotatePixels90CWJob effectively rotates the physical pixel data 90 degrees CCW 
                // in a bottom-left origin coordinate space. We must rotate UVs CCW to match.
                float rotatedU = 1.0f - v;
                float rotatedV = u;

                remappedU = AtlasX + (rotatedU * AtlasWidth);
                remappedV = AtlasY + (rotatedV * AtlasHeight);
            }
            else
            {
                remappedU = AtlasX + (u * AtlasWidth);
                remappedV = AtlasY + (v * AtlasHeight);
            }

            UVs[i] = new float2(remappedU, remappedV);
        }
    }


    /// <summary>
        /// The runtime-safe core logic for combining materials and remapping meshes.
        /// Optimized with GPU acceleration and C# Job System/Burst Compiler.
        /// </summary>
    public class CombinatorCore
    {
        private CombinatorSettings settings;
        private Dictionary<Shader, MaterialGroup> shaderGroups = new();
        private List<SourceObject> sourceObjects = new();
        private Dictionary<Texture2D, Texture2D> downscaledTextures = new();

        public delegate void ProgressCallback(string status, float progress);
        public event ProgressCallback OnProgress;

        public const string ERROR_CUSTOM_SIZE_TOO_SMALL = "PACKING_ERROR_CUSTOM_SIZE_TOO_SMALL";
        private Dictionary<string, Texture2D> fallbackTextures = new();

        public CombinatorCore(CombinatorSettings config)
        {
            this.settings = config;
        }

        public CombinationResult Process(List<SourceObject> inputs, Dictionary<Shader, HashSet<string>> propertiesToInclude = null)
        {
            sourceObjects = inputs;
            shaderGroups.Clear();
            downscaledTextures.Clear();

            CombinationResult combinationResult = new CombinationResult();

            if (inputs == null || inputs.Count == 0)
            {
                combinationResult.Success = false;
                combinationResult.Message = "Input list is empty or invalid.";
                return combinationResult;
            }

            try
            {
                OnProgress?.Invoke("Grouping materials and analyzing usage...", 0.1f);
                GroupMaterials();

                if (propertiesToInclude != null)
                {
                    FilterUsedProperties(propertiesToInclude);
                }

                OnProgress?.Invoke("Generating texture atlases (GPU Accelerated)...", 0.3f);
                var atlasGenResult = GenerateAtlases();

                combinationResult = atlasGenResult;

                if (!combinationResult.Success)
                {
                    return combinationResult;
                }

                OnProgress?.Invoke("Combining meshes and remapping UVs (Parallelized/MeshData)...", 0.7f);
                GenerateMeshes(combinationResult);

                OnProgress?.Invoke("Complete.", 1.0f);
                combinationResult.Success = true;
                combinationResult.Message = "Combination successful.";
                return combinationResult;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[CombinatorCore] Error: {ex.Message}\n{ex.StackTrace}");
                return new CombinationResult { Success = false, Message = $"An error occurred: {ex.Message}" };
            }
            finally
            {
                foreach (var tex in fallbackTextures.Values)
                {
                    if (tex != null)
                    {
                        if (Application.isPlaying) UnityEngine.Object.Destroy(tex);
                        else UnityEngine.Object.DestroyImmediate(tex);
                    }
                }
                fallbackTextures.Clear();

                foreach (var tex in downscaledTextures.Values)
                {
                    if (tex != null)
                    {
                        if (Application.isPlaying) UnityEngine.Object.Destroy(tex);
                        else UnityEngine.Object.DestroyImmediate(tex);
                    }
                }
                downscaledTextures.Clear();
            }
        }
        private Texture2D GetFallbackTexture(string propertyName)
        {
            if (fallbackTextures.TryGetValue(propertyName, out Texture2D tex)) return tex;

            string lowerName = propertyName.ToLower();
            bool isNormalMap = lowerName.Contains("normal") || lowerName.Contains("bump");
            bool isMask = lowerName.Contains("mask") || lowerName.Contains("occlusion") || lowerName.Contains("roughness") || lowerName.Contains("metallic") || lowerName.Contains("smoothness");

            Color fallbackColor = isNormalMap ? new Color(0.5f, 0.5f, 1.0f, 1.0f) : (isMask ? Color.black : Color.white);

            int fallbackSize = Mathf.Max(1, settings.FallbackTextureSize);
            tex = new Texture2D(fallbackSize, fallbackSize, TextureFormat.RGBA32, false, isNormalMap || isMask);
            tex.name = "Fallback_" + propertyName;

            Color[] pixels = new Color[fallbackSize * fallbackSize];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = fallbackColor;
            tex.SetPixels(pixels);
            tex.Apply();

            fallbackTextures[propertyName] = tex;
            return tex;
        }
        #region Step 1 & 2: Grouping and Filtering

        private void GroupMaterials()
        {
            shaderGroups.Clear();
            foreach (var obj in sourceObjects)
            {
                foreach (var material in obj.SharedMaterials)
                {
                    if (material == null || material.shader == null) continue;
                    Shader shader = material.shader;
                    if (!shaderGroups.ContainsKey(shader))
                    {
                        shaderGroups[shader] = new MaterialGroup(shader);
                    }
                    // AddObject also performs the initial analysis of usage (AnalyzeMaterialUsage).
                    shaderGroups[shader].AddObject(material, obj);
                }
            }
        }

        // Updates MaterialGroup.UsedTextureProperties based on the provided inclusion list from the UI.
        private void FilterUsedProperties(Dictionary<Shader, HashSet<string>> propertiesToInclude)
        {
            foreach (var kvp in shaderGroups)
            {
                Shader shader = kvp.Key;
                MaterialGroup group = kvp.Value;

                if (propertiesToInclude.TryGetValue(shader, out HashSet<string> allowedProperties))
                {
                    // We update UsedTextureProperties to be the set of allowed properties selected by the user.
                    group.UsedTextureProperties = allowedProperties;
                }
                else
                {
                    // If a shader group exists but isn't in the inclusion list, it means nothing was selected for it.
                    group.UsedTextureProperties.Clear();
                }
            }
        }

        #endregion

        #region Step 3: Atlasing (GPU Accelerated)

        private CombinationResult GenerateAtlases()
        {
            var result = new CombinationResult();
            int groupIndex = 0;

            foreach (var kvp in shaderGroups)
            {
                MaterialGroup group = kvp.Value;

                if (group.UniqueMaterials.Count <= 1)
                {
                    result.AtlasResults[group] = CreateDummyAtlasResult(group);
                    continue;
                }

                OnProgress?.Invoke($"Calculating atlas layout for group {groupIndex + 1}/{shaderGroups.Count}...", 0.3f + (0.4f * (float)groupIndex / shaderGroups.Count));

                AtlasResult atlasResult = CalculatePacking(group);

                if (atlasResult == null)
                {
                    if (settings.SizingMode == AtlasSizingMode.CustomDimensions)
                    {
                        return new CombinationResult { Success = false, Message = ERROR_CUSTOM_SIZE_TOO_SMALL };
                    }
                    else
                    {
                        return new CombinationResult { Success = false, Message = $"[CombinatorCore] Failed to pack textures for shader '{group.SourceShader.name}'. Textures might exceed the maximum allowed atlas size ({settings.MaxAtlasSize}x{settings.MaxAtlasSize})." };
                    }
                }

                OnProgress?.Invoke($"Generating textures for group {groupIndex + 1} (Size: {atlasResult.AtlasWidth}x{atlasResult.AtlasHeight})...", 0.3f + (0.4f * (float)(groupIndex + 0.5f) / shaderGroups.Count));
               
                Material combinedMaterial = null;


                // ARCHITECTURE BYPASS: Ignore material creation if OutputRawAtlasesOnly is selected
                if (settings.GenerationMode == MaterialGenerationMode.AutoGenerate)
                {
                    combinedMaterial = new Material(group.SourceShader)
                    {
                        name = $"CombinedMat_{groupIndex}"
                    };
                }

                foreach (string propertyName in group.UsedTextureProperties)
                {
                    Texture2D atlas = CreateAtlasTexture(group, atlasResult, propertyName);
                    if (atlas != null)
                    {
                        atlasResult.GeneratedAtlases[propertyName] = atlas;
                    }
                }

                if (settings.GenerationMode == MaterialGenerationMode.AutoGenerate && combinedMaterial != null)
                {
                    ConfigureCombinedMaterial(combinedMaterial, group);
                    atlasResult.GeneratedMaterial = combinedMaterial;
                }
                else
                {
                    // Fail-safe mode: bypass combined material generation completely
                    atlasResult.GeneratedMaterial = null;
                }

                result.AtlasResults[group] = atlasResult;
                groupIndex++;
            }

            result.Success = true;
            return result;
        }

        // Calculate packing based on the largest texture required by each material across all selected properties.
        private AtlasResult CalculatePacking(MaterialGroup group)
        {
            List<AtlasPacker.PackingInput> inputs = new();
            bool texturesExist = false;

            int fallbackSize = Mathf.Max(1, settings.FallbackTextureSize);

            foreach (Material mat in group.UniqueMaterials)
            {
                int maxWidth = fallbackSize;
                int maxHeight = fallbackSize;

                foreach (string propertyName in group.UsedTextureProperties)
                {
                    if (mat.HasProperty(propertyName))
                    {
                        Texture2D tex = mat.GetTexture(propertyName) as Texture2D;
                        if (tex != null)
                        {
                            int texW = tex.width;
                            int texH = tex.height;

                            if (settings.AutoDownscaleLargeInputs && (texW > settings.MaxAtlasSize || texH > settings.MaxAtlasSize))
                            {
                                if (!downscaledTextures.TryGetValue(tex, out Texture2D scaledTex))
                                {
                                    float scaleX = (float)settings.MaxAtlasSize / texW;
                                    float scaleY = (float)settings.MaxAtlasSize / texH;
                                    float scale = Mathf.Min(scaleX, scaleY);

                                    string lowerName = propertyName.ToLower();
                                    bool isNormalMap = lowerName.Contains("normal") || lowerName.Contains("bump");
                                    bool isLinear = isNormalMap || lowerName.Contains("metallic") || lowerName.Contains("mask") ||
                                                    lowerName.Contains("occlusion") || lowerName.Contains("roughness") || lowerName.Contains("smoothness") || TextureProcessor.IsHDR(tex.format);

                                    OnProgress?.Invoke($"Downscaling large input: {tex.name}...", 0.25f);

                                    scaledTex = TextureProcessor.Resize(tex, scale, isLinear, isNormalMap);
                                    if (scaledTex != null)
                                    {
                                        scaledTex.name = tex.name + "_DownscaledTemp";
                                        downscaledTextures[tex] = scaledTex;
                                    }
                                }

                                if (downscaledTextures.TryGetValue(tex, out Texture2D cachedTex) && cachedTex != null)
                                {
                                    texW = cachedTex.width;
                                    texH = cachedTex.height;
                                }
                            }

                            maxWidth = Mathf.Max(maxWidth, texW);
                            maxHeight = Mathf.Max(maxHeight, texH);
                            texturesExist = true;
                        }
                        else
                        {
                            maxWidth = Mathf.Max(maxWidth, fallbackSize);
                            maxHeight = Mathf.Max(maxHeight, fallbackSize);
                            texturesExist = true;
                        }
                    }
                }

                inputs.Add(new AtlasPacker.PackingInput
                {
                    SourceMaterial = mat,
                    Width = maxWidth,
                    Height = maxHeight,
                    Padding = settings.AtlasPadding
                });
            }

            if (!texturesExist)
            {
                return CreateNoTextureAtlasResult(group);
            }

            AtlasPacker packer = new();

            AtlasPacker.PackingResult packingResult = packer.Pack(
                inputs,
                settings.AllowRotation,
                settings.SizingMode,
                settings.CustomAtlasWidth,
                settings.CustomAtlasHeight,
                settings.MinAtlasSize,
                settings.MaxAtlasSize,
                settings.PackingHeuristic
            );

            if (!packingResult.Success)
            {
                return null;
            }

            return ConvertPackingResult(packingResult);
        }
        // Creates the actual Texture2D atlas using GPU acceleration (Graphics.CopyTexture) and Jobs (for rotation).
        private Texture2D CreateAtlasTexture(MaterialGroup group, AtlasResult atlasResult, string propertyName)
        {
            int atlasWidth = atlasResult.AtlasWidth;
            int atlasHeight = atlasResult.AtlasHeight;
            bool isNormalMap = propertyName.ToLower().Contains("normal") || propertyName.ToLower().Contains("bump");

            RenderTextureReadWrite readWrite = RenderTextureReadWrite.Linear;

            RenderTexture atlasRT = RenderTexture.GetTemporary(atlasWidth, atlasHeight, 0, RenderTextureFormat.ARGB32, readWrite);
            if (!atlasRT.Create())
            {
                Debug.LogError("[CombinatorCore] Failed to create RenderTexture for atlas.");
                RenderTexture.ReleaseTemporary(atlasRT);
                return null;
            }

            RenderTexture previousActive = RenderTexture.active;

            try
            {
                RenderTexture.active = atlasRT;
                Color defaultColor = isNormalMap ? new Color(0.5f, 0.5f, 1.0f, 1.0f) : Color.clear;
                GL.Clear(true, true, defaultColor);

                GL.PushMatrix();
                try
                {
                    GL.LoadPixelMatrix(0, atlasWidth, 0, atlasHeight);

                    foreach (Material mat in group.UniqueMaterials)
                    {
                        PackingInfo packingInfo = atlasResult.MaterialPackingInfo[mat];
                        Rect pixelRect = packingInfo.PixelRect;

                        Texture2D sourceTex = null;
                        if (mat.HasProperty(propertyName))
                        {
                            sourceTex = mat.GetTexture(propertyName) as Texture2D;
                            if (sourceTex != null && downscaledTextures.TryGetValue(sourceTex, out Texture2D downscaled))
                            {
                                sourceTex = downscaled;
                            }
                        }

                        bool isFallback = false;
                        if (sourceTex == null)
                        {
                            sourceTex = GetFallbackTexture(propertyName);
                            isFallback = true;
                        }

                        int destX = (int)pixelRect.x;
                        int destY = (int)pixelRect.y;

                        int sourceDataWidth = packingInfo.IsRotated ? sourceTex.height : sourceTex.width;
                        int sourceDataHeight = packingInfo.IsRotated ? sourceTex.width : sourceTex.height;

                        int copyWidth = isFallback ? (int)pixelRect.width : Mathf.Min(sourceDataWidth, (int)pixelRect.width);
                        int copyHeight = isFallback ? (int)pixelRect.height : Mathf.Min(sourceDataHeight, (int)pixelRect.height);

                        if (!packingInfo.IsRotated || isFallback)
                        {
                            Graphics.DrawTexture(new Rect(destX, destY, copyWidth, copyHeight), sourceTex, BlitMaterial);
                        }
                        else
                        {
                            ProcessRotatedTexture(sourceTex, atlasRT, destX, destY, copyWidth, copyHeight);
                        }
                    }
                }
                finally
                {
                    GL.PopMatrix();
                }

                Texture2D atlas = new Texture2D(atlasWidth, atlasHeight, TextureFormat.RGBA32, true, true);
                atlas.ReadPixels(new Rect(0, 0, atlasWidth, atlasHeight), 0, 0);
                atlas.Apply();

                return atlas;
            }
            finally
            {
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(atlasRT);
            }
        }

        // Handles reading texture data, rotating it using a Burst job, and uploading it to the atlas.
        private void ProcessRotatedTexture(Texture2D sourceTex, RenderTexture atlasRT, int destX, int destY, int targetWidth, int targetHeight)
        {
            if (!TextureUtils.TryGetPixelDataNative(sourceTex, out NativeArray<Color32> sourcePixels, Allocator.TempJob))
            {
                Debug.LogError($"[CombinatorCore] Failed to read pixels from texture {sourceTex.name} for rotation.");
                return;
            }

            NativeArray<Color32> rotatedPixels = new NativeArray<Color32>(sourcePixels.Length, Allocator.TempJob);
            Texture2D tempRotatedTex = null;

            try
            {
                int sourceWidth = sourceTex.width;
                int sourceHeight = sourceTex.height;
                int rotatedWidth = sourceHeight;
                int rotatedHeight = sourceWidth;

                var rotateJob = new RotatePixels90CWJob
                {
                    InputPixels = sourcePixels,
                    OutputPixels = rotatedPixels,
                    SourceWidth = sourceWidth,
                    SourceHeight = sourceHeight
                };

                JobHandle handle = rotateJob.Schedule(rotatedPixels.Length, 64);
                handle.Complete();

                tempRotatedTex = new Texture2D(rotatedWidth, rotatedHeight, TextureFormat.RGBA32, false, true);
                tempRotatedTex.LoadRawTextureData(rotatedPixels);
                tempRotatedTex.Apply(false, true);

                int copyWidth = Mathf.Min(rotatedWidth, targetWidth);
                int copyHeight = Mathf.Min(rotatedHeight, targetHeight);

                Graphics.DrawTexture(new Rect(destX, destY, copyWidth, copyHeight), tempRotatedTex, BlitMaterial);
            }
            finally
            {
                if (sourcePixels.IsCreated) sourcePixels.Dispose();
                if (rotatedPixels.IsCreated) rotatedPixels.Dispose();

                if (tempRotatedTex != null)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(tempRotatedTex);
                    else UnityEngine.Object.DestroyImmediate(tempRotatedTex);
                }
            }
        }

        #endregion

        #region Step 4: Mesh UV Remapping & Extraction

        private struct SubmeshKey
        {
            public object MaterialKey;
            public int OriginalIndex;
            public SubmeshKey(object materialKey, int originalIndex)
            {
                MaterialKey = materialKey;
                OriginalIndex = originalIndex;
            }
        }

        private void GenerateMeshes(CombinationResult combinationResult)
        {
            for (int objIndex = 0; objIndex < sourceObjects.Count; objIndex++)
            {
                SourceObject sourceObj = sourceObjects[objIndex];
                Mesh sourceMesh = sourceObj.SourceMesh;

                Vector3[] oldVertices = sourceMesh.vertices;
                Vector3[] oldNormals = sourceMesh.normals;
                Vector4[] oldTangents = sourceMesh.tangents;
                Color32[] oldColors = sourceMesh.colors32;
                BoneWeight[] oldBoneWeights = sourceMesh.boneWeights;

                List<List<Vector4>> oldUvs = new List<List<Vector4>>();
                for (int i = 0; i < 8; i++)
                {
                    List<Vector4> uvs = new List<Vector4>();
                    sourceMesh.GetUVs(i, uvs);
                    oldUvs.Add(uvs);
                }

                List<Vector3> newVertices = new List<Vector3>();
                List<Vector3> newNormals = oldNormals != null && oldNormals.Length > 0 ? new List<Vector3>() : null;
                List<Vector4> newTangents = oldTangents != null && oldTangents.Length > 0 ? new List<Vector4>() : null;
                List<Color32> newColors = oldColors != null && oldColors.Length > 0 ? new List<Color32>() : null;
                List<BoneWeight> newBoneWeights = oldBoneWeights != null && oldBoneWeights.Length > 0 ? new List<BoneWeight>() : null;

                List<List<Vector4>> newUvs = new List<List<Vector4>>();
                for (int i = 0; i < 8; i++)
                {
                    newUvs.Add(oldUvs[i].Count > 0 ? new List<Vector4>() : null);
                }

                Dictionary<object, List<int>> outputSubmeshes = new Dictionary<object, List<int>>();
                List<object> orderedOutputKeys = new List<object>();

                // Cache deduplicates shared vertices UNLESS they require conflicting atlas UV rects
                Dictionary<(int, PackingInfo), int> globalVertexMap = new Dictionary<(int, PackingInfo), int>();

                for (int i = 0; i < sourceMesh.subMeshCount; i++)
                {
                    if (i >= sourceObj.SharedMaterials.Length) continue;

                    Material sourceMaterial = sourceObj.SharedMaterials[i];
                    object groupKey = sourceMaterial != null ? (object)sourceMaterial : $"NULL_MATERIAL_{i}";
                    PackingInfo packingInfo = null;

                    if (sourceMaterial != null && sourceMaterial.shader != null &&
                        shaderGroups.TryGetValue(sourceMaterial.shader, out MaterialGroup materialGroup) &&
                        combinationResult.AtlasResults.TryGetValue(materialGroup, out AtlasResult atlasResult) &&
                        atlasResult.MaterialPackingInfo.TryGetValue(sourceMaterial, out PackingInfo outPackingInfo))
                    {
                        if (materialGroup.UniqueMaterials.Count > 1)
                        {
                            groupKey = materialGroup;
                        }
                        packingInfo = outPackingInfo;
                    }

                    // Handles the optional submesh combination routing safely
                    object finalKey = settings.CombineSubmeshes ? groupKey : new SubmeshKey(groupKey, i);

                    if (!outputSubmeshes.ContainsKey(finalKey))
                    {
                        outputSubmeshes[finalKey] = new List<int>();
                        orderedOutputKeys.Add(finalKey);
                    }

                    List<int> currentOutputIndices = outputSubmeshes[finalKey];
                    int[] submeshIndices = sourceMesh.GetTriangles(i);

                    foreach (int oldIndex in submeshIndices)
                    {
                        var cacheKey = (oldIndex, packingInfo);
                        if (!globalVertexMap.TryGetValue(cacheKey, out int newIndex))
                        {
                            newIndex = newVertices.Count;
                            globalVertexMap[cacheKey] = newIndex;

                            newVertices.Add(oldVertices[oldIndex]);
                            if (newNormals != null) newNormals.Add(oldNormals[oldIndex]);
                            if (newTangents != null) newTangents.Add(oldTangents[oldIndex]);
                            if (newColors != null) newColors.Add(oldColors[oldIndex]);
                            if (newBoneWeights != null) newBoneWeights.Add(oldBoneWeights[oldIndex]);

                            for (int uvChannel = 0; uvChannel < 8; uvChannel++)
                            {
                                if (newUvs[uvChannel] != null)
                                {
                                    Vector4 uv = oldUvs[uvChannel][oldIndex];

                                    if (uvChannel == 0 && packingInfo != null)
                                    {
                                        float u = uv.x % 1.0f;
                                        float v = uv.y % 1.0f;
                                        if (u < 0) u += 1.0f;
                                        if (v < 0) v += 1.0f;

                                        // Protects the boundary to completely eliminate atlas tearing
                                        if (u == 0.0f && uv.x > 0.0f) u = 1.0f;
                                        if (v == 0.0f && uv.y > 0.0f) v = 1.0f;

                                        if (packingInfo.IsRotated)
                                        {
                                            float rotatedU = 1.0f - v;
                                            float rotatedV = u;
                                            uv.x = packingInfo.UVRect.x + (rotatedU * packingInfo.UVRect.width);
                                            uv.y = packingInfo.UVRect.y + (rotatedV * packingInfo.UVRect.height);
                                        }
                                        else
                                        {
                                            uv.x = packingInfo.UVRect.x + (u * packingInfo.UVRect.width);
                                            uv.y = packingInfo.UVRect.y + (v * packingInfo.UVRect.height);
                                        }
                                    }
                                    newUvs[uvChannel].Add(uv);
                                }
                            }
                        }
                        currentOutputIndices.Add(newIndex);
                    }
                }

                if (newVertices.Count == 0) continue;

                Mesh newCombinedMesh = new Mesh
                {
                    name = $"{sourceMesh.name}_Optimized"
                };

                if (newVertices.Count > 65535)
                {
                    newCombinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                }

                newCombinedMesh.SetVertices(newVertices);
                if (newNormals != null) newCombinedMesh.SetNormals(newNormals);
                if (newTangents != null) newCombinedMesh.SetTangents(newTangents);
                if (newColors != null) newCombinedMesh.SetColors(newColors);
                if (newBoneWeights != null) newCombinedMesh.boneWeights = newBoneWeights.ToArray();
                if (sourceMesh.bindposes != null && sourceMesh.bindposes.Length > 0) newCombinedMesh.bindposes = sourceMesh.bindposes;

                for (int i = 0; i < 8; i++)
                {
                    if (newUvs[i] != null)
                        newCombinedMesh.SetUVs(i, newUvs[i]);
                }

                newCombinedMesh.subMeshCount = orderedOutputKeys.Count;
                List<object> finalSubmeshKeys = new List<object>();

                for (int i = 0; i < orderedOutputKeys.Count; i++)
                {
                    var key = orderedOutputKeys[i];
                    newCombinedMesh.SetTriangles(outputSubmeshes[key], i);

                    // Unwrap the SubmeshKey safely back to the raw Material/Group for mapping
                    if (!settings.CombineSubmeshes && key is SubmeshKey sk)
                    {
                        finalSubmeshKeys.Add(sk.MaterialKey);
                    }
                    else
                    {
                        finalSubmeshKeys.Add(key);
                    }
                }

                newCombinedMesh.RecalculateBounds();

                combinationResult.GeneratedMeshes[sourceObj.Identifier] = new MeshResult
                {
                    Mesh = newCombinedMesh,
                    SubmeshKeys = finalSubmeshKeys
                };
            }
        }
        #endregion

        #region Helpers (Dummy Results, Conversion, Configuration)

        private AtlasResult CreateDummyAtlasResult(MaterialGroup group)
        {
            var singleItemResult = new AtlasResult();
            Material originalMat = group.UniqueMaterials.FirstOrDefault();
            if (originalMat != null)
            {
                int fallbackSize = Mathf.Max(1, settings.FallbackTextureSize);
                singleItemResult.GeneratedMaterial = originalMat;

                singleItemResult.AtlasWidth = fallbackSize;
                singleItemResult.AtlasHeight = fallbackSize;
                singleItemResult.MaterialPackingInfo[originalMat] = new PackingInfo
                {
                    PixelRect = new Rect(0, 0, fallbackSize, fallbackSize),
                    UVRect = new Rect(0, 0, 1, 1),
                    IsRotated = false
                };
            }
            return singleItemResult;
        }

        private AtlasResult CreateNoTextureAtlasResult(MaterialGroup group)
        {
            int fallbackSize = Mathf.Max(1, settings.FallbackTextureSize);
            var simpleResult = new AtlasResult
            {
                AtlasWidth = fallbackSize,
                AtlasHeight = fallbackSize
            };
            foreach (var mat in group.UniqueMaterials)
            {
                simpleResult.MaterialPackingInfo[mat] = new PackingInfo
                {
                    PixelRect = new Rect(0, 0, fallbackSize, fallbackSize),
                    UVRect = new Rect(0, 0, 1, 1),
                    IsRotated = false
                };
            }
            return simpleResult;
        }

        private AtlasResult ConvertPackingResult(AtlasPacker.PackingResult packingResult)
        {
            AtlasResult atlasResult = new()
            {
                AtlasWidth = packingResult.AtlasWidth,
                AtlasHeight = packingResult.AtlasHeight
            };

            float atlasWidthFloat = (float)packingResult.AtlasWidth;
            float atlasHeightFloat = (float)packingResult.AtlasHeight;

            foreach (var kvp in packingResult.PixelRects)
            {
                Material mat = kvp.Key;
                PackingInfo info = kvp.Value;

                // Calculate normalized UV coordinates (Handles non-square dimensions)
                info.UVRect = new Rect(
     info.PixelRect.x / atlasWidthFloat,
     info.PixelRect.y / atlasHeightFloat,
     info.PixelRect.width / atlasWidthFloat,
     info.PixelRect.height / atlasHeightFloat
    );

                atlasResult.MaterialPackingInfo[mat] = info;
            }
            return atlasResult;
        }

        private void ConfigureCombinedMaterial(Material combinedMaterial, MaterialGroup group)
        {
            if (group.UniqueMaterials.Count > 0)
            {
                // Start by copying properties, keywords, and render queue from the first material
                combinedMaterial.CopyPropertiesFromMaterial(group.UniqueMaterials[0]);
                combinedMaterial.shaderKeywords = group.UniqueMaterials[0].shaderKeywords;
                combinedMaterial.renderQueue = group.UniqueMaterials[0].renderQueue;
            }

            // Ensure Tiling/Offset are reset for properties that will use an atlas
            foreach (var propertyName in group.UsedTextureProperties)
            {
                if (combinedMaterial.HasProperty(propertyName))
                {
                    combinedMaterial.SetTextureScale(propertyName, Vector2.one);
                    combinedMaterial.SetTextureOffset(propertyName, Vector2.zero);
                }
            }

            // Average non-texture properties
            AverageMaterialProperties(combinedMaterial, group);
        }

        private void AverageMaterialProperties(Material combinedMaterial, MaterialGroup group)
        {
            if (group.UniqueMaterials.Count <= 1) return;

            Shader shader = group.SourceShader;
            int propertyCount = shader.GetPropertyCount();

            for (int i = 0; i < propertyCount; i++)
            {
                string propertyName = shader.GetPropertyName(i);
                var propertyType = shader.GetPropertyType(i);

                // Average Colors
                if (propertyType == UnityEngine.Rendering.ShaderPropertyType.Color)
                {
                    Color avgColor = Color.clear;
                    int count = 0;
                    foreach (var mat in group.UniqueMaterials)
                    {
                        if (mat.HasProperty(propertyName))
                        {
                            avgColor += mat.GetColor(propertyName);
                            count++;
                        }
                    }
                    if (count > 0)
                    {
                        combinedMaterial.SetColor(propertyName, avgColor / count);
                    }
                }
                // Average Floats and Ranges
                else if (propertyType == UnityEngine.Rendering.ShaderPropertyType.Float || propertyType == UnityEngine.Rendering.ShaderPropertyType.Range)
                {
                    float avgValue = 0f;
                    int count = 0;
                    foreach (var mat in group.UniqueMaterials)
                    {
                        if (mat.HasProperty(propertyName))
                        {
                            avgValue += mat.GetFloat(propertyName);
                            count++;
                        }
                    }
                    if (count > 0)
                    {
                        combinedMaterial.SetFloat(propertyName, avgValue / count);
                    }
                }
                // Average Vectors
                else if (propertyType == UnityEngine.Rendering.ShaderPropertyType.Vector)
                {
                    Vector4 avgVector = Vector4.zero;
                    int count = 0;
                    foreach (var mat in group.UniqueMaterials)
                    {
                        if (mat.HasProperty(propertyName))
                        {
                            avgVector += mat.GetVector(propertyName);
                            count++;
                        }
                    }
                    if (count > 0)
                    {
                        combinedMaterial.SetVector(propertyName, avgVector / count);
                    }
                }
            }
        }
        #endregion

        #region Helper Material
        // Helper material for blitting textures, ensures we have a valid material for Graphics.Blit.
        private static Material _blitMaterial;
        private static Material BlitMaterial
        {
            get
            {
                if (_blitMaterial == null)
                {
                    // Try a few common names for Unity's internal blit shader.
                    var shader = Shader.Find("Hidden/BlitCopy");
                    if (shader == null) shader = Shader.Find("Hidden/Internal-BlitCopy");

                    if (shader == null)
                    {
                        // If the internal ones aren't found, use a safe public fallback and issue a warning.
                        Debug.LogWarning("[CombinatorCore] Could not find internal blit shaders. Using fallback 'Unlit/Texture'. This is safe.");
                        shader = Shader.Find("Unlit/Texture");
                    }
                    _blitMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                }
                return _blitMaterial;
            }
        }
        #endregion
    }
}