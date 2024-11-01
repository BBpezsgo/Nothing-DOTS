using UnityEngine;

public static class MainCamera
{
    static Camera? _camera;

    public static Camera Camera => _camera == null ? (_camera = Camera.main) : _camera;
}
