using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.VFX;

class VisualEffectHandlerComponent : MonoBehaviour
{
    public float Lifetime;
    public float Age;
    public VisualEffect VisualEffect;
    public ObjectPool<VisualEffect>? Pool;

    public void Reinit()
    {
        Age = 0f;
    }

    void Update()
    {
        Age += Time.deltaTime;
        if (Age >= Lifetime && VisualEffect.aliveParticleCount == 0)
        {
            Pool!.Release(VisualEffect);
        }
    }
}