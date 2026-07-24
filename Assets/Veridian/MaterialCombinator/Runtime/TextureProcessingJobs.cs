// Filename: TextureProcessingJobs.cs
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Veridian.Perspective.Combinator
{
    /// <summary>
    /// Renormalizes vectors in a normal map after operations like resizing.
    /// Operates on float4 data where xyz represents the vector and w is unused.
    /// </summary>
    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
    public struct NormalizeNormalMapJob : IJobParallelFor
    {
        public NativeArray<float4> Pixels;

        public void Execute(int index)
        {
            float4 p = Pixels[index];
            float3 normal = new(p.x * 2f - 1f, p.y * 2f - 1f, p.z * 2f - 1f);

            if (math.lengthsq(normal) > 1e-6f)
            {
                normal = math.normalize(normal);
            }
            else
            {
                normal = new float3(0, 0, 1); // Default flat normal for safety
            }

            p.x = normal.x * 0.5f + 0.5f;
            p.y = normal.y * 0.5f + 0.5f;
            p.z = normal.z * 0.5f + 0.5f;

            Pixels[index] = p;
        }
    }

    /// <summary>
    /// Converts pixel data from LDR (Color32) to linear HDR (float4) format.
    /// </summary>
    [BurstCompile]
    public struct Convert32ToFloat4Job : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Color32> Input;
        [WriteOnly] public NativeArray<float4> Output;
        [ReadOnly] public float Inv255; // Pre-calculate 1.0f / 255.0f

        public void Execute(int index)
        {
            Color32 c32 = Input[index];
            Output[index] = new float4(c32.r * Inv255, c32.g * Inv255, c32.b * Inv255, c32.a * Inv255);
        }
    }

    /// <summary>
    /// Converts pixel data from linear HDR (float4) to LDR (Color32) format with saturation.
    /// </summary>
    [BurstCompile]
    public struct ConvertFloat4To32Job : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float4> Input;
        [WriteOnly] public NativeArray<Color32> Output;

        public void Execute(int index)
        {
            float4 f4 = Input[index];
            Output[index] = new Color32(
                (byte)(math.saturate(f4.x) * 255.0f),
                (byte)(math.saturate(f4.y) * 255.0f),
                (byte)(math.saturate(f4.z) * 255.0f),
                (byte)(math.saturate(f4.w) * 255.0f)
            );
        }
    }
}