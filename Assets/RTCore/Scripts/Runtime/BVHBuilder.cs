using System;
using System.Collections.Generic;

namespace RTCore
{
    public static class BVHBuilder
    {
        public const int DefaultLeafSize = 4;

        public static void Build(RTTriangle[] input, int leafSize, out RTTriangle[] ordered, out RTBvhNode[] nodes)
        {
            if (leafSize < 1) leafSize = 1;
            if (leafSize > 255) leafSize = 255;

            if (input == null || input.Length == 0)
            {
                ordered = new RTTriangle[0];
                nodes = new RTBvhNode[0];
                return;
            }

            int[] indices = new int[input.Length];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;
            var tree = new List<RTBvhNode> { new RTBvhNode() };
            BuildNode(0, 0, input.Length);

            ordered = new RTTriangle[input.Length];
            for (int i = 0; i < ordered.Length; i++) ordered[i] = input[indices[i]];
            nodes = tree.ToArray();

            void BuildNode(int nodeIndex, int start, int count)
            {
                RTTriangle first = input[indices[start]];
                FxVec3 min = first.BoundsMin, max = first.BoundsMax;
                long[] centroidMin = new long[3], centroidMax = new long[3];
                for (int axis = 0; axis < 3; axis++)
                    centroidMin[axis] = centroidMax[axis] = first.CentroidTimes3(axis);

                for (int i = start + 1; i < start + count; i++)
                {
                    RTTriangle triangle = input[indices[i]];
                    min = FxVec3.Min(min, triangle.BoundsMin);
                    max = FxVec3.Max(max, triangle.BoundsMax);
                    for (int axis = 0; axis < 3; axis++)
                    {
                        long center = triangle.CentroidTimes3(axis);
                        centroidMin[axis] = Math.Min(centroidMin[axis], center);
                        centroidMax[axis] = Math.Max(centroidMax[axis], center);
                    }
                }

                if (count <= leafSize)
                {
                    tree[nodeIndex] = new RTBvhNode {
                        min = min, max = max, offset = (uint)start, count = (ushort)count
                    };
                    return;
                }

                int splitAxis = 0;
                for (int axis = 1; axis < 3; axis++)
                    if (centroidMax[axis] - centroidMin[axis] > centroidMax[splitAxis] - centroidMin[splitAxis])
                        splitAxis = axis;

                // Keep input order if all centroids coincide; otherwise sort by center, then index.
                if (centroidMax[splitAxis] > centroidMin[splitAxis])
                    Array.Sort(indices, start, count, Comparer<int>.Create((a, b) => {
                        int comparison = input[a].CentroidTimes3(splitAxis).CompareTo(input[b].CentroidTimes3(splitAxis));
                        return comparison != 0 ? comparison : a.CompareTo(b);
                    }));

                int left = tree.Count;
                tree.Add(new RTBvhNode());
                tree.Add(new RTBvhNode());
                tree[nodeIndex] = new RTBvhNode { min = min, max = max, offset = (uint)left };
                int leftCount = count / 2;
                BuildNode(left, start, leftCount);
                BuildNode(left + 1, start + leftCount, count - leftCount);
            }
        }

        // Check child links, bounds, and triangle coverage.
        public static bool Validate(RTTriangle[] tris, RTBvhNode[] nodes, out string error)
        {
            error = null;
            if (tris.Length == 0) return nodes.Length == 0 || Fail(out error, "non-empty BVH for empty scene");
            if (nodes.Length == 0) return Fail(out error, "empty BVH for non-empty scene");

            var covered = new int[tris.Length];
            var visited = new bool[nodes.Length];
            var stack = new Stack<int>();
            stack.Push(0);
            while (stack.Count > 0)
            {
                int n = stack.Pop();
                if (n < 0 || n >= nodes.Length) return Fail(out error, "node index out of range: " + n);
                if (visited[n]) return Fail(out error, "node visited twice (cycle or shared child): " + n);
                visited[n] = true;
                RTBvhNode node = nodes[n];
                if (node.IsLeaf)
                {
                    long end = (long)node.offset + node.count;
                    if (end > tris.Length) return Fail(out error, "leaf " + n + " reads past the triangle array");
                    for (int t = (int)node.offset; t < end; t++)
                    {
                        covered[t]++;
                        if (!Contains(node, tris[t].BoundsMin, tris[t].BoundsMax))
                            return Fail(out error, "leaf " + n + " box does not contain triangle " + t);
                    }
                }
                else
                {
                    long l = node.offset, r = node.offset + 1L;
                    if (r >= nodes.Length) return Fail(out error, "internal node " + n + " has children out of range");
                    if (!Contains(node, nodes[l].min, nodes[l].max) || !Contains(node, nodes[r].min, nodes[r].max))
                        return Fail(out error, "internal node " + n + " box does not contain its children");
                    stack.Push((int)r);
                    stack.Push((int)l);
                }
            }
            for (int i = 0; i < visited.Length; i++)
                if (!visited[i]) return Fail(out error, "unreachable node: " + i);
            for (int i = 0; i < covered.Length; i++)
                if (covered[i] != 1) return Fail(out error, "triangle " + i + " referenced " + covered[i] + " times (expected 1)");
            return true;
        }

        private static bool Contains(RTBvhNode outer, FxVec3 min, FxVec3 max)
        {
            return outer.min.x <= min.x && outer.min.y <= min.y && outer.min.z <= min.z &&
                   outer.max.x >= max.x && outer.max.y >= max.y && outer.max.z >= max.z;
        }

        private static bool Fail(out string error, string msg) { error = msg; return false; }


    }
}
