using System.Diagnostics.CodeAnalysis;
using System.Linq;
using SaintsField.Playa;
using UnityEngine;

[AddComponentMenu("Authoring/Research Database")]
class ResearchDatabaseAuthoring : MonoBehaviour
{
    [SerializeField, NotNull] ResearchMetadata[]? Researches = default;

    [Button]
    void Generate()
    {
        foreach (GameObject? item in Enumerable.Range(0, transform.childCount).Select(i => transform.GetChild(i).gameObject).ToArray())
        {
            DestroyImmediate(item);
        }

        foreach (ResearchMetadata research in Researches)
        {
            GameObject o = new("Research", typeof(ResearchAuthoring));
            o.transform.SetParent(transform);
            o.GetComponent<ResearchAuthoring>().Metadata = research;
        }
    }
}
