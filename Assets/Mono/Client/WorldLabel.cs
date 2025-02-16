using System.Diagnostics.CodeAnalysis;
using TMPro;
using UnityEngine;

public class WorldLabel : MonoBehaviour
{
    [SerializeField, NotNull] public TextMeshProUGUI? TextMeshPro = default;

    void Update()
    {
        transform.LookAt(transform.position * 2f - MainCamera.Camera.transform.position, Vector3.up);
    }
}
