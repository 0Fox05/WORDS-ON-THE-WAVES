#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Veridian.Perspective.Combinator.Demo.Editor
{
    public static class ProceduralTextureSynthesizer
    {
        public static Texture2D GenerateNoiseTexture(int width, int height, Color colorA, Color colorB, float scaleX, float scaleY)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, true);
            Color[] pixels = new Color[width * height];

            float offsetX = Random.Range(0f, 100f);
            float offsetY = Random.Range(0f, 100f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float u = (float)x / width * scaleX + offsetX;
                    float v = (float)y / height * scaleY + offsetY;

                    float noise1 = Mathf.PerlinNoise(u, v);
                    float noise2 = Mathf.PerlinNoise(u * 2.5f, v * 2.5f) * 0.5f;
                    float finalNoise = Mathf.Clamp01((noise1 + noise2) / 1.5f);

                    pixels[y * width + x] = Color.Lerp(colorA, colorB, finalNoise);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(true);
            return tex;
        }

        public static Texture2D SaveTexture(Texture2D tex, string name, string folder)
        {
            if (tex == null) return null;
            byte[] bytes = tex.EncodeToPNG();
            string path = $"{folder}/{name}.png";
            File.WriteAllBytes(path, bytes);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public static Material CreateURPLitMaterial(string path, string name, Texture2D texture)
        {
            string assetPath = $"{path}/{name}.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);

            Shader targetShader = Shader.Find("Universal Render Pipeline/Simple Lit") ??
                                  Shader.Find("Universal Render Pipeline/Lit") ??
                                  Shader.Find("Standard");

            if (targetShader == null) return null;

            Material mat = existing != null ? existing : new Material(targetShader);

            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
            else if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", texture);

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            else if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);

            mat.enableInstancing = true;

            if (existing == null)
            {
                AssetDatabase.CreateAsset(mat, assetPath);
            }
            else
            {
                EditorUtility.SetDirty(mat);
            }

            // Lock the texture assignment into the asset database immediately
            AssetDatabase.SaveAssets();

            return mat;
        }
    }
}
#endif