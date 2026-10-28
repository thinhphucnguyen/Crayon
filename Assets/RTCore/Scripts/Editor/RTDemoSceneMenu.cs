#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RTCore.EditorTools
{
    public static class RTDemoSceneMenu
    {
        private const string GeneratedDir = "Assets/RTCore/Generated";

        [MenuItem("RTCore/Create Demo Scene")]
        public static void CreateDemoScene()
        {
            Directory.CreateDirectory(GeneratedDir);

            var root = new GameObject("RTSceneRoot");
            Undo.RegisterCreatedObjectUndo(root, "Create RT demo scene");

            var room = Child(root, "Room");
            Quad(room, "Floor", new Vector3(0, 0, 0), new Vector3(90, 0, 0), RTMaterialDef.Diffuse(new Color(0.8f, 0.8f, 0.8f)));
            Quad(room, "Ceiling", new Vector3(0, 4, 0), new Vector3(-90, 0, 0), RTMaterialDef.Diffuse(new Color(0.8f, 0.8f, 0.8f)));
            Quad(room, "BackWall", new Vector3(0, 2, 2), new Vector3(0, 0, 0), RTMaterialDef.Diffuse(new Color(0.8f, 0.8f, 0.8f)));
            Quad(room, "LeftWall", new Vector3(-2, 2, 0), new Vector3(0, -90, 0), RTMaterialDef.Diffuse(new Color(0.85f, 0.15f, 0.15f)));
            Quad(room, "RightWall", new Vector3(2, 2, 0), new Vector3(0, 90, 0), RTMaterialDef.Diffuse(new Color(0.15f, 0.75f, 0.2f)));

            var props = Child(root, "Props");
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Setup(box, props.transform, "Box", new Vector3(-0.9f, 0.75f, 0.6f), new Vector3(0, 25, 0), new Vector3(1.5f, 1.5f, 1.5f),
                  RTMaterialDef.Diffuse(new Color(0.9f, 0.9f, 0.9f)));

            Sphere(props, "MirrorSphere", new Vector3(1.0f, 0.8f, 0.2f), 0.8f, RTMaterialDef.Mirror(new Color(0.95f, 0.95f, 0.95f)));
            Sphere(props, "GlassSphere", new Vector3(0.2f, 0.45f, -1.0f), 0.45f, RTMaterialDef.Glass(1.5f, 0.9f));

            var lightGo = Child(root, "PointLight");
            lightGo.transform.position = new Vector3(0, 3.7f, 0);
            lightGo.AddComponent<RTLightSource>().intensity = 1f;
            var unityLight = lightGo.AddComponent<Light>();     // only so the Unity preview is not dark
            unityLight.type = LightType.Point;
            unityLight.range = 12f;
            unityLight.intensity = 1.2f;

            Camera cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
                Undo.RegisterCreatedObjectUndo(camGo, "Create camera");
            }
            cam.transform.position = new Vector3(0, 2, -5.5f);
            cam.transform.rotation = Quaternion.identity;
            cam.fieldOfView = 50f;
            cam.orthographic = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.85f, 0.90f, 1.00f);

            var ctrl = root.AddComponent<RTSceneController>();
            ctrl.sceneRoot = root.transform;
            ctrl.targetCamera = cam;

            Selection.activeGameObject = root;
            AssetDatabase.SaveAssets();
            Debug.Log("RTCore: demo scene created. Select RTSceneRoot, open the RTSceneController context menu and run 'Export Scene For FPGA'.");
        }

        [MenuItem("RTCore/Write Reference Scene (parity test)")]
        public static void WriteReferenceScene()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "RTExport"));
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, "reference.rtscene");
            File.WriteAllBytes(file, SceneSerializer.Serialize(RTSelfTest.BuildReferenceScene()));
            Debug.Log("RTCore: wrote " + file + ". Now run: python3 Tools/RTCore/rtscene_inspect.py check-reference \"" + file + "\"");
        }


        private static GameObject Child(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        private static void Quad(GameObject parent, string name, Vector3 pos, Vector3 euler, RTMaterialDef def)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Setup(go, parent.transform, name, pos, euler, new Vector3(4, 4, 1), def);
        }

        private static void Sphere(GameObject parent, string name, Vector3 pos, float radius, RTMaterialDef def)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = SaveAsset(UvSphere(16, 10), "Sphere16x10");
            go.AddComponent<MeshRenderer>();
            Setup(go, parent.transform, name, pos, Vector3.zero, Vector3.one * (radius * 2f), def);
        }

        private static void Setup(GameObject go, Transform parent, string name, Vector3 pos, Vector3 euler, Vector3 scale, RTMaterialDef def)
        {
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.eulerAngles = euler;
            go.transform.localScale = scale;

            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            go.AddComponent<RTSceneObject>().material = def;

            // preview material for the Unity view only; the FPGA never sees it
            var mat = new Material(FindLitShader());
            mat.color = def.albedo;
            go.GetComponent<MeshRenderer>().sharedMaterial = SaveAsset(mat, name + "_mat");
        }

        private static Shader FindLitShader()
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Standard");
            if (s == null) s = Shader.Find("Sprites/Default");
            return s;
        }

        private static T SaveAsset<T>(T obj, string name) where T : Object
        {
            string ext = obj is Material ? ".mat" : ".asset";
            string path = AssetDatabase.GenerateUniqueAssetPath(GeneratedDir + "/" + name + ext);
            AssetDatabase.CreateAsset(obj, path);
            return obj;
        }

        // Low-poly UV sphere, unit diameter. Triangles = 2 * lon * (lat - 1).
        private static UnityEngine.Mesh UvSphere(int lon, int lat)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();

            verts.Add(new Vector3(0, 0.5f, 0));                       // 0 = top pole
            for (int i = 1; i < lat; i++)
            {
                float phi = Mathf.PI * i / lat;
                for (int j = 0; j < lon; j++)
                {
                    float theta = 2f * Mathf.PI * j / lon;
                    verts.Add(0.5f * new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta)));
                }
            }
            int bottom = verts.Count;
            verts.Add(new Vector3(0, -0.5f, 0));

            System.Func<int, int, int> ring = (i, j) => 1 + (i - 1) * lon + ((j % lon) + lon) % lon;   // ring i in 1..lat-1

            for (int j = 0; j < lon; j++)                              // top fan
            {
                tris.Add(0); tris.Add(ring(1, j + 1)); tris.Add(ring(1, j));
            }
            for (int i = 1; i < lat - 1; i++)                          // quads between rings
                for (int j = 0; j < lon; j++)
                {
                    int a = ring(i, j), b = ring(i, j + 1), c = ring(i + 1, j), d = ring(i + 1, j + 1);
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(b); tris.Add(d); tris.Add(c);
                }
            for (int j = 0; j < lon; j++)                              // bottom fan
            {
                tris.Add(ring(lat - 1, j)); tris.Add(ring(lat - 1, j + 1)); tris.Add(bottom);
            }

            var mesh = new UnityEngine.Mesh { name = "Sphere" + lon + "x" + lat };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
#endif
