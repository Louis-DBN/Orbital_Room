using UnityEngine;

namespace Features.Movement
{
    /// <summary>
    /// Ensures every MeshFilter in this hierarchy has a MeshCollider at runtime
    /// so imported 3D environment models support 0G body collisions and hand grabs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MeshColliderPopulator : MonoBehaviour
    {
        [SerializeField] private bool includeInactive;
        [SerializeField] private bool convex;

        private void Awake()
        {
            MeshFilter[] filters = GetComponentsInChildren<MeshFilter>(includeInactive);
            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter filter = filters[i];
                if (filter == null || filter.sharedMesh == null)
                    continue;

                if (filter.GetComponent<Collider>() != null)
                    continue;

                MeshCollider meshCollider = filter.gameObject.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = filter.sharedMesh;
                meshCollider.convex = convex;
            }
        }
    }
}
