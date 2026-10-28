using System;
using System.Collections.Generic;

namespace RTCore
{
    // One triangle, vertices in Q16.16. The exporter marks these as double-sided.
    public struct RTTriangle
    {
        public FxVec3 v0, v1, v2;
        public ushort materialId;
        public ushort flags;               // bit0 = double sided (always set for now)

        public const ushort FlagDoubleSided = 1;

        public FxVec3 BoundsMin { get { return FxVec3.Min(v0, FxVec3.Min(v1, v2)); } }
        public FxVec3 BoundsMax { get { return FxVec3.Max(v0, FxVec3.Max(v1, v2)); } }

        // Three times the centroid; long avoids overflow when summing coordinates.
        public long CentroidTimes3(int axis)
        {
            return (long)v0[axis] + v1[axis] + v2[axis];
        }

        // Skip triangles that have zero area after rounding.
        public bool IsDegenerate()
        {
            // Decimal keeps the integer cross product exact, even at the Q16.16 limits.
            decimal ax = (decimal)v1.x - v0.x, ay = (decimal)v1.y - v0.y, az = (decimal)v1.z - v0.z;
            decimal bx = (decimal)v2.x - v0.x, by = (decimal)v2.y - v0.y, bz = (decimal)v2.z - v0.z;
            decimal cx = ay * bz - az * by;
            decimal cy = az * bx - ax * bz;
            decimal cz = ax * by - ay * bx;
            return cx == 0 && cy == 0 && cz == 0;
        }
    }

    // Material record: 16 bytes in the RTS1 export.
    public struct RTPackedMaterial
    {
        public byte albedoR, albedoG, albedoB;
        public byte emitR, emitG, emitB;      // emission colour already multiplied by strength, clamped to 0..1
        public byte reflectivity;             // 0..255 -> 0..1
        public byte transparency;             // 0..255 -> 0..1
        public byte iorQ2_6;                  // index of refraction in Q2.6 (1.0 == 64)
        public byte smoothness;               // 0..255 -> 0..1

        public byte[] ToBytes()
        {
            return new byte[]
            {
                albedoR, albedoG, albedoB, 0,
                emitR, emitG, emitB, 0,
                reflectivity, transparency, iorQ2_6, smoothness,
                0, 0, 0, 0
            };
        }

        public string Key { get { return BitConverter.ToString(ToBytes()); } }
    }

    public struct RTPackedLight
    {
        public FxVec3 position;               // Q16.16
        public byte r, g, b;                  // 0..255
        public byte intensityQ4_4;            // 0..15.9375
    }

    public struct RTPackedCamera
    {
        public FxVec3 origin;                 // Q16.16 world position
        public FxVec3 dir00;                  // Q4.28 direction (unnormalised) through the centre of pixel (0,0)
        public FxVec3 dirDX;                  // Q4.28 change of direction per +1 pixel in x
        public FxVec3 dirDY;                  // Q4.28 change of direction per +1 pixel in y (downwards)
    }

    public struct RTSky
    {
        public byte horizonR, horizonG, horizonB;
        public byte zenithR, zenithG, zenithB;
    }

    // BVH node, 32 bytes on the wire. count==0 means internal node: children are offset and offset+1.
    public struct RTBvhNode
    {
        public FxVec3 min;                    // Q16.16, conservative (built from quantised vertices)
        public FxVec3 max;
        public uint offset;                   // internal: index of left child (right = offset+1); leaf: first triangle
        public ushort count;                  // 0 = internal, otherwise number of triangles in the leaf
        public ushort reserved;

        public bool IsLeaf { get { return count != 0; } }
    }

    // Object names and counts for the text report only.
    public struct RTObjectInfo
    {
        public string path;                   // e.g. "SceneRoot/Room/LeftWall"
        public int triangleCount;
        public int materialId;
    }

    public class RTScene
    {
        public ushort width = 320;
        public ushort height = 240;
        public byte maxBounces = 3;

        public RTPackedCamera camera;
        public RTSky sky;

        public List<RTPackedLight> lights = new List<RTPackedLight>();
        public List<RTPackedMaterial> materials = new List<RTPackedMaterial>();
        public RTTriangle[] triangles = new RTTriangle[0];      // already in BVH leaf order
        public RTBvhNode[] bvh = new RTBvhNode[0];

        public List<RTObjectInfo> objects = new List<RTObjectInfo>();
        public int droppedDegenerateTriangles;

        public int BvhDepth()
        {
            if (bvh.Length == 0) return 0;
            return DepthOf(0);
        }

        private int DepthOf(int node)
        {
            if (bvh[node].IsLeaf) return 1;
            int l = DepthOf((int)bvh[node].offset);
            int r = DepthOf((int)bvh[node].offset + 1);
            return 1 + Math.Max(l, r);
        }
    }

    // Export size estimate. The hardware memory budget is not agreed yet.
    public static class RTBudget
    {
        public const int TriangleBytes = 40;
        public const int BvhNodeBytes = 32;
        public const int MaterialBytes = 16;

        // Placeholder limit; update when the team agrees on the memory map.
        public const int DefaultBudgetBytes = 256 * 1024;

        public static int SceneBytes(RTScene s)
        {
            return s.triangles.Length * TriangleBytes + s.bvh.Length * BvhNodeBytes + s.materials.Count * MaterialBytes;
        }
    }
}
