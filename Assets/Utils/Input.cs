using UnityEngine;

public static class InputUtils
{
    public static class Mouse
    {
        public static Vector2 ViewportPosition => MainCamera.Camera.ScreenToViewportPoint(UnityEngine.InputSystem.Mouse.current.position.ReadValue());
    }
}
