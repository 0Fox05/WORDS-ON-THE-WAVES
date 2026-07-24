using System.Collections.Generic;
using UnityEngine;

namespace Veridian.Perspective.Combinator
{
    /// <summary>
    /// Represents a single object involved in the combination process. (Runtime Safe)
    /// </summary>
    public class SourceObject
    {
        public Material[] SharedMaterials;
        public Mesh SourceMesh;

        // We use a flexible identifier mechanism. The Editor implementation will use the (RootPrefab, TransformPath) tuple.
        // This allows the Core logic to track objects without knowing how they are stored/referenced in the environment.
        public object Identifier;

        public SourceObject(Material[] materials, Mesh mesh, object identifier)
        {
            SharedMaterials = materials;
            SourceMesh = mesh;
            Identifier = identifier;
        }
    }

    /// <summary>
    /// Represents a group of materials that share the same shader. (Runtime Safe)
    /// </summary>
    public class MaterialGroup
    {
        public Shader SourceShader;
        public List<Material> UniqueMaterials = new();
        // Maps an original material to the list of objects using it.
        public Dictionary<Material, List<SourceObject>> MaterialToObjectMap = new();
        // List of all texture property names found in the shader definition.
        public List<string> TextureProperties = new();
        // This set is also used to store the final selection of properties to process after filtering.
        public HashSet<string> UsedTextureProperties = new();

        public MaterialGroup(Shader shader)
        {
            SourceShader = shader;
            IdentifyTextureProperties(shader);
        }

        public void AddObject(Material mat, SourceObject obj)
        {
            if (!UniqueMaterials.Contains(mat))
            {
                UniqueMaterials.Add(mat);
                MaterialToObjectMap[mat] = new List<SourceObject>();
                // Analyze usage when the material is first added to the group.
                AnalyzeMaterialUsage(mat);
            }
            MaterialToObjectMap[mat].Add(obj);
        }

        // Dynamically inspect the shader definition to find all texture properties.
        private void IdentifyTextureProperties(Shader shader)
        {
            // A list of built-in Unity texture properties that should not be atlased.
            var propertiesToIgnore = new HashSet<string>
      {
        "unity_Lightmaps",
        "unity_LightmapsInd",
        "unity_ShadowMasks"
      };

            int propertyCount = shader.GetPropertyCount();
            for (int i = 0; i < propertyCount; i++)
            {
                if (shader.GetPropertyType(i) == UnityEngine.Rendering.ShaderPropertyType.Texture)
                {
                    string propertyName = shader.GetPropertyName(i);
                    if (!propertiesToIgnore.Contains(propertyName))
                    {
                        TextureProperties.Add(propertyName);
                    }
                }
            }
        }

        private void AnalyzeMaterialUsage(Material mat)
        {
            foreach (var propName in TextureProperties)
            {
                // We check if the material has the property AND if a texture is assigned.
                if (mat.HasProperty(propName) && mat.GetTexture(propName) != null)
                {
                    UsedTextureProperties.Add(propName);
                }
            }
        }
    }

    /// <summary>
    /// Holds information about how a specific texture was packed into the atlas. (Runtime Safe)
    /// </summary>
    public class PackingInfo
    {
        public Rect PixelRect;     // The rectangle in pixel coordinates within the atlas.
        public Rect UVRect;        // The normalized UV coordinates (0-1).
        public bool IsRotated;     // Whether the texture was rotated 90 degrees during packing.
    }

    /// <summary>
    /// Holds the results of the texture atlasing process for a specific MaterialGroup. (Runtime Safe)
    /// </summary>
    public class AtlasResult
    {
        // This may be the original material if the group had only 1 item.
        public Material GeneratedMaterial;

        // The final calculated dimensions of the atlas (e.g., 2048x2048 or 1536x1024)
        public int AtlasWidth;
        public int AtlasHeight;

        // Maps the original Material to its packing information.
        public Dictionary<Material, PackingInfo> MaterialPackingInfo = new();

        // Maps the texture property name to the generated atlas texture (in memory).
        public Dictionary<string, Texture2D> GeneratedAtlases = new();
    }
}