using System.IO;
using UnityEngine;

namespace RTCore
{
    public class RTSceneController : MonoBehaviour
    {
        [Header("Scene")]
        [Tooltip("Everything below this transform with an RTSceneObject / RTLightSource is exported.")]
        public Transform sceneRoot;
        public Camera targetCamera;

        [Header("Render settings sent to the FPGA")]
        public int width = 320;
        public int height = 240;
        [Range(0, 8)] public int maxBounces = 3;
        [Range(1, 16)] public int bvhLeafSize = BVHBuilder.DefaultLeafSize;
        public Color skyHorizon = new Color(0.85f, 0.90f, 1.00f);
        public Color skyZenith = new Color(0.35f, 0.55f, 0.95f);

        [Header("Output (relative to the Unity project folder)")]
        public string outputFolder = "RTExport";
        public string sceneName = "scene";

        [Header("Gizmos")]
        [Range(0, 12)] public int bvhGizmoDepth = 3;
        public bool drawBvhGizmos = true;

        private RTScene lastScene;

        private void Reset()
        {
            sceneRoot = transform;
            targetCamera = Camera.main;
        }

        public RTScene BuildScene()
        {
            if (sceneRoot == null) sceneRoot = transform;
            if (targetCamera == null) targetCamera = Camera.main;
            lastScene = RTSceneBuilder.Build(sceneRoot, targetCamera, width, height, maxBounces, bvhLeafSize, skyHorizon, skyZenith);
            return lastScene;
        }

        [ContextMenu("Export Scene For FPGA")]
        public void ExportScene()
        {
            RTScene s = BuildScene();
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", outputFolder));
            SceneSerializer.WriteAll(s, dir, sceneName);
            Debug.Log("RTCore: exported " + s.triangles.Length + " triangles, " + s.bvh.Length + " BVH nodes to " + dir);
        }

        [ContextMenu("Validate Scene (log only)")]
        public void ValidateScene()
        {
            RTScene s = BuildScene();
            Debug.Log(SceneSerializer.Describe(s));
        }

        [ContextMenu("Save Unity Camera PNG")]
        public void SaveCameraPng()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            var rt = new RenderTexture(width, height, 24);
            var prevTarget = targetCamera.targetTexture;
            var prevActive = RenderTexture.active;
            try
            {
                targetCamera.targetTexture = rt;
                targetCamera.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();

                string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", outputFolder));
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, sceneName + "_unity.png");
                File.WriteAllBytes(file, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                Debug.Log("RTCore: wrote " + file);
            }
            finally
            {
                targetCamera.targetTexture = prevTarget;
                RenderTexture.active = prevActive;
                rt.Release();
                Object.DestroyImmediate(rt);
            }
        }

        // Draw the first few BVH levels in the Scene view.
        private void OnDrawGizmosSelected()
        {
            if (!drawBvhGizmos || lastScene == null || lastScene.bvh.Length == 0) return;
            DrawNode(0, 0);
        }

        private void DrawNode(int index, int depth)
        {
            RTBvhNode n = lastScene.bvh[index];
            Vector3 min = new Vector3((float)Fx.ToDouble(n.min.x), (float)Fx.ToDouble(n.min.y), (float)Fx.ToDouble(n.min.z));
            Vector3 max = new Vector3((float)Fx.ToDouble(n.max.x), (float)Fx.ToDouble(n.max.y), (float)Fx.ToDouble(n.max.z));
            Gizmos.color = Color.HSVToRGB((depth * 0.13f) % 1f, 0.8f, 1f);
            Gizmos.DrawWireCube((min + max) * 0.5f, max - min);
            if (n.IsLeaf || depth >= bvhGizmoDepth) return;
            DrawNode((int)n.offset, depth + 1);
            DrawNode((int)n.offset + 1, depth + 1);
        }
    }
}
