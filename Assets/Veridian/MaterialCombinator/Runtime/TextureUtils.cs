using UnityEngine;
using Unity.Collections;

namespace Veridian.Perspective.Combinator
{
    /// <summary>
    /// Runtime-safe utilities for texture manipulation.
    /// </summary>
    public static class TextureUtils
    {
        /// <summary>
        /// Reads pixels from a texture, handling non-readable textures by copying via RenderTexture (GPU).
        /// Runtime safe. Ensures data is read linearly.
        /// </summary>
        public static Color[] GetPixels(Texture2D texture)
        {
            if (texture == null) return new Color[0];

            if (TryGetPixelDataNative(texture, out NativeArray<Color32> nativePixels, Allocator.TempJob))
            {
                try
                {
                    Color[] managedPixels = new Color[nativePixels.Length];
                    for (int i = 0; i < nativePixels.Length; i++)
                    {
                        managedPixels[i] = nativePixels[i];
                    }
                    return managedPixels;
                }
                finally
                {
                    if (nativePixels.IsCreated) nativePixels.Dispose();
                }
            }

            Debug.LogError($"[TextureUtils] Failed to get pixels for texture: {texture.name}");
            return new Color[0];
        }

        /// <summary>
        /// Attempts to read pixel data into a NativeArray<Color32> suitable for use in the Job System.
        /// Handles non-readable textures via GPU readback. Ensures data is Linear and compatible with Color32.
        /// The resulting NativeArray is allocated with the specified allocator and MUST be disposed by the caller.
        /// </summary>
        public static bool TryGetPixelDataNative(Texture2D texture, out NativeArray<Color32> pixels, Allocator allocator)
        {
            pixels = default;
            if (texture == null) return false;

            int width = texture.width;
            int height = texture.height;

            if (texture.isReadable)
            {
                try
                {
                    var rawData = texture.GetPixelData<Color32>(0);
                    pixels = new NativeArray<Color32>(rawData.Length, allocator);
                    NativeArray<Color32>.Copy(rawData, pixels);
                    return true;
                }
                catch (UnityException)
                {
                    if (pixels.IsCreated) pixels.Dispose();

                    try
                    {
                        Color32[] cpuPixels = texture.GetPixels32();
                        pixels = new NativeArray<Color32>(cpuPixels.Length, allocator);
                        pixels.CopyFrom(cpuPixels);
                        return true;
                    }
                    catch (UnityException)
                    {
                        if (pixels.IsCreated) pixels.Dispose();
                    }
                }
            }

            RenderTexture previousRT = RenderTexture.active;
            RenderTexture tempRT = RenderTexture.GetTemporary(
                width,
                height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear);

            Texture2D tempTexture = null;
            try
            {
                Graphics.Blit(texture, tempRT);
                RenderTexture.active = tempRT;

                tempTexture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
                tempTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tempTexture.Apply();

                var rawDataNonReadable = tempTexture.GetPixelData<Color32>(0);
                pixels = new NativeArray<Color32>(rawDataNonReadable.Length, allocator);
                NativeArray<Color32>.Copy(rawDataNonReadable, pixels);

                return true;
            }
            catch
            {
                if (pixels.IsCreated) pixels.Dispose();
                return false;
            }
            finally
            {
                RenderTexture.active = previousRT;
                RenderTexture.ReleaseTemporary(tempRT);

                if (tempTexture != null)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(tempTexture);
                    else UnityEngine.Object.DestroyImmediate(tempTexture);
                }
            }
        }
    }
}