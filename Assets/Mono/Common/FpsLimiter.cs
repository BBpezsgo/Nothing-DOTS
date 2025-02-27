using UnityEngine;

public class FpsLimiter : MonoBehaviour
{
    [SerializeField, Min(1)] int TargetFPS = 60;

    void Start()
    {
        Application.targetFrameRate = TargetFPS;
    }
}
