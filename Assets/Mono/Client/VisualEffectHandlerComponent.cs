using System;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.VFX;

class VisualEffectHandlerComponent : MonoBehaviour
{
    public float Lifetime;
    public float Age;
    public BufferedVisualEffect Asset;
    [NotNull] public VisualEffect? VisualEffect = null;
    public Light? Light;
    public ObjectPool<VisualEffectHandlerComponent>? Pool;

    public void Reinit()
    {
        Age = 0f;

        if (Light != null)
        {
            Light.range = Asset.LightRange;
            Light.intensity = 0f;
            Light.enabled = true;
        }
    }

    void Update()
    {
        Age += Time.deltaTime;

        if (Age >= Lifetime && VisualEffect.aliveParticleCount == 0)
        {
            if (Light != null)
            {
                Light.range = Asset.LightRange;
                Light.intensity = 0f;
                Light.enabled = false;
            }
            Pool!.Release(this);
        }
        else
        {
            if (Light != null)
            {
                float t = 1f - (Math.Min(Age, Lifetime) / Lifetime);
                Light.intensity = Asset.LightIntensity * t;
                Light.range = Asset.LightRange;
            }
        }
    }
}
