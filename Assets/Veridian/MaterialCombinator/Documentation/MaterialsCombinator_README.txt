
README: Materials Combinator

Thank you for downloading the Materials Combinator. This utility is designed to procedurally pack your fragmented textures into unified atlases and permanently remap your mesh UVs to aggressively reduce material-based draw calls directly within the Unity Editor.

Below is a quick guide to help you access the tool and understand its initial setup.



Getting Started
The Materials Combinator operates primarily through a centralized Editor window. You can access the tool and its core features using the following methods:

Menu Path: Navigate to the top menu bar and select Tools > Veridian > Materials Combinator.

Core Workflows: Once the window is open, you will define your input prefabs, analyze their shaders, and select your generation mode. The tool supports two primary workflows depending on your project's rendering complexity:

Auto-Generate: The standard, automated workflow. The engine will pack your selected texture maps, remap your mesh UVs, create a new master material, wire the new atlases into the correct property slots, and apply it directly to your newly optimized prefabs.

Output Raw Atlases Only (The Developer Fallback): Designed for developers using highly complex, custom shader graphs. In this mode, the engine still does the heavy lifting—packing the textures and remapping the geometric UVs—but it leaves the material slots on your generated prefabs completely empty. It outputs the raw texture atlases to your folder, allowing you to manually build your master material and route the textures exactly how your custom shader requires.



Project Cleanup & Demo Deletion
Because texture packing and UV rewriting are destructive operations, testing these workflows directly on your production-ready prefabs carries some risk. To provide a safe onboarding experience, this package includes a Demo Control Panel and an Asset Factory that procedurally generates basic test models (trees, rocks, etc.) equipped with multi-material LODs.

Access the Demo Panel: Tools > Veridian > Materials Combinator > Demo Control Panel.

Safe Deletion: The demo scripts are provided purely for evaluation and operate within their own isolated Assembly Definition. Once you understand how the core tool functions, it is highly recommended that you safely delete the entire Demo folder from your project to keep your directories clean. Removing the demo factory and its associated scripts will not cause compiler errors or break the core Materials Combinator asset.

If you prefer to leave the demo files in your project, they are marked strictly as Editor-only and will automatically be stripped from your final compiled game builds.



Technical Footprint
Before integrating this utility into your development pipeline, it is helpful to understand how the codebase is structured under the hood.

Assembly Architecture: To keep compilation times fast and prevent Editor scripts from leaking into your final game build, the core Materials Combinator codebase is cleanly partitioned into two distinct Assembly Definitions: an Editor assembly and a Runtime assembly.

One-Way Dependency: The Editor UI and AssetDatabase serialization logic rely on the underlying Runtime processing engine in a strict, one-way dependence.

Omitting from Builds: Although the core packing engine is housed in a "Runtime" assembly, it is fundamentally a world-building and optimization tool. Unless your game actually performs procedural generation and texture-atlasing while the player is actively playing, this assembly is not helpful in a compiled game. Unity's compiler will automatically strip it from your final build as long as no active game scripts reference it.

If your project does not use Assembly Definitions at all, you can safely delete the .asmdef files located inside the Veridian folders to compile the scripts directly into your global assembly.



Full Documentation
A comprehensive user manual is available detailing advanced packing parameters, bounds management, topological submesh fusion, and the exposed C# runtime API.

You can access the full online documentation here:
https://docs.google.com/document/d/16S4gEWaVQwAj22GHlO6pX1pR8WLrflbY689BurVzBdk/edit?tab=t.0



Support and Contact
If you have questions regarding the setup, encounter unexpected edge cases, or wish to submit a bug report, please feel free to reach out via email:

trevor.keiber@gmail.com

Please keep in mind that because this is a free utility, I cannot provide guaranteed one-on-one troubleshooting, custom shader integration, or dedicated technical support. However, I am active in maintaining the stability of the tool and will review bug reports to address core issues in future updates.



Support the Developer & Ecosystem
As a solo developer, creating and maintaining free utilities requires a significant time investment. If the Materials Combinator helps accelerate your workflow or resolves draw-call bottlenecks in your project, please consider leaving a rating or review on the Unity Asset Store. It is the single most effective way to support the tool and help it gain visibility.

Additionally, managing dense environments requires optimizing more than just your materials. You can view my other specialized performance utilities on my publisher page:

Mesh Constructor: The ultimate companion to this tool. Once the Materials Combinator fixes your material fragmentation and atlas maps, feed those prefabs directly into the Mesh Constructor to physically weld their vertices together into a single static mesh, entirely eliminating the remaining CPU overhead.

Terrain Slicer (FREE): Instantly divide massive, monolithic landscapes into streamable, optimized chunks using the Burst Compiler to manage your open-world memory footprint.

Terrain Merger (FREE): The exact inverse of the Slicer. Seamlessly stitch split terrain chunks back together so you can paint global biomes across seams before re-slicing.

BurstLOD: Rapidly generate highly optimized runtime or Editor-based LODs, ensuring the custom prefabs you scatter and place stay performant at any distance.

You can view the full catalog of utilities here:
https://assetstore.unity.com/publishers/120204

Good luck with your project.