using System.Diagnostics.CodeAnalysis;
using NaughtyAttributes;
using TMPro;
using UnityEngine;

public class WorldLabel : MonoBehaviour
{
    [SerializeField, Required, NotNull] public TextMeshProUGUI? TextMeshPro = default;

    void Start()
    {
        this.GetRequiredComponent<TextMeshProUGUI>();
    }

    void Update()
    {
        transform.LookAt(transform.position * 2f - MainCamera.Camera.transform.position, Vector3.up);
    }
}
