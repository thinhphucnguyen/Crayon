using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RTCore.EditorTools
{
    public static class CrayonSceneSetup
    {
        [MenuItem("RTCore/Make Crayon Demo Copy")]
        public static void Prepare()
        {
            if (EditorApplication.isPlaying)
                throw new System.InvalidOperationException("Stop Play mode before preparing the scene.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.GetActiveScene();
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/CrayonDemo.unity");
            EditorSceneManager.SaveScene(scene, path, true);
            EditorSceneManager.OpenScene(path);

            int count = 0;
            foreach (var obj in Object.FindObjectsByType<RTSceneObject>(FindObjectsSortMode.None))
            {
                var mesh = obj.GetComponent<RTCore_mesh>();
                if (mesh == null) mesh = obj.gameObject.AddComponent<RTCore_mesh>();
                RTMaterialDef material = obj.material;
                mesh.materialData = new Mat {
                    diffuseColor = new Vector3(material.albedo.r, material.albedo.g, material.albedo.b),
                    emissionColor = new Vector3(material.emission.r, material.emission.g, material.emission.b) * material.emissionStrength
                };
                mesh.objectID = count++;
                mesh.objectName = obj.name;
                if (material.reflectivity > 0 || material.transparency > 0)
                    Debug.LogWarning(obj.name + ": Crayon's material has no reflection or transparency fields; only diffuse and emission are copied.");
            }
            if (count == 0)
                throw new System.InvalidOperationException("No RTSceneObject components in the scene.");

            foreach (var renderer in Object.FindObjectsByType<FakePGA>(FindObjectsSortMode.None))
                renderer.enabled = false;
            var controller = Object.FindFirstObjectByType<RTSceneController>();
            Camera camera = controller != null ? controller.targetCamera : Camera.main;
            if (camera == null) throw new System.InvalidOperationException("Assign a camera first.");
            var demo = Object.FindFirstObjectByType<RTTraversalDemo>();
            if (demo == null) demo = new GameObject("TI_Shading_Demo").AddComponent<RTTraversalDemo>();
            demo.rtCam = camera.gameObject;
            demo.width = 160;
            demo.height = 120;
            demo.pixelStep = 2f * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad / 2f) / demo.height;
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("Crayon demo copy ready: " + count + " meshes. Press Play.");
        }
    }
}
