using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Veridian.Perspective.Combinator
{
    /// <summary>
        /// Defines the heuristic used for packing.
        /// </summary>
    public enum FreeRectChoiceHeuristic
    {
        BestShortSideFit, // BSSF: Positions the rectangle against the short side of a free rectangle into which it fits the best.
        BestAreaFit,      // BAF: Positions the rectangle into the smallest free rect into which it fits.
    }

    /// <summary>
    /// Implements the MaxRects algorithm for efficient 2D texture packing, supporting rotation.
    /// (Runtime Safe)
    /// Based on the description by Jukka Jylänki: http://clb.demon.fi/files/RectangleBinPack.pdf
    /// </summary>
    public class MaxRectsPacker
    {
        public int BinWidth { get; private set; }
        public int BinHeight { get; private set; }
        private readonly bool allowRotations;

        private List<Rect> freeRectangles = new();

        public MaxRectsPacker(int width, int height, bool rotations)
        {
            BinWidth = width;
            BinHeight = height;
            allowRotations = rotations;

            freeRectangles.Clear();
            freeRectangles.Add(new Rect(0, 0, width, height));
        }

        /// <summary>
        /// Inserts a single rectangle into the bin.
        /// </summary>
        public Rect Insert(int width, int height, FreeRectChoiceHeuristic method)
        {
            Rect newNode = new();
            int score1 = int.MaxValue;
            int score2 = int.MaxValue;

            switch (method)
            {
                case FreeRectChoiceHeuristic.BestShortSideFit:
                    newNode = FindPositionForNewNodeBestShortSideFit(width, height, ref score1, ref score2);
                    break;
                case FreeRectChoiceHeuristic.BestAreaFit:
                    newNode = FindPositionForNewNodeBestAreaFit(width, height, ref score1, ref score2);
                    break;
            }

            if (newNode.height == 0)
                return newNode;

            PlaceRect(newNode);
            return newNode;
        }

        private void PlaceRect(Rect node)
        {
            int numRectanglesToProcess = freeRectangles.Count;
            for (int i = 0; i < numRectanglesToProcess; ++i)
            {
                if (SplitFreeNode(freeRectangles[i], ref node))
                {
                    freeRectangles.RemoveAt(i);
                    --i;
                    --numRectanglesToProcess;
                }
            }

            PruneFreeList();
        }

        private Rect FindPositionForNewNodeBestShortSideFit(int width, int height, ref int bestShortSideFit, ref int bestLongSideFit)
        {
            Rect bestNode = new();
            bestShortSideFit = int.MaxValue;
            bestLongSideFit = int.MaxValue;

            for (int i = 0; i < freeRectangles.Count; ++i)
            {
                // Try to place the rectangle in the current free rectangle.
                if (freeRectangles[i].width >= width && freeRectangles[i].height >= height)
                {
                    int leftoverHoriz = Mathf.Abs((int)freeRectangles[i].width - width);
                    int leftoverVert = Mathf.Abs((int)freeRectangles[i].height - height);
                    int shortSideFit = Mathf.Min(leftoverHoriz, leftoverVert);
                    int longSideFit = Mathf.Max(leftoverHoriz, leftoverVert);

                    if (shortSideFit < bestShortSideFit || (shortSideFit == bestShortSideFit && longSideFit < bestLongSideFit))
                    {
                        bestNode = new Rect(freeRectangles[i].x, freeRectangles[i].y, width, height);
                        bestShortSideFit = shortSideFit;
                        bestLongSideFit = longSideFit;
                    }
                }

                // Try rotation
                if (allowRotations && freeRectangles[i].width >= height && freeRectangles[i].height >= width)
                {
                    int leftoverHoriz = Mathf.Abs((int)freeRectangles[i].width - height);
                    int leftoverVert = Mathf.Abs((int)freeRectangles[i].height - width);
                    int shortSideFit = Mathf.Min(leftoverHoriz, leftoverVert);
                    int longSideFit = Mathf.Max(leftoverHoriz, leftoverVert);

                    if (shortSideFit < bestShortSideFit || (shortSideFit == bestShortSideFit && longSideFit < bestLongSideFit))
                    {
                        // We store the rotated dimensions in the Rect.
                        bestNode = new Rect(freeRectangles[i].x, freeRectangles[i].y, height, width);
                        bestShortSideFit = shortSideFit;
                        bestLongSideFit = longSideFit;
                    }
                }
            }
            return bestNode;
        }

        private Rect FindPositionForNewNodeBestAreaFit(int width, int height, ref int bestAreaFit, ref int bestShortSideFit)
        {
            Rect bestNode = new();
            bestAreaFit = int.MaxValue;
            bestShortSideFit = int.MaxValue;

            for (int i = 0; i < freeRectangles.Count; ++i)
            {
                int areaFit = (int)freeRectangles[i].width * (int)freeRectangles[i].height - width * height;

                // Try without rotation
                if (freeRectangles[i].width >= width && freeRectangles[i].height >= height)
                {
                    int leftoverHoriz = Mathf.Abs((int)freeRectangles[i].width - width);
                    int leftoverVert = Mathf.Abs((int)freeRectangles[i].height - height);
                    int shortSideFit = Mathf.Min(leftoverHoriz, leftoverVert);

                    if (areaFit < bestAreaFit || (areaFit == bestAreaFit && shortSideFit < bestShortSideFit))
                    {
                        bestNode = new Rect(freeRectangles[i].x, freeRectangles[i].y, width, height);
                        bestShortSideFit = shortSideFit;
                        bestAreaFit = areaFit;
                    }
                }

                // Try with rotation
                if (allowRotations && freeRectangles[i].width >= height && freeRectangles[i].height >= width)
                {
                    int leftoverHoriz = Mathf.Abs((int)freeRectangles[i].width - height);
                    int leftoverVert = Mathf.Abs((int)freeRectangles[i].height - width);
                    int shortSideFit = Mathf.Min(leftoverHoriz, leftoverVert);

                    if (areaFit < bestAreaFit || (areaFit == bestAreaFit && shortSideFit < bestShortSideFit))
                    {
                        bestNode = new Rect(freeRectangles[i].x, freeRectangles[i].y, height, width);
                        bestShortSideFit = shortSideFit;
                        bestAreaFit = areaFit;
                    }
                }
            }
            return bestNode;
        }

        private bool SplitFreeNode(Rect freeNode, ref Rect placedNode)
        {
            // Test if the rects even intersect.
            if (placedNode.x >= freeNode.x + freeNode.width || placedNode.x + placedNode.width <= freeNode.x ||
        placedNode.y >= freeNode.y + freeNode.height || placedNode.y + placedNode.height <= freeNode.y)
                return false;

            // Split along the X axis (left side)
            if (placedNode.x > freeNode.x)
                freeRectangles.Add(new Rect(freeNode.x, freeNode.y, placedNode.x - freeNode.x, freeNode.height));

            // Split along the X axis (right side)
            if (placedNode.x + placedNode.width < freeNode.x + freeNode.width)
                freeRectangles.Add(new Rect(placedNode.x + placedNode.width, freeNode.y, freeNode.x + freeNode.width - (placedNode.x + placedNode.width), freeNode.height));

            // Split along the Y axis (top side)
            if (placedNode.y > freeNode.y)
                freeRectangles.Add(new Rect(freeNode.x, freeNode.y, freeNode.width, placedNode.y - freeNode.y));

            // Split along the Y axis (bottom side)
            if (placedNode.y + placedNode.height < freeNode.y + freeNode.height)
                freeRectangles.Add(new Rect(freeNode.x, placedNode.y + placedNode.height, freeNode.width, freeNode.y + freeNode.height - (placedNode.y + placedNode.height)));

            return true;
        }

        private void PruneFreeList()
        {
            for (int i = 0; i < freeRectangles.Count; ++i)
                for (int j = i + 1; j < freeRectangles.Count; ++j)
                {
                    if (IsContainedIn(freeRectangles[i], freeRectangles[j]))
                    {
                        freeRectangles.RemoveAt(i);
                        --i;
                        break;
                    }
                    if (IsContainedIn(freeRectangles[j], freeRectangles[i]))
                    {
                        freeRectangles.RemoveAt(j);
                        --j;
                    }
                }
        }

        private bool IsContainedIn(Rect a, Rect b)
        {
            return a.x >= b.x && a.y >= b.y
              && a.x + a.width <= b.x + b.width
              && a.y + a.height <= b.y + b.height;
        }
    }

    /// <summary>
        /// A helper class to manage the iterative packing process and find the optimal atlas size.
        /// (Runtime Safe)
        /// </summary>
    public class AtlasPacker
    {
        public class PackingInput
        {
            public Material SourceMaterial;
            public int Width;
            public int Height;
            public int Padding;
        }

        public class PackingResult
        {
            public int AtlasWidth;
            public int AtlasHeight;

            public Dictionary<Material, PackingInfo> PixelRects = new();
            public bool Success;
        }

        public PackingResult Pack(List<PackingInput> inputs, bool allowRotation, AtlasSizingMode sizingMode, int customWidth, int customHeight, int minAtlasSize, int maxAtlasSize, FreeRectChoiceHeuristic heuristic)
        {
            var sortedInputs = inputs.OrderByDescending(i => Mathf.Max(i.Width, i.Height) * 1000 + Mathf.Min(i.Width, i.Height)).ToList();

            if (sizingMode == AtlasSizingMode.CustomDimensions)
            {
                return AttemptPacking(sortedInputs, customWidth, customHeight, allowRotation, heuristic);
            }
            else
            {
                int maxDimension = 0;
                long totalArea = 0;
                foreach (var input in inputs)
                {
                    int paddedWidth = input.Width + input.Padding * 2;
                    int paddedHeight = input.Height + input.Padding * 2;
                    maxDimension = Mathf.Max(maxDimension, Mathf.Max(paddedWidth, paddedHeight));
                    totalArea += (long)paddedWidth * paddedHeight;
                }

                if (sizingMode == AtlasSizingMode.AutomaticRectangularPOT)
                {
                    List<Vector2Int> permutations = new List<Vector2Int>();
                    for (int w = minAtlasSize; w <= maxAtlasSize; w *= 2)
                    {
                        for (int h = minAtlasSize; h <= maxAtlasSize; h *= 2)
                        {
                            if ((long)w * h >= totalArea && Mathf.Max(w, h) >= maxDimension)
                            {
                                permutations.Add(new Vector2Int(w, h));
                            }
                        }
                    }

                    permutations.Sort((a, b) =>
                    {
                        long areaA = (long)a.x * a.y;
                        long areaB = (long)b.x * b.y;
                        if (areaA != areaB) return areaA.CompareTo(areaB);
                        return Mathf.Abs(a.x - a.y).CompareTo(Mathf.Abs(b.x - b.y));
                    });

                    foreach (var p in permutations)
                    {
                        PackingResult result = AttemptPacking(sortedInputs, p.x, p.y, allowRotation, heuristic);
                        if (result.Success)
                        {
                            return result;
                        }
                    }
                }
                else
                {
                    int startSize = Mathf.Max(minAtlasSize, Mathf.NextPowerOfTwo(maxDimension));
                    int areaEstimate = Mathf.NextPowerOfTwo((int)Mathf.Ceil(Mathf.Sqrt(totalArea)));
                    startSize = Mathf.Max(startSize, areaEstimate);

                    for (int size = startSize; size <= maxAtlasSize; size *= 2)
                    {
                        PackingResult result = AttemptPacking(sortedInputs, size, size, allowRotation, heuristic);

                        if (result.Success)
                        {
                            return result;
                        }
                    }
                }
            }

            return new PackingResult { Success = false };
        }

        private PackingResult AttemptPacking(List<PackingInput> sortedInputs, int width, int height, bool allowRotation, FreeRectChoiceHeuristic heuristic)
        {
            MaxRectsPacker packer = new(width, height, allowRotation);
            PackingResult result = new() { AtlasWidth = width, AtlasHeight = height, Success = true };

            foreach (var input in sortedInputs)
            {
                int paddedWidth = input.Width + input.Padding * 2;
                int paddedHeight = input.Height + input.Padding * 2;

                Rect packedRect = packer.Insert(paddedWidth, paddedHeight, heuristic);

                if (packedRect.height == 0)
                {
                    result.Success = false;
                    return result;
                }

                bool isRotated = (int)packedRect.width != paddedWidth;

                result.PixelRects[input.SourceMaterial] = new PackingInfo
                {
                    PixelRect = new Rect(
                        packedRect.x + input.Padding,
                        packedRect.y + input.Padding,
                        isRotated ? input.Height : input.Width,
                        isRotated ? input.Width : input.Height
                    ),
                    IsRotated = isRotated
                };
            }

            return result;
        }
    }
}