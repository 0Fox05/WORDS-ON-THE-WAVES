# **Materials Combinator**

## **1\. Overview & Requirements**

The Materials Combinator allows you to take visually complex scenes—such as modular buildings, scattered debris, or kitbashed props—and procedurally pack their fragmented textures into a single, highly optimized atlas. By algorithmically combining multiple unique materials into a unified texture sheet and remapping the underlying mesh UVs to match, this tool is designed to aggressively crush draw calls and reclaim your CPU overhead.

While Unity's default batching and GPU instancing are powerful, relying on scenes polluted with dozens of tiny, unique materials will eventually crash into a performance ceiling. The Combinator bypasses this by physically packing those textures together and rewriting the geometry data so the GPU only sees one material.

### **Origin & Ideal Use Cases**

I built this tool because relying heavily on modular asset packs and kitbashing often results in scenes bloated with fragmented materials. While great for visual variety, treating every minor prop as a distinct draw call will eventually choke your render threads. I needed a way to dynamically scan those fragmented materials, pack their maps together, and rewrite the mesh data directly in the Editor, without having to bounce back and forth to external 3D software like Blender or Maya.

**Ideal Use Cases:**

* Kitbashed architecture and modular building assets.  
* Heavy ground cover and grouped static environmental props.  
* Optimizing multi-material foliage and static background elements.  
* Preparing assets for massive structural fusion.

### **Realistic Expectations (When Not to Use It)**

Before getting started, it is important to clearly define what this tool does. The Materials Combinator is fundamentally an automated texture packing and UV-remapping engine.

* **Skinned Meshes:** It is not suitable for combining multiple entirely different Skinned Mesh Renderers. Attempting to merge distinct animated rigs will irreparably corrupt their bone weight indices. You may use this tool to optimize the materials of a *single* Skinned Mesh prefab, but combining multiple different characters is not supported.  
* **The Optimization Trade-off:** Understand the "Draw Call vs. VRAM" trade-off. Atlasing drastically reduces CPU-bound draw calls by batching materials together. However, depending on the maximum atlas size and pixel padding required, it can increase overall VRAM usage on the GPU. Use it judiciously where draw calls are your primary bottleneck.  
* **Tiling Textures:** Atlasing fundamentally breaks textures that rely on UVs tiling outside of the 0-to-1 range. If a brick wall material relies on a repeating 5x5 tiling scale, atlasing it will constrain it to its packed quadrant, breaking the seamless repetition.

### **Asset Architecture & Footprint**

Packing massive textures, rotating pixels, and recalculating thousands of UV coordinates is computationally expensive. To prevent your Editor from locking up during a heavy generation cycle, the Combinator bypasses standard C\# bottlenecks.

It heavily leverages Unity's Burst Compiler and the C\# Job System to crunch pixel data and vector math concurrently across multiple CPU threads. The asset is designed to be lightweight and will not drain your project's resources. It is strictly divided into distinct assemblies so that Editor UI logic never bloats your runtime build.

### **System Requirements & Day One Setup**

This tool is optimized specifically for modern architecture and requires **Unity 6** or newer. To ensure the engine runs flawlessly on day one, check the following:

* **The Burst Compiler:** For maximum performance, the core engine relies on the Native pipeline. In modern Unity versions, the required packages (`Unity.Burst`, `Unity.Collections`, `Unity.Mathematics`) are included by default. Unless you have intentionally uninstalled them, no setup is required.  
* **Render Pipeline Agnostic:** The core Combinator engine is completely render-pipeline agnostic. It will mathematically pack textures and rewrite mesh UVs perfectly regardless of whether your project uses the **Built-In Render Pipeline, URP, or HDRP**.  
* **Color Space:** If you intend to pack Normal maps, ensure your Unity Project Settings are set to use **Linear Color Space**.

### **Quick Start Guide**

If you want to immediately see how the asset works without diving into the rest of the manual, follow these steps to generate your first atlased prefab:

1. **Open the Tool:** Navigate to the top menu bar and click **Tools \> Veridian \> Materials Combinator**.  
2. **Add Prefabs:** Drag and drop a few GameObjects from your Project view directly into the Input Prefabs list.  
3. **Analyze & Select:** Click the **Analyze Inputs** button. The tool will dynamically scan the shaders of your inputted prefabs. From the generated list, select the specific texture channels (like Base Color or Normal Map) you want to atlas.  
4. **Execute:** Configure your output settings (like Atlas Padding or Max Atlas Size) and click the green **Generate Optimized Prefabs** button. The Burst Compiler will asynchronously pack the textures, remap the UVs, and save the newly combined asset to your designated output folder.

