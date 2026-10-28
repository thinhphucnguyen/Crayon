using System;
using System.IO;
using System.Collections.Generic;
using RTCore;

class ParityChecks
{
    static void Main(string[] args)
    {
        Directory.CreateDirectory(args[0]);
        File.WriteAllBytes(Path.Combine(args[0], "reference.rtscene"), SceneSerializer.Serialize(RTSelfTest.BuildReferenceScene()));
        var random = new Random(551);
        for (int sample = 0; sample < 50; sample++)
        {
            var input = new RTTriangle[sample * 7];
            for (int i = 0; i < input.Length; i++)
            {
                var origin = new FxVec3(random.Next(-64,65)*4096, random.Next(0,65)*4096, random.Next(-64,65)*4096);
                input[i] = new RTTriangle {
                    v0 = origin,
                    v1 = new FxVec3(origin.x + random.Next(-16,17)*4096, origin.y + random.Next(-16,17)*4096, origin.z + random.Next(-16,17)*4096),
                    v2 = new FxVec3(origin.x + random.Next(-16,17)*4096, origin.y + random.Next(-16,17)*4096, origin.z + random.Next(-16,17)*4096),
                    flags = RTTriangle.FlagDoubleSided
                };
            }
            foreach (int leafSize in new [] {1, 4, 16})
            {
                var scene = new RTScene();
                scene.materials.Add(new RTPackedMaterial {albedoR = 128});
                BVHBuilder.Build(input, leafSize, out scene.triangles, out scene.bvh);
                string error;
                if (!BVHBuilder.Validate(scene.triangles, scene.bvh, out error)) throw new Exception(error);
                File.WriteAllBytes(Path.Combine(args[0], sample + "-" + leafSize + ".rtscene"), SceneSerializer.Serialize(scene));
            }
        }
        Console.WriteLine("Generated reference + 150 scene/leaf-size cases.");
        if (args.Length > 1)
        {
            var thin = new RTTriangle {
                v0 = new FxVec3(int.MinValue, int.MinValue,0),
                v1 = new FxVec3(int.MaxValue, int.MaxValue-1,0),
                v2 = new FxVec3(int.MaxValue-1,int.MaxValue-2,0)
            };
            if (thin.IsDegenerate()) throw new Exception("Nonzero integer triangle was dropped");
            ExpectReject(() => Fx.ToFixed(double.NaN));
            ExpectReject(() => Fx.ToFixed(double.PositiveInfinity));
            ExpectReject(() => Fx.ToFixed(1, 31));
            ExpectReject(() => Fx.ToFixed(40000));
            if (Fx.ToFixed(0.5/65536) != 1 || Fx.ToFixed(-0.5/65536) != -1) throw new Exception("rounding");
            RTTriangle[] ordered; RTBvhNode[] nodes;
            BVHBuilder.Build(new [] {thin},4,out ordered,out nodes);
            Array.Resize(ref nodes,2);
            string error;
            if (BVHBuilder.Validate(ordered,nodes,out error)) throw new Exception("Unreachable node accepted");
            Console.WriteLine("PASS: exact degeneracy, nonfinite input, range, rounding, and unreachable-node regressions.");
        }
    }
    static void ExpectReject(Action action)
    {
        try { action(); } catch (ArgumentException) { return; } catch (OverflowException) { return; }
        throw new Exception("Invalid input accepted");
    }
}
