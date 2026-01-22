using System.Collections;
using System.Diagnostics.CodeAnalysis;
using SaintsField.Playa;
using Unity.EditorCoroutines.Editor;
using UnityEngine;

class HologramGenerator : MonoBehaviour
{
#if UNITY_EDITOR
    [SerializeField, NotNull] GameObject? Prefab = default;
    [SerializeField, NotNull] Material? Material = default;

    [Button]
    public void Generate() => EditorCoroutineUtility.StartCoroutine(GenerateImpl(), this);

    IEnumerator GenerateImpl()
    {
        transform.position = default;

        yield return new WaitForEndOfFrame();

        for (int i = 0; i < transform.childCount; i++)
        {
            DestroyImmediate(transform.GetChild(i).gameObject, false);
        }

        yield return new WaitForEndOfFrame();

        foreach (UnityEngine.Collider collider in GetComponents<UnityEngine.Collider>())
        {
            DestroyImmediate(collider, false);
        }

        yield return new WaitForEndOfFrame();

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

        yield return new WaitForEndOfFrame();

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

        yield return new WaitForEndOfFrame();
    }
#endif
}