## **2\. The Demo Environment & Sandbox**

Before you start merging thousands of your own highly detailed game assets, it is highly recommended to learn the Combinator's workflows in a controlled environment.

To facilitate this, the asset includes a built-in Demo Control Panel. This utility procedurally constructs 3D test assets equipped with LODs and dynamically sized, generated textures. This provides the perfect sandbox to safely evaluate the texture packer and UV-remapping logic without risking accidental modifications to your actual project data.

### **Accessing the Demo Panel**

To open the utility, navigate to the top menu bar and select **Tools \> Veridian \> Materials Combinator \> Demo Control Panel**.

### **The Asset Factory**

The top section of the Control Panel is the Asset Factory. Rather than forcing you to dig through your project for safe prefabs to test—or bloating the asset download size with heavy textures—this tool generates testing assets from scratch via C\# code using procedural noise.

By clicking **Generate All Missing**, the factory will create four highly optimized, multi-LOD prefabs: a Pine Tree, an Oak Tree, a Rock, and a Bush.

* **Custom Output Path:** By default, these assets are saved to `Assets/VeridianData/MaterialsCombinator/Demo_Assets`. You can change this directory path at the top of the window if you prefer to keep your test files organized differently.  
* **Purge Demo Assets:** Once you are finished testing the Combinator and want to clean up your project, clicking this button instantly deletes the generated demo directory and all associated materials, textures, and meshes.

**A Quick Note on HDRP:** While the main Materials Combinator tool works flawlessly across all render pipelines, the procedural testing shaders built by this specific Demo Factory are currently only configured for URP and the Built-in pipeline. If you run the demo factory in an HDRP project, the test assets will spawn with pink/broken materials. This is purely a limitation of the free demo sandbox, not a flaw with the core atlasing engine. 

### **Combinator Integration**

If you want to quickly test how the algorithmic packer handles different texture channels and sizing modes, you need to feed the main tool a pool of prefabs.

Clicking **Push Assets to Combinator** automates this setup entirely:

1. It locates the procedural assets generated by the Asset Factory.  
2. It opens the main Materials Combinator window.  
3. It automatically injects the demo objects into the Input Prefabs list, ready for analysis.

Once pushed, you can immediately click "Analyze Inputs" on the main window, select the generated Base Color maps, and click "Generate" to see exactly how the engine merges the topology and creates the texture atlas in real-time.

## **3\. The Editor Window & Core Workflows**

The Editor window is the central control hub for the Materials Combinator. It provides a structured, top-to-bottom pipeline designed to guide you through configuring your inputs, analyzing their material properties, and executing the final generation.

To open the main tool, navigate to the top menu bar and select **Tools \> Veridian \> Materials Combinator**.

### **Input Prefabs**

This is where you define the specific assets you want to combine.

* **Drag & Drop Zone:** The fastest way to populate your list. Simply drag prefab assets from your Project view directly into this box. Note that you must use saved prefab assets from your project folders, not loose GameObjects currently sitting in your active scene.  
* **Skinned Mesh Safeguard:** As mentioned in the limitations, combining distinct animated rigs breaks their bone weight indices. If the tool detects that you have dropped in multiple *different* prefabs containing Skinned Mesh Renderers, a red "Unsupported Setup" warning will appear in the UI to prevent you from accidentally destroying your character rigs.

### **Combinator Settings (Overview)**

Once your inputs are assigned, this section acts as your primary configuration hub. Here you define exactly where the new assets will be saved (the **Save Directory**) and what tag will be appended to their names (the **Asset Suffix**).

You can also toggle **Process LOD Groups** here. If enabled, the engine will extract and process every Level of Detail mesh within your prefabs, packing their materials into the shared atlas and ensuring the entire LOD chain remains fully intact and optimized.

*(A complete, detailed breakdown of the packing rules, bounds, and submesh combinations is available in Section 4).*

### **Texture Analysis & Selection**

