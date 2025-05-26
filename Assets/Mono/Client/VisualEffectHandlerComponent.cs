using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.VFX;

class VisualEffectHandlerComponent : MonoBehaviour
{
    public float Lifetime;
    public float Age;
    public ObjectPool<VisualEffect>? Pool;

    public void Reinit()
    {
        Age = 0f;
    }

    void Update()
    {
        Age += Time.deltaTime;
        if (Age >= Lifetime)
        {
            Pool!.Release(GetComponent<VisualEffect>());
        }
    }
}