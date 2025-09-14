using System.Diagnostics.CodeAnalysis;
using NaughtyAttributes;
using UnityEngine;

class HologramGenerator : MonoBehaviour
{
#if UNITY_EDITOR
    [SerializeField, NotNull] GameObject? Prefab = default;
    [SerializeField, NotNull] Material? Material = default;

    [Button("Generate", EButtonEnableMode.Editor)]
    public void Generate()
    {
        transform.position = default;

        for (int i = 0; i < transform.childCount; i++)
        {
            DestroyImmediate(transform.GetChild(i).gameObject, false);
        }

        foreach (UnityEngine.Collider collider in GetComponents<UnityEngine.Collider>())
        {
            DestroyImmediate(collider, false);
        }

        if (Prefab.TryGetComponent(out BoxCollider boxCollider))
        {
            BoxCollider newBoxCollider = gameObject.AddComponent<BoxCollider>();
            newBoxCollider.center = boxCollider.center;
            newBoxCollider.size = boxCollider.size;
        }
        else if (Prefab.TryGetComponent(out UnityEngine.SphereCollider sphereCollider))
        {
            UnityEngine.SphereCollider newSphereCollider = gameObject.AddComponent<UnityEngine.SphereCollider>();
            newSphereCollider.center = sphereCollider.center;
            newSphereCollider.radius = sphereCollider.radius;
        }

        MeshRenderer[] renderers = Prefab.GetComponentsInChildren<MeshRenderer>(false);
        foreach (MeshRenderer renderer in renderers)
        {
            MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();

            GameObject newObject = new(renderer.name, typeof(MeshRenderer), typeof(MeshFilter));
            newObject.transform.SetParent(transform);

            MeshFilter newMeshFilter = newObject.GetComponent<MeshFilter>();
            MeshRenderer newRenderer = newObject.GetComponent<MeshRenderer>();

            newMeshFilter.sharedMesh = meshFilter.sharedMesh;
            newRenderer.sharedMaterial = Material;

            newObject.transform.SetPositionAndRotation(renderer.transform.position, renderer.transform.rotation);
            newObject.transform.localScale = renderer.transform.localScale;
        }
    }
#endif
}