This is the most critical step of the workflow. Because different shaders use different naming conventions for their texture maps (e.g., `_BaseMap` vs. `_MainTex`), the Combinator doesn't guess what to pack. It asks you.

Clicking the **Analyze Inputs** button commands the engine to dynamically scan every shader attached to your inputted prefabs.

* **Dynamic UI Generation:** The UI will populate with a list of every active texture property found across your assets, grouped cleanly by Shader.  
* **Property Selection:** Simply check the box next to the channels you want to pack (such as `_BaseColor` or `_BumpMap`). If you leave a property unchecked, the packer will ignore it.  
* **Map Type Hinting:** Next to each property is a Map Type dropdown (Auto, Base Color, Normal Map). The engine tries to guess the correct color space handling based on the property name. Normal maps require strict linear processing to prevent the tangent space math from breaking. Leaving this on "Auto" usually works perfectly, but you can force it here if you are packing custom, non-standard mask maps.

### **Execution & Reversibility**

Fusing geometry and rewriting texture coordinates are destructive operations. To prevent accidental data loss, the tool uses an isolated output system.

* **Generate Optimized Prefabs:** Once your inputs are analyzed and your bounds are set, this button becomes active. Clicking it hands your configuration over to the Burst Compiler, initiating the asynchronous texture packing and UV-remapping process.  
* **Undo / Delete Last Creation:** Mistakes happen. If you generate an atlas, realize the padding was too low, and want to try again, you do not have to manually hunt down the generated files in your project directory. Immediately after a successful generation, a red undo button will appear at the bottom of the window. Clicking it instantly deletes the last generated folder and all its contents, keeping your project clean.

## **4\. Atlasing Strategies & Settings Deep-Dive**

While the default settings will work for the majority of standard environmental props, the Combinator provides deep control over how your textures are mathematically packed and how the final mesh topology is structured. Understanding these parameters ensures you are getting the most optimal balance between draw calls, VRAM usage, and visual fidelity.

### **Mesh Topology Optimization**

By default, the Combinator simply remaps the UVs of your inputted meshes so they can share a single material, leaving the underlying geometric structure untouched.

* **Combine Submeshes (Optional):** Enabling this toggle commands the engine to aggressively merge any submeshes that end up sharing the new generated material atlas.  
* **The Warning:** This is a highly destructive geometric operation. It permanently removes individual material slots and fuses the topology. While this is fantastic for squeezing the absolute maximum performance out of static background scenery or scattered debris, you should leave this disabled for interactable objects, hero assets, or complex multi-part props where you might need to swap individual materials at runtime.

### **Packing Bounds & Sizing Modes**

This section determines the absolute maximum memory footprint your newly generated texture sheets are allowed to consume. The packer relies on the original input texture sizes and will hard-fail if an individual texture exceeds the maximum allowed bounds.

* **Sizing Mode:**  
  * **Auto Power of Two (POT):** The standard, game-ready approach. The engine automatically evaluates the total area of your inputted textures and calculates the smallest possible standard resolution (e.g., 1024x1024, 2048x1024) to fit them all.  
  * **Custom Dimensions:** Forces the packer to attempt fitting everything into an exact width and height. Note that custom dimensions must be positive multiples of 8 to maintain texture compression compatibility in Unity.  
* **Min / Max Atlas Size:** When using Auto POT, these sliders constrain the algorithm. If you set the max size to 2048, the packer will refuse to generate a 4096 texture, even if it means failing the pack.

### **Packing Rules**

These parameters dictate the specific rules the packing algorithm must obey when finding a home for each texture inside the defined bounds.

* **Atlas Padding (Pixels):** Inserts blank pixel padding around every packed texture in the atlas. This is crucial for preventing "texture bleeding" at lower mip-map levels when viewed from a distance. However, be aware of the math: adding 8 pixels of padding to 50 tiny textures massively increases their total area footprint, which might force the overall atlas to jump to the next Power of Two size tier.  
* **Allow Rotation:** When enabled, the packer is allowed to rotate textures 90 degrees to find a tighter, more efficient fit. *Turn this off* if your assets rely on strict directionality (e.g., a wood grain texture that must always run vertically).  
* **Packing Heuristic:** Allows you to tweak the logic the algorithm uses to determine the "best fit" for a rectangle. `Best Short Side Fit` is the default and usually yields the tightest packs, but you can experiment with `Best Area Fit` if you have highly irregular texture sizes.

