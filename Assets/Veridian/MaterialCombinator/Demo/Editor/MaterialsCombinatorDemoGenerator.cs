#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Veridian.Perspective.Combinator.Editor;

namespace Veridian.Perspective.Combinator.Demo.Editor
{
    public class MaterialsCombinatorDemoGenerator : EditorWindow
    {
        public static string OutputPath
        {
            get => EditorPrefs.GetString("Veridian_MatCombDemoGenerator_Path", "Assets/VeridianData/MaterialsCombinator/Demo_Assets");
            set => EditorPrefs.SetString("Veridian_MatCombDemoGenerator_Path", value);
        }

        private Vector2 scrollPosition;

        [MenuItem("Tools/Veridian/Materials Combinator/Demo Control Panel", false, 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<MaterialsCombinatorDemoGenerator>("Demo Control Panel");
            window.minSize = new Vector2(400, 500);
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            GUILayout.Space(10);

            EditorGUILayout.LabelField("Materials Combinator Demo", new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, alignment = TextAnchor.MiddleCenter });
            GUILayout.Space(5);
            EditorGUILayout.HelpBox("This utility procedurally constructs 3D test assets equipped with LODs and dynamically sized generated textures. This provides the perfect environment to safely evaluate the texture packer and mesh fusion logic.", MessageType.Info);

            GUILayout.Space(10);
            OutputPath = EditorGUILayout.TextField("Output Path", OutputPath);
            GUILayout.Space(15);

            bool pineExists = AssetExists("DemoPine");
            bool oakExists = AssetExists("DemoOak");
            bool rockExists = AssetExists("DemoRock");
            bool bushExists = AssetExists("DemoBush");
            bool anyExists = pineExists || oakExists || rockExists || bushExists;
            bool allExists = pineExists && oakExists && rockExists && bushExists;

            GUILayout.Label("1. Asset Factory", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            DrawAssetRow("Pine Tree (128x/256x)", pineExists, GeneratePine);
            DrawAssetRow("Oak Tree (128x/256x)", oakExists, GenerateOak);
            DrawAssetRow("Rock (512x Surface)", rockExists, GenerateRockAsset);
            DrawAssetRow("Bush (256x Leaves)", bushExists, GenerateBushAsset);

            GUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.4f, 0.9f, 0.4f);
            using (new EditorGUI.DisabledScope(allExists))
            {
                if (GUILayout.Button("Generate All Missing", GUILayout.Height(30)))
                {
                    if (!pineExists) GeneratePine();
                    if (!oakExists) GenerateOak();
                    if (!rockExists) GenerateRockAsset();
                    if (!bushExists) GenerateBushAsset();
                }
            }
            GUI.backgroundColor = new Color(0.9f, 0.4f, 0.4f);
            using (new EditorGUI.DisabledScope(!anyExists))
            {
                if (GUILayout.Button("Purge Demo Assets", GUILayout.Height(30)))
                {
                    PurgeAssets();
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            GUILayout.Space(15);

            GUILayout.Label("2. Combinator Integration", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.HelpBox("Pushes generated multi-material assets into the main tool's 'Input Prefabs' list for algorithmic testing.", MessageType.None);

            using (new EditorGUI.DisabledScope(!anyExists))
            {
                if (GUILayout.Button("Push Assets to Combinator", GUILayout.Height(30)))
                {
                    PushToCombinator();
                }
            }

            GUILayout.Space(5);
            if (GUILayout.Button("Open Main Materials Combinator", GUILayout.Height(30)))
            {
                MaterialsCombinatorWindow.ShowWindow();
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);
            EditorGUILayout.EndScrollView();
        }

        private void DrawAssetRow(string label, bool exists, System.Action onGenerate)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(200));
            if (exists)
            {
                GUILayout.Label("Ready", new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.2f, 0.8f, 0.2f) } }, GUILayout.Width(80));
                using (new EditorGUI.DisabledScope(true))
                {
                    GUILayout.Button("Generate");
                }
            }
            else
            {
                GUILayout.Label("Missing", new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.8f, 0.2f, 0.2f) } }, GUILayout.Width(80));
                if (GUILayout.Button("Generate"))
                {
                    onGenerate?.Invoke();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        #region Operational Logic

        private bool AssetExists(string name)
        {
            string p = $"{OutputPath}/Prefabs/{name}.prefab";
            return AssetDatabase.LoadAssetAtPath<GameObject>(p) != null;
        }

        private void EnsureDirectories()
        {
            string texPath = $"{OutputPath}/Textures";
            string matPath = $"{OutputPath}/Materials";
            string meshPath = $"{OutputPath}/Meshes";
            string prefabPath = $"{OutputPath}/Prefabs";

            string[] folders = { OutputPath, texPath, matPath, meshPath, prefabPath };
            bool needsRefresh = false;
            foreach (var f in folders)
            {
                if (!Directory.Exists(f))
                {
                    Directory.CreateDirectory(f);
                    needsRefresh = true;
                }
            }
            if (needsRefresh) AssetDatabase.Refresh();
        }

        private void PurgeAssets()
        {
            if (AssetDatabase.IsValidFolder(OutputPath))
            {
                AssetDatabase.DeleteAsset(OutputPath);
                AssetDatabase.Refresh();
                Debug.Log("[Materials Combinator Demo] Purged demo assets directory.");
            }
        }

        private void PushToCombinator()
        {
            string searchPath = $"{OutputPath}/Prefabs";
            if (!AssetDatabase.IsValidFolder(searchPath)) return;

            string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { searchPath });
            List<GameObject> inputs = new List<GameObject>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    inputs.Add(prefab);
                }
            }

            if (!EditorWindow.HasOpenInstances<MaterialsCombinatorWindow>())
            {
                MaterialsCombinatorWindow.ShowWindow();
            }

            var window = EditorWindow.GetWindow<MaterialsCombinatorWindow>();
            window.InjectDemoPrefabs(inputs);
            window.Focus();
            Debug.Log($"[Materials Combinator Demo] Injected {inputs.Count} procedural prefabs into Combinator.");
        }

        private void GeneratePine()
        {
            EnsureDirectories();
            string texPath = $"{OutputPath}/Textures";
            string matPath = $"{OutputPath}/Materials";
            string meshPath = $"{OutputPath}/Meshes";
            string prefabPath = $"{OutputPath}/Prefabs";

            // 1. Generate raw textures in RAM
            Texture2D rawBarkTex = ProceduralTextureSynthesizer.GenerateNoiseTexture(128, 128, new Color(0.35f, 0.25f, 0.15f), new Color(0.2f, 0.15f, 0.1f), 2.0f, 10.0f);
            Texture2D rawLeafTex = ProceduralTextureSynthesizer.GenerateNoiseTexture(256, 256, new Color(0.12f, 0.35f, 0.15f), new Color(0.05f, 0.2f, 0.05f), 15.0f, 15.0f);

            // 2. Save them to disk and retrieve the persistent asset references
            Texture2D assetBarkTex = ProceduralTextureSynthesizer.SaveTexture(rawBarkTex, "Tex_PineBark", texPath);
            Texture2D assetLeafTex = ProceduralTextureSynthesizer.SaveTexture(rawLeafTex, "Tex_PineLeaves", texPath);

            // 3. Immediately destroy the RAM textures to prevent memory leaks (Do NOT destroy the asset references)
            DestroyImmediate(rawBarkTex);
            DestroyImmediate(rawLeafTex);

            // 4. Assign the persistent assets to the materials
            Material matTrunk = ProceduralTextureSynthesizer.CreateURPLitMaterial(matPath, "Demo_PineTrunkMat", assetBarkTex);
            Material matLeaf = ProceduralTextureSynthesizer.CreateURPLitMaterial(matPath, "Demo_PineLeafMat", assetLeafTex);

            GeneratePineTree(prefabPath, meshPath, matTrunk, matLeaf);

            AssetDatabase.Refresh();
        }

        private void GenerateOak()
        {
            EnsureDirectories();
            string texPath = $"{OutputPath}/Textures";
            string matPath = $"{OutputPath}/Materials";
            string meshPath = $"{OutputPath}/Meshes";
            string prefabPath = $"{OutputPath}/Prefabs";

            Texture2D rawBarkTex = ProceduralTextureSynthesizer.GenerateNoiseTexture(128, 128, new Color(0.4f, 0.3f, 0.2f), new Color(0.25f, 0.2f, 0.15f), 3.0f, 8.0f);
            Texture2D rawLeafTex = ProceduralTextureSynthesizer.GenerateNoiseTexture(256, 256, new Color(0.25f, 0.5f, 0.20f), new Color(0.15f, 0.35f, 0.1f), 10.0f, 10.0f);

            Texture2D assetBarkTex = ProceduralTextureSynthesizer.SaveTexture(rawBarkTex, "Tex_OakBark", texPath);
            Texture2D assetLeafTex = ProceduralTextureSynthesizer.SaveTexture(rawLeafTex, "Tex_OakLeaves", texPath);

            DestroyImmediate(rawBarkTex);
            DestroyImmediate(rawLeafTex);

            Material matTrunk = ProceduralTextureSynthesizer.CreateURPLitMaterial(matPath, "Demo_OakTrunkMat", assetBarkTex);
            Material matOak = ProceduralTextureSynthesizer.CreateURPLitMaterial(matPath, "Demo_OakLeafMat", assetLeafTex);

            GenerateOakTree(prefabPath, meshPath, matTrunk, matOak);

            AssetDatabase.Refresh();
        }

        private void GenerateRockAsset()
        {
            EnsureDirectories();
            string texPath = $"{OutputPath}/Textures";
            string matPath = $"{OutputPath}/Materials";
            string meshPath = $"{OutputPath}/Meshes";
            string prefabPath = $"{OutputPath}/Prefabs";

            Texture2D rawRockTex = ProceduralTextureSynthesizer.GenerateNoiseTexture(512, 512, new Color(0.6f, 0.6f, 0.6f), new Color(0.3f, 0.3f, 0.3f), 8.0f, 8.0f);
            Texture2D assetRockTex = ProceduralTextureSynthesizer.SaveTexture(rawRockTex, "Tex_Rock", texPath);

            DestroyImmediate(rawRockTex);

            Material matRock = ProceduralTextureSynthesizer.CreateURPLitMaterial(matPath, "Demo_RockMat", assetRockTex);

            GenerateRock(prefabPath, meshPath, matRock);

            AssetDatabase.Refresh();
        }

        private void GenerateBushAsset()
        {
            EnsureDirectories();
            string texPath = $"{OutputPath}/Textures";
            string matPath = $"{OutputPath}/Materials";
            string meshPath = $"{OutputPath}/Meshes";
            string prefabPath = $"{OutputPath}/Prefabs";

            Texture2D rawBushTex = ProceduralTextureSynthesizer.GenerateNoiseTexture(256, 256, new Color(0.35f, 0.6f, 0.25f), new Color(0.15f, 0.4f, 0.15f), 12.0f, 12.0f);
            Texture2D assetBushTex = ProceduralTextureSynthesizer.SaveTexture(rawBushTex, "Tex_Bush", texPath);

            DestroyImmediate(rawBushTex);

            Material matBush = ProceduralTextureSynthesizer.CreateURPLitMaterial(matPath, "Demo_BushMat", assetBushTex);

            GenerateBush(prefabPath, meshPath, matBush);

            AssetDatabase.Refresh();
        }

        #endregion

        #region Asset Constructors

        private void GeneratePineTree(string prefabPath, string meshPath, Material trunkMat, Material leafMat)
        {
            Mesh trunkLOD0 = GenerateProceduralCylinder(0.25f, 1.5f, 6, Vector3.zero);
            Mesh leavesLOD0 = GenerateStackedCones(3, 1.8f, 0.1f, 4.5f, 7, new Vector3(0, 1.0f, 0));
            Mesh pineLOD0 = CombineToNewMesh("Pine_LOD0", trunkLOD0, leavesLOD0, meshPath);

            Mesh trunkLOD1 = GetScaledPrimitive(PrimitiveType.Cube, new Vector3(0.4f, 1.5f, 0.4f), new Vector3(0, 0.75f, 0));
            Mesh leavesLOD1 = GenerateStackedCones(1, 1.8f, 0.1f, 4.5f, 4, new Vector3(0, 1.0f, 0));
            Mesh pineLOD1 = CombineToNewMesh("Pine_LOD1", trunkLOD1, leavesLOD1, meshPath);

            CreateLODPrefab("DemoPine", prefabPath, pineLOD0, pineLOD1, new[] { trunkMat, leafMat });

            DestroyImmediate(trunkLOD0); DestroyImmediate(leavesLOD0);
            DestroyImmediate(trunkLOD1); DestroyImmediate(leavesLOD1);
        }

        private void GenerateOakTree(string prefabPath, string meshPath, Material trunkMat, Material leafMat)
        {
            Mesh trunkLOD0 = GenerateProceduralCylinder(0.35f, 2.5f, 6, Vector3.zero);
            Mesh leavesLOD0 = GenerateLumpyIcosphere(2.2f, 0.25f, new Vector3(0, 3.2f, 0));
            Mesh oakLOD0 = CombineToNewMesh("Oak_LOD0", trunkLOD0, leavesLOD0, meshPath);

            Mesh trunkLOD1 = GetScaledPrimitive(PrimitiveType.Cube, new Vector3(0.6f, 2.5f, 0.6f), new Vector3(0, 1.25f, 0));
            Mesh leavesLOD1 = GetScaledPrimitive(PrimitiveType.Cube, new Vector3(3.5f, 3.5f, 3.5f), new Vector3(0, 3.2f, 0));
            Mesh oakLOD1 = CombineToNewMesh("Oak_LOD1", trunkLOD1, leavesLOD1, meshPath);

            CreateLODPrefab("DemoOak", prefabPath, oakLOD0, oakLOD1, new[] { trunkMat, leafMat });

            DestroyImmediate(trunkLOD0); DestroyImmediate(leavesLOD0);
            DestroyImmediate(trunkLOD1); DestroyImmediate(leavesLOD1);
        }

        private void GenerateRock(string prefabPath, string meshPath, Material rockMat)
        {
            Mesh rockLOD0 = GenerateLumpyIcosphere(1.5f, 0.4f, Vector3.zero);
            rockLOD0 = SaveMesh(rockLOD0, "Rock_LOD0", meshPath);

            Mesh rockLOD1 = GetScaledPrimitive(PrimitiveType.Cube, new Vector3(2f, 2f, 2f), Vector3.zero);
            rockLOD1 = SaveMesh(rockLOD1, "Rock_LOD1", meshPath);

            CreateLODPrefabSingle("DemoRock", prefabPath, rockLOD0, rockLOD1, rockMat);
        }

        private void GenerateBush(string prefabPath, string meshPath, Material bushMat)
        {
            Mesh bushLOD0 = GenerateLumpyIcosphere(1.0f, 0.35f, new Vector3(0, 0.5f, 0));
            bushLOD0 = SaveMesh(bushLOD0, "Bush_LOD0", meshPath);

            Mesh bushLOD1 = GetScaledPrimitive(PrimitiveType.Cube, new Vector3(1.5f, 0.8f, 1.5f), new Vector3(0, 0.4f, 0));
            bushLOD1 = SaveMesh(bushLOD1, "Bush_LOD1", meshPath);

            CreateLODPrefabSingle("DemoBush", prefabPath, bushLOD0, bushLOD1, bushMat);
        }

        private void CreateLODPrefab(string name, string folder, Mesh lod0Mesh, Mesh lod1Mesh, Material[] materials)
        {
            string assetPath = $"{folder}/{name}.prefab";
            GameObject root = new GameObject(name);

            GameObject lod0Obj = new GameObject($"{name}_LOD0");
            lod0Obj.transform.SetParent(root.transform);
            lod0Obj.AddComponent<MeshFilter>().sharedMesh = lod0Mesh;
            lod0Obj.AddComponent<MeshRenderer>().sharedMaterials = materials;

            GameObject lod1Obj = new GameObject($"{name}_LOD1");
            lod1Obj.transform.SetParent(root.transform);
            lod1Obj.AddComponent<MeshFilter>().sharedMesh = lod1Mesh;
            lod1Obj.AddComponent<MeshRenderer>().sharedMaterials = materials;

            LODGroup lodGroup = root.AddComponent<LODGroup>();
            LOD[] lods = new LOD[2];
            lods[0] = new LOD(0.3f, new Renderer[] { lod0Obj.GetComponent<MeshRenderer>() });
            lods[1] = new LOD(0.05f, new Renderer[] { lod1Obj.GetComponent<MeshRenderer>() });
            lodGroup.SetLODs(lods);
            lodGroup.RecalculateBounds();

            PrefabUtility.SaveAsPrefabAsset(root, assetPath);
            DestroyImmediate(root);
        }

        private void CreateLODPrefabSingle(string name, string folder, Mesh lod0Mesh, Mesh lod1Mesh, Material mat)
        {
            CreateLODPrefab(name, folder, lod0Mesh, lod1Mesh, new[] { mat });
        }

        private Mesh CombineToNewMesh(string name, Mesh m1, Mesh m2, string saveFolder)
        {
            CombineInstance[] combine = new CombineInstance[2];
            combine[0].mesh = m1; combine[0].transform = Matrix4x4.identity;
            combine[1].mesh = m2; combine[1].transform = Matrix4x4.identity;

            Mesh fusedMesh = new Mesh { name = name };

            // Critical for multi-material asset synthesis. Ensures materials 
            // array mapping remains isolated to distinct submeshes.
            fusedMesh.CombineMeshes(combine, false, false);
            fusedMesh.RecalculateBounds();

            return SaveMesh(fusedMesh, name, saveFolder);
        }

        private Mesh SaveMesh(Mesh rawMesh, string name, string folder)
        {
            string assetPath = $"{folder}/{name}.asset";
            Mesh existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);

            if (existingMesh != null)
            {
                EditorUtility.CopySerialized(rawMesh, existingMesh);
                DestroyImmediate(rawMesh);
                return existingMesh;
            }
            else
            {
                AssetDatabase.CreateAsset(rawMesh, assetPath);
                return rawMesh;
            }
        }

        #endregion

        #region Geometry Generators

        private void EnsureValidMeshData(Mesh mesh)
        {
            int vCount = mesh.vertexCount;
            Vector2[] uvs = new Vector2[vCount];
            Vector4[] tangs = new Vector4[vCount];
            Vector4 safeTangent = new Vector4(1f, 0f, 0f, 1f);

            mesh.RecalculateBounds();
            Vector3 min = mesh.bounds.min;
            Vector3 size = mesh.bounds.size;

            for (int i = 0; i < vCount; i++)
            {
                Vector3 v = mesh.vertices[i];
                float ux = size.x > 0.001f ? (v.x - min.x) / size.x : 0.5f;
                float uy = size.y > 0.001f ? (v.y - min.y) / size.y : 0.5f;
                uvs[i] = new Vector2(ux, uy);
                tangs[i] = safeTangent;
            }

            mesh.uv = uvs;
            mesh.tangents = tangs;
        }

        private void ApplySphericalNormals(Mesh mesh)
        {
            mesh.RecalculateBounds();
            Vector3 center = mesh.bounds.center;
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = new Vector3[vertices.Length];

            for (int i = 0; i < vertices.Length; i++)
                normals[i] = (vertices[i] - center).normalized;

            mesh.normals = normals;
        }

        private Mesh GetPrimitiveMesh(PrimitiveType type)
        {
            GameObject temp = GameObject.CreatePrimitive(type);
            Mesh newMesh = Instantiate(temp.GetComponent<MeshFilter>().sharedMesh);
            DestroyImmediate(temp);
            return newMesh;
        }

        private Mesh GetScaledPrimitive(PrimitiveType type, Vector3 scale, Vector3 offset)
        {
            Mesh mesh = GetPrimitiveMesh(type);
            Vector3[] verts = mesh.vertices;
            for (int i = 0; i < verts.Length; i++)
                verts[i] = new Vector3(verts[i].x * scale.x, verts[i].y * scale.y, verts[i].z * scale.z) + offset;
            mesh.vertices = verts;
            mesh.RecalculateBounds();
            ApplySphericalNormals(mesh);
            EnsureValidMeshData(mesh);
            return mesh;
        }

        private Mesh GenerateProceduralCylinder(float radius, float height, int radialSegments, Vector3 positionOffset)
        {
            Mesh mesh = new Mesh { name = "Proc_Cylinder" };
            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();

            float angleStep = (Mathf.PI * 2.0f) / radialSegments;

            for (int i = 0; i < radialSegments; i++)
            {
                float a1 = i * angleStep;
                float a2 = ((i + 1) % radialSegments) * angleStep;

                Vector3 bl = new Vector3(Mathf.Cos(a1) * radius, 0, Mathf.Sin(a1) * radius) + positionOffset;
                Vector3 br = new Vector3(Mathf.Cos(a2) * radius, 0, Mathf.Sin(a2) * radius) + positionOffset;
                Vector3 tl = new Vector3(Mathf.Cos(a1) * radius, height, Mathf.Sin(a1) * radius) + positionOffset;
                Vector3 tr = new Vector3(Mathf.Cos(a2) * radius, height, Mathf.Sin(a2) * radius) + positionOffset;

                int idx = verts.Count;
                verts.AddRange(new[] { bl, tl, tr, br });
                tris.AddRange(new[] { idx, idx + 1, idx + 2, idx, idx + 2, idx + 3 });
            }

            mesh.vertices = verts.ToArray();
            mesh.triangles = tris.ToArray();
            ApplySphericalNormals(mesh);
            EnsureValidMeshData(mesh);
            return mesh;
        }

        private Mesh GenerateStackedCones(int tiers, float bottomRadius, float topRadius, float totalHeight, int radialSegments, Vector3 positionOffset)
        {
            Mesh mesh = new Mesh { name = "Proc_StackedCones" };
            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();

            float angleStep = (Mathf.PI * 2.0f) / radialSegments;
            float tierHeight = totalHeight / tiers;

            for (int t = 0; t < tiers; t++)
            {
                float yStart = positionOffset.y + (tierHeight * t * 0.75f);
                float yEnd = yStart + tierHeight * 1.25f;

                float rBottom = Mathf.Lerp(bottomRadius, topRadius, (float)t / tiers);
                float rTop = Mathf.Lerp(bottomRadius, topRadius, (float)(t + 1) / tiers) * 0.3f;

                for (int i = 0; i < radialSegments; i++)
                {
                    float a1 = i * angleStep;
                    float a2 = ((i + 1) % radialSegments) * angleStep;

                    Vector3 bl = new Vector3(Mathf.Cos(a1) * rBottom, yStart, Mathf.Sin(a1) * rBottom);
                    Vector3 br = new Vector3(Mathf.Cos(a2) * rBottom, yStart, Mathf.Sin(a2) * rBottom);
                    Vector3 tl = new Vector3(Mathf.Cos(a1) * rTop, yEnd, Mathf.Sin(a1) * rTop);
                    Vector3 tr = new Vector3(Mathf.Cos(a2) * rTop, yEnd, Mathf.Sin(a2) * rTop);
                    Vector3 centerBot = new Vector3(0, yStart, 0);

                    int idx = verts.Count;
                    verts.AddRange(new[] { bl, tl, tr, br, centerBot });
                    tris.AddRange(new[] { idx, idx + 1, idx + 2, idx, idx + 2, idx + 3, idx + 4, idx, idx + 3 });
                }
            }

            mesh.vertices = verts.ToArray();
            mesh.triangles = tris.ToArray();
            ApplySphericalNormals(mesh);
            EnsureValidMeshData(mesh);
            return mesh;
        }

        private Mesh GenerateLumpyIcosphere(float radius, float lumpiness, Vector3 positionOffset)
        {
            Mesh mesh = new Mesh { name = "Proc_LumpyIcosphere" };
            float t = (1.0f + Mathf.Sqrt(5.0f)) / 2.0f;
            Vector3[] baseVerts = new Vector3[]
            {
                new Vector3(-1, t, 0).normalized, new Vector3(1, t, 0).normalized,
                new Vector3(-1, -t, 0).normalized, new Vector3(1, -t, 0).normalized,
                new Vector3(0, -1, t).normalized, new Vector3(0, 1, t).normalized,
                new Vector3(0, -1, -t).normalized, new Vector3(0, 1, -t).normalized,
                new Vector3(t, 0, -1).normalized, new Vector3(t, 0, 1).normalized,
                new Vector3(-t, 0, -1).normalized, new Vector3(-t, 0, 1).normalized
            };

            int[] baseTris = new int[]
            {
                0, 11, 5,   0, 5, 1,   0, 1, 7,   0, 7, 10,   0, 10, 11,
                1, 5, 9,    5, 11, 4,  11, 10, 2, 10, 7, 6,   7, 1, 8,
                3, 9, 4,    3, 4, 2,   3, 2, 6,   3, 6, 8,    3, 8, 9,
                4, 9, 5,    2, 4, 11,  6, 2, 10,  8, 6, 7,    9, 8, 1
            };

            List<Vector3> finalVerts = new List<Vector3>();
            List<int> finalTris = new List<int>();

            Vector3 GetLumpyPos(Vector3 dir)
            {
                float noise = Mathf.PerlinNoise(dir.x * 2.5f + 10f, dir.y * 2.5f + dir.z * 2.5f);
                float r = radius * (1.0f + (noise - 0.5f) * lumpiness);
                return (dir * r) + positionOffset;
            }

            for (int i = 0; i < baseTris.Length; i += 3)
            {
                Vector3 v0 = baseVerts[baseTris[i]];
                Vector3 v1 = baseVerts[baseTris[i + 1]];
                Vector3 v2 = baseVerts[baseTris[i + 2]];

                Vector3 m01 = ((v0 + v1) / 2f).normalized;
                Vector3 m12 = ((v1 + v2) / 2f).normalized;
                Vector3 m20 = ((v2 + v0) / 2f).normalized;

                int idx = finalVerts.Count;
                finalVerts.AddRange(new[] {
                    GetLumpyPos(v0), GetLumpyPos(v1), GetLumpyPos(v2),
                    GetLumpyPos(m01), GetLumpyPos(m12), GetLumpyPos(m20)
                });

                finalTris.AddRange(new[] { idx, idx + 3, idx + 5, idx + 3, idx + 1, idx + 4, idx + 5, idx + 4, idx + 2, idx + 3, idx + 4, idx + 5 });
            }

            mesh.vertices = finalVerts.ToArray();
            mesh.triangles = finalTris.ToArray();
            ApplySphericalNormals(mesh);
            EnsureValidMeshData(mesh);
            return mesh;
        }

        #endregion
    }
}
#endif