using UnityEngine;

namespace RTCore
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    public class RTSceneObject : MonoBehaviour
    {
        public RTMaterialDef material = new RTMaterialDef();

        public UnityEngine.Mesh Mesh
        {
            get { var mf = GetComponent<MeshFilter>(); return mf != null ? mf.sharedMesh : null; }
        }
    }
}
