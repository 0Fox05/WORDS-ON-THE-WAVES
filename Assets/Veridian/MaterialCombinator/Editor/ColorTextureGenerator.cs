using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
namespace Veridian.Perspective.Combinator.Editor
{
    public class ColorTextureGenerator
    {
        // --- You can change your colors and texture names here ---
        private static readonly Dictionary<string, Color32> colorDefinitions = new()
        {
        { "Color_ReddishBrown", new Color32(139, 69, 19, 255) },
        { "Color_BrightRed",    new Color32(255, 0, 0, 255) },
        { "Color_LightSkyBlue", new Color32(135, 206, 250, 255) },
        { "Color_DarkGray",     new Color32(80, 80, 80, 255) }, // Darker than default gray
        { "Color_Black",        new Color32(0, 0, 0, 255) }
        // Add more colors here (e.g., "Color_White", new Color32(255, 255, 255, 255))
    };

        private const int TEXTURE_WIDTH = 8;
        private const int TEXTURE_HEIGHT = 8;
        private const string OUTPUT_FOLDER = "GeneratedTextures";

        [MenuItem("Assets/Create/Color Textures")]
        private static void GenerateColorTextures()
        {
            string outputRoot = "Assets";
            string outputPath = Path.Combine(outputRoot, OUTPUT_FOLDER);

            // 1. Create the output folder if it doesn't exist
            if (!AssetDatabase.IsValidFolder(outputPath))
            {
                AssetDatabase.CreateFolder(outputRoot, OUTPUT_FOLDER);
                Debug.Log($"Created folder: {outputPath}");
            }

            int pixelCount = TEXTURE_WIDTH * TEXTURE_HEIGHT;

            Debug.Log($"Generating {colorDefinitions.Count} color textures...");

            foreach (var entry in colorDefinitions)
            {
                string fileName = entry.Key + ".png";
                string filePath = Path.Combine(outputPath, fileName);
                Color32 color = entry.Value;

                // 2. Create the texture and fill it with color
                Texture2D tex = new(TEXTURE_WIDTH, TEXTURE_HEIGHT, TextureFormat.RGBA32, false);

                // Create a color array for SetPixels (much faster)
                Color32[] pixels = new Color32[pixelCount];
                for (int i = 0; i < pixelCount; i++)
                {
                    pixels[i] = color;
                }

                tex.SetPixels32(pixels);
                tex.Apply();

                // 3. Encode to PNG and save to disk
                byte[] bytes = tex.EncodeToPNG();
                File.WriteAllBytes(filePath, bytes);

                // 4. Clean up the in-memory texture
                Object.DestroyImmediate(tex);

                // 5. Tell Unity to import the new file
                AssetDatabase.ImportAsset(filePath);

                // Optional: Set texture import settings (e.g., no compression, point filter)
                TextureImporter importer = AssetImporter.GetAtPath(filePath) as TextureImporter;
                if (importer != null)
                {
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.filterMode = FilterMode.Point;
                    importer.SaveAndReimport();
                }
            }

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Success",
                $"Generated {colorDefinitions.Count} textures in:\n{outputPath}",
                "OK");

            // Ping the folder to highlight it
            Object outputDirObject = AssetDatabase.LoadAssetAtPath<Object>(outputPath);
            if (outputDirObject != null)
            {
                EditorGUIUtility.PingObject(outputDirObject);
            }
        }
    }
}