### **Pre-Pack Bounds & Post-Process Scaling**

Dealing with massive input textures or optimizing for lower-end platforms requires intelligent scaling. It is critical to understand the difference between the two scaling phases:

* **Auto-Downscale Large Inputs (Pre-Pack):** This operates *before* the packing algorithm runs. If you attempt to pack a 4K texture, but your `Max Atlas Size` limit is capped at 2048, the packer will fail. Enabling this feature catches that failure early, temporarily shrinking the offending 4K texture until its longest edge fits within your 2048 limit. This prevents hard failures without affecting the rest of your smaller input textures.  
* **Final Atlas Downscale Factor (Post-Process):** This operates *after* everything has been successfully packed into the master texture sheet. For example, if the engine successfully generates a beautiful 2048x2048 atlas, but you are targeting mobile hardware and need to slash VRAM, setting this slider to `0.5` will shrink the *entire finished atlas* down to 1024x1024.

### **Material Output Configuration & Shader Compatibility**

Depending on the complexity of your project's rendering pipeline, you may not want the tool building materials for you. Because this is a free utility, it is simply beyond the scope of this asset to guarantee that the auto-generator will perfectly parse and wire up every custom shader graph or third-party asset store shader in existence.

* **Auto-Generate:** The standard mode. The engine creates a new Material using your source shader, writes the newly packed atlases into the correct standard property slots (Base Color, Normal, etc.), averages out any lingering color/float values, and assigns it directly to your newly optimized prefabs. It works brilliantly for standard Lit/Unlit and simple custom setups.  
* **Output Raw Atlases Only (The Developer Fallback):** If `Auto-Generate` fails to configure your specific, highly-complex custom shader perfectly, don't panic. Simply switch to this mode. The engine will still do 99% of the heavy lifting—packing the textures and permanently remapping the complex mesh UVs—but it will leave the material slots on your generated prefabs completely empty. It simply outputs the raw texture atlases to your folder, allowing you to manually build your master material, slot the textures in, and assign it exactly how you want.

### **Missing Maps & Fallback Textures**

When merging disparate assets, you will inevitably encounter mismatched material properties. For example, Rock A might use an Albedo and a Normal map, while Rock B only uses an Albedo.

If you tell the engine to pack both Albedo and Normal maps, it will not fail or skip Rock B. Instead, the Combinator automatically generates a **Fallback Texture** (by default, a tiny 4x4 pixel block). For Normal maps, it generates a perfectly flat, neutral blue (`128, 128, 255`). For masks, it generates black or white depending on the map type. This ensures the atlas remains mathematically sound and your shaders don't break when applied to the combined mesh.

## **5\. API Reference & Core Architecture**

For developers building advanced procedural generation pipelines, automated asset optimizers, or custom Editor tools, you can completely bypass the Materials Combinator Editor window. The underlying packing and UV-remapping engine is fully decoupled from the UI, allowing you to trigger the heavy, multithreaded operations dynamically via your own C\# scripts.

### **Core Data Structures**

Before you can pack textures and rewrite UVs, you must define your parameters and wrap your spatial data so the engine can read it.

* **`CombinatorSettings`**: The primary configuration object. This dictates the architectural boundaries of the packer. Key properties include `AtlasPadding`, `SizingMode`, `MaxAtlasSize`, and destructive toggles like `CombineSubmeshes`.  
* **`SourceObject`**: The input data wrapper. The engine does not interact directly with GameObjects or Prefabs. Instead, you feed it `SourceObject` instances, which simply hold a reference to a raw `Mesh`, an array of `Material[]`, and an `Identifier` (which you can use to map the generated mesh back to your original object later).  
* **`CombinationResult`**: The output container. When the engine finishes, it returns this object containing a success boolean, any error messages, the newly built `GeneratedMeshes`, and the `AtlasResults` (which hold the raw texture data in memory).

### **The CombinatorCore Engine**

The multithreaded brain of the asset is accessed via the `Veridian.Perspective.Combinator.CombinatorCore` class.

To execute a headless operation, you instantiate the core with your `CombinatorSettings`, define a `Dictionary<Shader, HashSet<string>>` to tell the engine exactly which shader properties (e.g., `_BaseColor`, `_BumpMap`) it is allowed to extract and pack, and call `Process()`.

