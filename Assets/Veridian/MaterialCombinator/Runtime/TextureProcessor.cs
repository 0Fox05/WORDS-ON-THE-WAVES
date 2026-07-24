// Filename: TextureProcessor.cs
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Veridian.Perspective.Combinator
{
    /// <summary>
    /// A self-contained, static utility class for performing high-quality,
    /// GPU-accelerated texture operations like resizing and format conversion.
    /// </summary>
    public static class TextureProcessor
    {
        #region Core Public Methods

        /// <summary>
        /// Resizes an in-memory Texture2D by a given scale factor. This process is
        /// HDR-aware, color-space aware, and correctly renormalizes normal maps.
        /// </summary>
        public static Texture2D Resize(Texture2D sourceTexture, float scaleFactor, bool isLinear, bool normalizeVectors)
        {
            if (sourceTexture == null) return null;

            if (scaleFactor >= 1.0f - Mathf.Epsilon)
            {
                var copy = new Texture2D(sourceTexture.width, sourceTexture.height, sourceTexture.format, sourceTexture.mipmapCount > 1, isLinear);
                Graphics.CopyTexture(sourceTexture, copy);
                return copy;
            }

            int newWidth = Mathf.Max(1, Mathf.RoundToInt(sourceTexture.width * scaleFactor));
            int newHeight = Mathf.Max(1, Mathf.RoundToInt(sourceTexture.height * scaleFactor));

            RenderTextureReadWrite readWrite = isLinear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB;
            RenderTextureFormat rtFormat = GetProcessingRTFormat(IsHDR(sourceTexture.format));

            RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight, 0, rtFormat, readWrite);
            rt.filterMode = FilterMode.Bilinear;

            try
            {
                if (UnlitBlitMaterial != null)
                {
                    Graphics.Blit(sourceTexture, rt, UnlitBlitMaterial, 0);
                }
                else
                {
                    Graphics.Blit(sourceTexture, rt);
                }

                Texture2D newTexture = ReadRenderTexture(rt, isLinear);

                if (normalizeVectors && newTexture != null)
                {
                    NormalizeNormalMap(newTexture);
                    newTexture.Apply(true, false);
                }

                return newTexture;
            }
            finally
            {
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        /// <summary>
        /// Checks if a TextureFormat is a High Dynamic Range (HDR) format.
        /// </summary>
        public static bool IsHDR(TextureFormat format)
        {
            return HDRFormats.Contains(format);
        }

        #endregion

        #region Internal Processing Logic

        private static readonly HashSet<TextureFormat> HDRFormats = new()
        {
            TextureFormat.RGBAHalf, TextureFormat.RGBAFloat,
            TextureFormat.RHalf, TextureFormat.RFloat,
            TextureFormat.RGHalf, TextureFormat.RGFloat,
            TextureFormat.BC6H
        };

        private static Material _unlitBlitMaterial;
        private static Material UnlitBlitMaterial
        {
            get
            {
                if (_unlitBlitMaterial == null)
                {
                    var shader = Shader.Find("Hidden/Internal-Unlit-Blit");
                    if (shader == null) shader = Shader.Find("Unlit/Texture"); // Fallback

                    if (shader != null)
                    {
                        _unlitBlitMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                    }
                    else
                    {
                        Debug.LogError("[TextureProcessor] Could not find a suitable shader for blitting.");
                    }
                }
                return _unlitBlitMaterial;
            }
        }

        private static RenderTextureFormat GetProcessingRTFormat(bool requireHDR)
        {
            if (requireHDR)
            {
                if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat)) return RenderTextureFormat.ARGBFloat;
                if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)) return RenderTextureFormat.ARGBHalf;
                return RenderTextureFormat.DefaultHDR;
            }
            return RenderTextureFormat.ARGB32;
        }

        private static Texture2D ReadRenderTexture(RenderTexture rt, bool isLinear)
        {
            if (rt == null) return null;

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;

            bool isHDR = rt.format == RenderTextureFormat.DefaultHDR || rt.format == RenderTextureFormat.ARGBFloat || rt.format == RenderTextureFormat.ARGBHalf;
            TextureFormat texFormat = isHDR ? TextureFormat.RGBAFloat : TextureFormat.RGBA32;

            Texture2D tex = new(rt.width, rt.height, texFormat, true, isLinear);
            try
            {
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[TextureProcessor] Failed to read pixels from RenderTexture. Error: {ex.Message}");
                Object.DestroyImmediate(tex);
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
            }

            return tex;
        }

        #endregion

        #region Normalization & Data Conversion Jobs

        private static void NormalizeNormalMap(Texture2D texture)
        {
            if (texture.format == TextureFormat.RGBA32 || texture.format == TextureFormat.ARGB32)
            {
                NormalizeNormalMapLDR(texture);
            }
            else if (texture.format == TextureFormat.RGBAFloat || texture.format == TextureFormat.RGBAHalf)
            {
                NativeArray<float4> pixelData = texture.GetPixelData<float4>(0);
                var job = new NormalizeNormalMapJob { Pixels = pixelData };
                job.Schedule(pixelData.Length, 128).Complete();
            }
            else
            {
                Debug.LogWarning($"[TextureProcessor] Cannot normalize texture with format {texture.format}.");
            }
        }

        private static void NormalizeNormalMapLDR(Texture2D texture)
        {
            var pixelData32 = texture.GetPixelData<Color32>(0);
            var pixelDataFloat4 = new NativeArray<float4>(pixelData32.Length, Allocator.TempJob);

            try
            {
                ConvertColor32ToFloat4(pixelData32, pixelDataFloat4);

                var job = new NormalizeNormalMapJob { Pixels = pixelDataFloat4 };
                job.Schedule(pixelDataFloat4.Length, 128).Complete();

                ConvertFloat4ToColor32(pixelDataFloat4, pixelData32);
            }
            finally
            {
                if (pixelDataFloat4.IsCreated)
                {
                    pixelDataFloat4.Dispose();
                }
            }
        }

        private static void ConvertColor32ToFloat4(NativeArray<Color32> input, NativeArray<float4> output)
        {
            var job = new Convert32ToFloat4Job
            {
                Input = input,
                Output = output,
                Inv255 = 1.0f / 255.0f
            };
            job.Schedule(input.Length, 128).Complete();
        }

        private static void ConvertFloat4ToColor32(NativeArray<float4> input, NativeArray<Color32> output)
        {
            var job = new ConvertFloat4To32Job
            {
                Input = input,
                Output = output
            };
            job.Schedule(input.Length, 128).Complete();
        }

        #endregion
    }
}