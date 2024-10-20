using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

#nullable enable

public static class CameraExtensions
{
    const float ScreenRayMaxDistance = 500f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ScreenToWorldPosition(this Camera camera, Vector2 screenPosition, out Vector3 worldPosition)
        => ScreenToWorldPosition(camera, screenPosition, ScreenRayMaxDistance, out worldPosition);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 ScreenToWorldPosition(this Camera camera, Vector2 screenPosition)
        => ScreenToWorldPosition(camera, screenPosition, ScreenRayMaxDistance);
    public static bool ScreenToWorldPosition(this Camera camera, Vector2 screenPosition, float maxDistance, out Vector3 worldPosition)
    {
        Ray ray = camera.ScreenPointToRay(screenPosition);
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance))
        {
            worldPosition = hit.point;
            return true;
        }
        worldPosition = default;
        return false;
    }
    public static Vector3 ScreenToWorldPosition(this Camera camera, Vector2 screenPosition, float maxDistance)
    {
        Ray ray = camera.ScreenPointToRay(screenPosition);
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance))
        {
            return hit.point;
        }
        return ray.GetPoint(maxDistance);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ScreenToWorldPosition(this Camera camera, Vector2 screenPosition, LayerMask layerMask, out Vector3 worldPosition)
        => ScreenToWorldPosition(camera, screenPosition, ScreenRayMaxDistance, layerMask, out worldPosition);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 ScreenToWorldPosition(this Camera camera, Vector2 screenPosition, LayerMask layerMask)
        => ScreenToWorldPosition(camera, screenPosition, ScreenRayMaxDistance, layerMask);
    public static bool ScreenToWorldPosition(this Camera camera, Vector2 screenPosition, float maxDistance, LayerMask layerMask, out Vector3 worldPosition)
    {
        Ray ray = camera.ScreenPointToRay(screenPosition);
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, layerMask))
        {
            worldPosition = hit.point;
            return true;
        }
        worldPosition = default;
        return false;
    }
    public static Vector3 ScreenToWorldPosition(this Camera camera, Vector2 screenPosition, float maxDistance, LayerMask layerMask)
    {
        Ray ray = camera.ScreenPointToRay(screenPosition);
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, layerMask))
        { return hit.point; }
        return ray.GetPoint(maxDistance);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 ScreenToWorldPosition(this Camera camera, Vector2 screenPosition, out RaycastHit[] hits)
        => ScreenToWorldPosition(camera, screenPosition, ScreenRayMaxDistance, out hits);
    public static Vector3 ScreenToWorldPosition(this Camera camera, Vector2 screenPosition, float maxDistance, out RaycastHit[] hits)
    {
        Ray ray = camera.ScreenPointToRay(screenPosition);
        hits = Physics.RaycastAll(ray, maxDistance).Where(v => !v.collider.isTrigger).ToArray();
        if (hits.Length > 0)
        { return hits[0].point; }
        return ray.GetPoint(maxDistance);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 ScreenToWorldPosition(this Camera camera, Vector2 screenPosition, int layerMask, out RaycastHit[] hits)
        => ScreenToWorldPosition(camera, screenPosition, ScreenRayMaxDistance, layerMask, out hits);
    public static Vector3 ScreenToWorldPosition(this Camera camera, Vector2 screenPosition, float maxDistance, int layerMask, out RaycastHit[] hits)
    {
        Ray ray = camera.ScreenPointToRay(screenPosition);
        hits = Physics.RaycastAll(ray, maxDistance, layerMask).Where(v => !v.collider.isTrigger).ToArray();
        if (hits.Length > 0)
        { return hits[0].point; }
        return ray.GetPoint(maxDistance);
    }
}