### Because the Combinator utilizes the Burst Compiler for pixel rotation and UV remapping, these operations execute incredibly fast in the background.  **Assembly Definitions (`.asmdef`) & Project Integration**

To keep compilation times fast and prevent editor scripts from leaking into your final game build, the Materials Combinator is strictly partitioned using Assembly Definitions. If you are writing custom C\# scripts to interact with the Combinator API, you must understand this architecture:

1. **Demo Assembly:** (`Veridian.Perspective.Combinator.Demo`) References both the runtime and editor assemblies. **It is highly recommended that you delete this entire demo folder** once you understand how the tool works. Deleting it has zero consequences on the core asset.  
2. **Runtime Assembly:** (`Veridian.Perspective.Combinator`) Contains the core packing engine, Burst jobs, and math structures.  
3. **Editor Assembly:** (`Veridian.Perspective.Combinator.Editor`) Contains the UI and AssetDatabase serialization logic. It has a strict one-way dependency on the Runtime assembly.

**Referencing the API:** If your project uses Assembly Definitions, you must explicitly add a reference to `Veridian.Perspective.Combinator` in your own `.asmdef` file to access the core API.

*Note for Build Sizes:* Although the core engine is housed in a "Runtime" assembly, it is fundamentally a world-building tool. Unless your game actually performs procedural generation and mesh-fusion while the player is playing, this assembly is not helpful in an actual compiled game. Unity's compiler will automatically strip it from your final build as long as no active game scripts reference it.

*Note for Non-Asmdef Users:* If your project does not use Assembly Definitions at all, you can safely delete the `.asmdef` files located inside the Veridian folders. Unity will then recompile the scripts into your global `Assembly-CSharp`.

### **Execution Example**

Here is a practical, lightweight example of how you might trigger a headless packing operation via a custom Editor script or a runtime automation pipeline.

C\#  
using System.Collections.Generic;  
using UnityEngine;  
using Veridian.Perspective.Combinator;

public class CombinatorAPIExample : MonoBehaviour  
{  
    public Mesh sourceMesh;  
    public Material\[\] sourceMaterials;

