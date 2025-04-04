using System.Diagnostics.CodeAnalysis;
using System.Linq;
using NaughtyAttributes;
using UnityEngine;

public class HologramGenerator : MonoBehaviour
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

        foreach (var collider in GetComponents<UnityEngine.Collider>())
        {
            DestroyImmediate(collider, false);
        }

        if (Prefab.TryGetComponent(out BoxCollider boxCollider))
        {
            var newBoxCollider = gameObject.AddComponent<BoxCollider>();
            newBoxCollider.center = boxCollider.center;
            newBoxCollider.size = boxCollider.size;
        }
        else if (Prefab.TryGetComponent(out UnityEngine.SphereCollider sphereCollider))
        {
            var newSphereCollider = gameObject.AddComponent<UnityEngine.SphereCollider>();
            newSphereCollider.center = sphereCollider.center;
            newSphereCollider.radius = sphereCollider.radius;
        }

        var renderers = Prefab.GetComponentsInChildren<MeshRenderer>(false);
        foreach (var renderer in renderers)
        {
            var meshFilter = renderer.GetComponent<MeshFilter>();

            GameObject newObject = new(renderer.name, typeof(MeshRenderer), typeof(MeshFilter));
            newObject.transform.SetParent(transform);

            var newMeshFilter = newObject.GetComponent<MeshFilter>();
            var newRenderer = newObject.GetComponent<MeshRenderer>();

            newMeshFilter.sharedMesh = meshFilter.sharedMesh;
            newRenderer.sharedMaterial = Material;

            newObject.transform.SetPositionAndRotation(renderer.transform.position, renderer.transform.rotation);
            newObject.transform.localScale = renderer.transform.localScale;
        }
    }
#endif
}
