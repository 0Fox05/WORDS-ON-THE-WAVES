#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace Veridian.Perspective.Combinator.Editor
{
    public static class AssetUtils
    {
    // Constants for default output locations
    private const string DefaultCombinatorOutput = "Assets/CombinatorOutput";

        /// <summary>
            /// Creates a unique, timestamped output folder for the Combinator.
            /// </summary>
        public static string CreateCombinatorOutputFolder(string parentFolder, string baseName)
        {
            if (string.IsNullOrEmpty(parentFolder))
            {
                parentFolder = "Assets/CombinatorOutput";
            }
            return CreateTimestampedFolder(parentFolder, baseName);
        }

        private static string CreateTimestampedFolder(string parentFolder, string baseName)
        {
            // (Logic from the prompt is retained here, condensed for brevity)
      if (!AssetDatabase.IsValidFolder(parentFolder))
            {
                // Simplified robust creation
                if (!System.IO.Directory.Exists(parentFolder))
                {
                    try
                    {
                        System.IO.Directory.CreateDirectory(parentFolder);
                        AssetDatabase.Refresh();
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[AssetUtils] Failed to create directory {parentFolder}. Error: {ex.Message}");
                        return null;
                    }
                }
            }

            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string folderName = $"{baseName}_{timestamp}";
            string folderPath = $"{parentFolder}/{folderName}";

      // Ensure uniqueness
      folderPath = AssetDatabase.GenerateUniqueAssetPath(folderPath);
            folderName = System.IO.Path.GetFileName(folderPath);

            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder(parentFolder, folderName);
            }

            return folderPath;
        }

        /// <summary>
            /// Saves a Texture2D as a PNG/EXR asset and configures its import settings.
            /// </summary>
        public static Texture2D SaveTexture(Texture2D texture, string path, string name, bool allowHDRtoLDR = false)
        {
            if (string.IsNullOrEmpty(path) || !AssetDatabase.IsValidFolder(path))
            {
                Debug.LogError($"[AssetUtils] Invalid path for saving texture: {path}");
                return null;
            }

            bool isHDR = TextureProcessor.IsHDR(texture.format);
            string extension;
            byte[] bytes;

            if (isHDR && !allowHDRtoLDR)
            {
                extension = ".exr";
                bytes = texture.EncodeToEXR(Texture2D.EXRFlags.CompressZIP);
            }
            else
            {
                extension = ".png";
                bytes = texture.EncodeToPNG();
            }

            string assetPath = $"{path}/{name}{extension}";

            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

            System.IO.File.WriteAllBytes(assetPath, bytes);
            AssetDatabase.ImportAsset(assetPath);

            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.mipmapEnabled = true;

                string lowerName = name.ToLower();
                if (lowerName.Contains("normal") || lowerName.Contains("bump"))
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.sRGBTexture = false;
                    importer.textureCompression = TextureImporterCompression.CompressedHQ;
                }
                else
                {
                    importer.sRGBTexture = !(isHDR && !allowHDRtoLDR) &&
                     !lowerName.Contains("metallic") && !lowerName.Contains("mask") &&
                     !lowerName.Contains("occlusion") && !lowerName.Contains("roughness") && !lowerName.Contains("smoothness");

                    importer.textureCompression = TextureImporterCompression.Compressed;
                }
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

    public static Material SaveMaterial(Material material, string path, string name)
        {
            if (string.IsNullOrEmpty(path) || !AssetDatabase.IsValidFolder(path)) return null;
            string assetPath = $"{path}/{name}.mat";
            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
            AssetDatabase.CreateAsset(material, assetPath);
            return material;
        }

        public static Mesh SaveMesh(Mesh mesh, string path, string name)
        {
            if (string.IsNullOrEmpty(path) || !AssetDatabase.IsValidFolder(path)) return null;
            string assetPath = $"{path}/{name}.mesh";
            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
            MeshUtility.Optimize(mesh);
            AssetDatabase.CreateAsset(mesh, assetPath);
            return mesh;
        }

        public static GameObject SavePrefab(GameObject gameObject, string path, string name)
        {
            if (string.IsNullOrEmpty(path) || !AssetDatabase.IsValidFolder(path)) return null;
            string assetPath = $"{path}/{name}.prefab";
            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(gameObject, assetPath, out bool success);
            if (!success)
            {
                Debug.LogError($"[AssetUtils] Failed to save prefab at {assetPath}");
            }
            return prefab;
        }

        public static Color[] GetPixels(Texture2D texture)
        {
            if (texture.isReadable)
            {
                return texture.GetPixels();
            }

            RenderTexture previousRT = RenderTexture.active;
            RenderTexture tempRT = RenderTexture.GetTemporary(
              texture.width,
              texture.height,
              0,
              RenderTextureFormat.Default,
              RenderTextureReadWrite.Linear);

            Graphics.Blit(texture, tempRT);
            RenderTexture.active = tempRT;

            Texture2D tempTexture = new(texture.width, texture.height);
            tempTexture.ReadPixels(new Rect(0, 0, tempRT.width, tempRT.height), 0, 0);
            tempTexture.Apply();

            RenderTexture.active = previousRT;
            RenderTexture.ReleaseTemporary(tempRT);
            Color[] pixels = tempTexture.GetPixels();
            Object.DestroyImmediate(tempTexture);

            return pixels;
        }

        public static bool DeleteFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                bool success = AssetDatabase.DeleteAsset(path);
                if (!success)
                {
                    Debug.LogError($"[AssetUtils] Failed to delete folder: {path}");
                }
                return success;
            }
            return false;
        }
    }
}
#endif