    public void ExecuteHeadlessCombinator()  
    {  
        // 1\. Define the architectural bounds and packing rules  
        CombinatorSettings settings \= new CombinatorSettings  
        {  
            AtlasPadding \= 4,  
            SizingMode \= AtlasSizingMode.AutomaticRectangularPOT,  
            MaxAtlasSize \= 4096,  
            AllowRotation \= true,  
            CombineSubmeshes \= false,  
            GenerationMode \= MaterialGenerationMode.OutputRawAtlasesOnly  
        };

        // 2\. Wrap your raw inputs into SourceObjects  
        List\<SourceObject\> inputs \= new List\<SourceObject\>();  
          
        // The third parameter is an identifier to help you track the output  
        inputs.Add(new SourceObject(sourceMaterials, sourceMesh, "MyCustomProp"));

        // 3\. Define exactly which texture channels to pack per-shader  
        var inclusionList \= new Dictionary\<Shader, HashSet\<string\>\>();  
          
        if (sourceMaterials.Length \> 0 && sourceMaterials\[0\] \!= null)  
        {  
            Shader targetShader \= sourceMaterials\[0\].shader;  
              
            // Tell the engine to only pack the Albedo and Normal maps  
            inclusionList\[targetShader\] \= new HashSet\<string\> { "\_BaseMap", "\_BumpMap" };  
        }

        // 4\. Initialize the engine  
        CombinatorCore core \= new CombinatorCore(settings);  
          
        // Optional: Hook into the progress delegate for UI telemetry  
        core.OnProgress \+= (status, progress) \=\>   
        {  
            Debug.Log($"\[Combinator\] {Mathf.RoundToInt(progress \* 100)}% \- {status}");  
        };

        // 5\. Execute the process (hands data to the Burst Compiler)  
        CombinationResult result \= core.Process(inputs, inclusionList);

        if (result.Success)  
        {  
            Debug.Log("Atlasing complete. Extracting meshes and textures...");  
              
            // 6\. Retrieve your newly UV-remapped meshes  
            foreach (var kvp in result.GeneratedMeshes)  
            {  
                object identifier \= kvp.Key;  
                Mesh optimizedMesh \= kvp.Value.Mesh;  
                  
                // You can now save the mesh to disk or use it at runtime  
                optimizedMesh.name \= identifier \+ "\_Optimized";  
            }

            // 7\. Retrieve the generated atlases from memory  
            foreach (var kvp in result.AtlasResults)  
            {  
                AtlasResult atlasData \= kvp.Value;  
                  
                foreach (var atlasTex in atlasData.GeneratedAtlases)  
                {  
                    string propertyName \= atlasTex.Key; // e.g., "\_BaseMap"  
                    Texture2D packedTexture \= atlasTex.Value;  
                      
                    // Save to disk via System.IO or assign to a new material  
                }  
            }  
        }  
        else  
        {  
            Debug.LogError($"Combinator failed: {result.Message}");  
        }  
    }  
}  
\`\`\`\</Shader,\>

### **Extended API Examples & Utilities**

While the `CombinatorCore.Process()` method handles the heavy lifting of full prefab atlasing, the Combinator's internal architecture is built on several highly optimized, standalone utilities. If you are building your own custom Editor tools or procedural generators, you can leverage these public methods directly to handle texture manipulation and asset serialization without invoking the entire atlasing pipeline.

Here are a few short, pragmatic snippets you can drop into your own scripts.

#### **1\. GPU-Accelerated Texture Resizing**

Standard Unity texture resizing requires reading and writing raw CPU arrays, which is painfully slow. The Combinator includes a `TextureProcessor` that bypasses the CPU entirely, using `RenderTexture` blitting to resize images instantly on the GPU.

C\#  
using Veridian.Perspective.Combinator;  
using UnityEngine;

public class TextureTools   
{  
    public Texture2D DownscaleTexture(Texture2D sourceTex)  
    {  
        // Resizes the texture to exactly 50% (0.5f).  
        // The boolean flags tell the engine if it's linear data (like masks)   
        // and if it needs to execute a Burst job to mathematically re-normalize normal map vectors.  
        Texture2D resizedTex \= TextureProcessor.Resize(  
            sourceTex,   
            scaleFactor: 0.5f,   
            isLinear: false,   
            normalizeVectors: false  
        );

        return resizedTex;  
    }  
}

#### **2\. Safe Pixel Extraction (Bypassing Read/Write Locks)**

Extracting pixels via Unity's standard `GetPixels()` often fails and throws errors if the texture's import settings aren't strictly flagged as "Read/Write Enabled." The `TextureUtils` class provides a safe, brute-force extraction method that uses temporary RenderTextures and `NativeArrays` to rip the data regardless of import settings.

C\#  
using Veridian.Perspective.Combinator;  
using UnityEngine;

public class PixelExtractor   
{  
    public Color\[\] ForceExtractPixels(Texture2D lockedTexture)  
    {  
        // Safely forces pixel extraction without throwing Unity exceptions,   
        // even if the texture is marked non-readable.  
        Color\[\] rawPixels \= TextureUtils.GetPixels(lockedTexture);  
          
        Debug.Log($"Extracted {rawPixels.Length} pixels successfully.");  
        return rawPixels;  
    }  
}

#### **3\. Editor Asset Serialization**

If your custom script is generating raw meshes, materials, or textures in active memory, you eventually need to save them to your hard drive. The `AssetUtils` wrapper handles all the messy Unity AssetDatabase logic (like generating unique paths, preventing GUID overwrites, and configuring default texture compression).

C\#  
\#if UNITY\_EDITOR  
using Veridian.Perspective.Combinator.Editor;  
using UnityEngine;

public class AssetSaver   
{  
    public void SaveGeneratedData(Texture2D atlasTex, Mesh combinedMesh, string targetPath)  
    {  
        // 1\. Automatically serializes the texture as a PNG, configures mipmaps,   
        // and safely resolves naming conflicts.  
        AssetUtils.SaveTexture(atlasTex, targetPath, "MyCustomAtlas", allowHDRtoLDR: false);

        // 2\. Optimizes the mesh for the GPU and saves it as a .mesh asset.  
        AssetUtils.SaveMesh(combinedMesh, targetPath, "MyFusedMesh");  
    }  
}  
\#endif

#### **4\. Procedural Noise Generation**

If you need to quickly generate placeholder textures for testing (similar to how the built-in Demo Control Panel operates), you can use the synthesizer.

C\#  
\#if UNITY\_EDITOR  
using Veridian.Perspective.Combinator.Demo.Editor;  
using UnityEngine;

public class TestAssetBuilder   
{  
    public Texture2D CreateBarkTexture()  
    {  
        // Generates a 256x256 seamless Perlin noise texture blending two colors  
        Texture2D generatedNoise \= ProceduralTextureSynthesizer.GenerateNoiseTexture(  
            width: 256,   
            height: 256,   
            colorA: new Color(0.4f, 0.3f, 0.2f),   
            colorB: new Color(0.2f, 0.15f, 0.1f),   
            scaleX: 3.0f,   
            scaleY: 10.0f  
        );

        return generatedNoise;  
    }  
}  
\#endif

## **6\. Technical Considerations & Performance Boundaries**

While the Combinator is designed to be as automated and user-friendly as possible, reading raw pixel data and rewriting mesh arrays requires navigating some of Unity's strict internal memory limits. To get the most out of this tool, it is important to understand what is happening under the hood.

### **Texture Import Settings Hijacking**

To mathematically pack textures, the engine requires direct, low-level memory access to the pixel arrays of your source images. By default, to save VRAM, Unity imports most textures with the **Read/Write** setting disabled and applies aggressive compression (like crunch compression), making the raw pixels completely inaccessible to C\# scripts.

**How the Combinator Handles This:** To prevent you from having to manually dig through your project and change the import settings of every single texture you want to pack, the tool runs a pre-processing pass.

1. When you hit Generate, it meticulously backs up the current import state of every required texture.  
2. It temporarily overrides the import settings, forces Read/Write to true, and disables compression.  
3. It extracts the raw pixels for the packer.  
4. Once the atlas is successfully generated, it automatically restores your source textures back to their original, optimized import state.

*Note: Because the tool is dynamically altering Unity Asset import settings, you may experience a brief Editor pause at the very beginning and end of a generation cycle while Unity recompiles the source textures.*

### **Normal Maps & Color Space**

Packing standard Albedo maps is simple, but packing Normal maps requires strict color space management. Normal maps encode 3D directional vectors into RGB channels. If these channels are accidentally converted to sRGB or improperly scaled during the packing process, your lighting will look inverted or broken.

The Combinator automatically detects Normal maps (via naming conventions and shader property types) and forces the packing render textures into a Linear color space. Furthermore, the tool leverages the Burst Compiler to run an asynchronous `NormalizeNormalMapJob` over the final packed atlas, ensuring that every vector is perfectly mathematically normalized before the asset is saved to your disk.

### **Material Property Averaging**

Draw calls are dictated by materials. If you have two identical rocks, but one uses a material tinted slightly red and the other uses a material tinted slightly blue, they cannot share a draw call.

If you force the Combinator to pack and combine these two rocks into a single atlas, the tool has to generate a single master material. When it encounters conflicting float values or color tints (e.g., Rock A has a Metallic slider of `0.2` and Rock B has `0.8`), **the engine will average them out** (the new master material will have a Metallic value of `0.5`).

Because of this, you should aim to combine props that share similar shader profiles and tinting. If you combine a glowing neon sign material with a matte brick wall material, the averaged properties on the final combined material will likely look wrong.

## **7\. Expand Your Toolkit & Support**

The Materials Combinator is a powerful optimization engine, but it is just one half of a broader ecosystem designed to help developers build massive environments efficiently.

If this asset has sped up your world-building process, there are two other tools on my publisher page that you will find essential for your pipeline:

### **The Veridian Ecosystem**

* **Mesh Constructor:** The Materials Combinator perfectly solves *material fragmentation* (reducing 50 unique textures down to 1), but you still have 50 separate GameObjects in your scene. To get the absolute maximum performance out of Unity, feed your newly atlased prefabs directly into the **Mesh Constructor**. It will physically weld the vertices together and bake the separate objects into a single, unified static mesh, completely eliminating the remaining CPU overhead.  
* **BurstLOD:** To get the best performance out of the engine, the prefabs you scatter need to be optimized. BurstLOD is a tool I developed to quickly generate incredibly fast, high-quality runtime or Editor-based LODs for your custom models, ensuring your source geometry is as performant as possible before you even begin the atlasing process.

### **Compatibility & Support**

This asset is built specifically for modern rendering architectures and requires **Unity 6** or newer. It heavily leverages the Burst Compiler and the C\# Job System, so please ensure those packages are present and up to date in your project.

Please note that because this is a completely free utility, I cannot provide the same level of dedicated one-on-one support or custom modifications for specific project edge cases that you might expect from a paid, premium tool. The system has been thoroughly tested, but if you do encounter a reproducible bug or a hard crash, I would appreciate hearing about your experience so I can investigate potential fixes for future updates.

### **Leave a Review**

Finally, if you like this asset and find that it saves you time and optimizes your environmental design process, please consider taking a moment to leave a rating and a review on the Asset Store page.

Positive reviews are the single best way to help the asset gain visibility in the algorithm, and they directly support my ability to continue creating, updating, and sharing these free tools with the community.

## **8\. Troubleshooting & FAQ**

No matter how heavily optimized the Burst jobs are, combining millions of pixels and vertices pushes against Unity's hard memory limits. If you run into an issue, check here first.

### **Why did my pack fail with an "ERROR\_CUSTOM\_SIZE\_TOO\_SMALL" or bounds warning?**

This means the cumulative area of your input textures physically cannot fit inside the maximum limits you set.

* **Solution 1:** Increase your `Max Atlas Size` (e.g., from 2048 to 4096).  
* **Solution 2:** Enable the **Auto-Downscale Large Inputs** toggle in the Combinator Settings. This tells the engine to automatically shrink massive individual textures *before* attempting to pack them, ensuring they always squeeze into your defined bounds.

### **Why did Unity freeze or crash when I clicked Generate?**

You are likely asking the engine to process too much geometry at once. While the 32-bit index buffer allows a mesh to hold billions of vertices in theory, your Editor's RAM cannot scale infinitely.

* **The Practical Limit:** If your projected vertex count exceeds 2,000,000 vertices, you run the risk of an Out-Of-Memory (OOM) crash, especially if Unity has to fall back to the synchronous pipeline to read incompatible UV channels.  
* **The Solution:** Do not try to atlas and combine your entire level in a single click. Instead, build localized "chunks" (e.g., combining a forest into four separate 500k-vertex quadrants). This is safer to generate, easier to manage, and vastly superior for Unity's occlusion culling at runtime.

### **My optimized prefab still has dozens of draw calls and sub-meshes. Why?**

Atlasing reduces draw calls by packing textures, but it cannot perform miracles if you feed it fundamentally incompatible shaders.

* If you feed 50 objects into the Combinator, and half of them use the standard `Lit` shader while the other half use a custom `Foliage_Sway` shader, the engine **cannot** merge them into a single material.  
* The resulting fused mesh will be split into multiple sub-meshes—one for each distinct shader type. To get the maximum draw-call reduction, ensure the props you are combining share the same base shader before you hit generate.

### **My normal maps look completely broken, flat, or inverted on the new asset.**

Normal maps encode 3D directional vectors into RGB channels. If these channels are accidentally converted to sRGB space, the tangent math breaks.

* **Solution:** Ensure your Unity Project Settings are set to use **Linear Color Space** (`Edit > Project Settings > Player > Other Settings > Color Space`).  
* The Combinator runs a specific Burst job to safely unpack and re-normalize your normal maps during generation, but if your project is forcing Gamma space, Unity's default rendering will fight the generated atlas.

### **My animated character stopped moving after I combined it.**

The Materials Combinator (and the Mesh Constructor) are designed to build highly optimized, **static** environmental architecture.

* If you feed a prefab containing a `SkinnedMeshRenderer` into the tool, the Native Pipeline will evaluate the object's active pose at the exact moment of generation and bake it into a hardened, rigid `MeshFilter`. All bone weights, armatures, and blendshapes are permanently stripped from the fused asset.  
* This is incredibly useful for freezing a rigged tree into a static background prop, but it will permanently break walking NPCs.

### **The pre-flight telemetry says "Unsupported Setup Detected".**

You have dragged multiple *different* prefabs containing `SkinnedMeshRenderers` into the drag-and-drop zone. Combining distinct animated rigs will irreparably corrupt their bone weight indices. Remove the conflicting skeletal meshes from the input list to proceed.

