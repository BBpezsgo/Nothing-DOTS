using System;
using System.Diagnostics.CodeAnalysis;
using NaughtyAttributes;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraControl : Singleton<CameraControl>
{
    [NotNull] CameraInput? cameraActions = default;
    [NotNull] InputAction? movement = default;
    [NotNull] InputAction? keyZoom = default;
    [NotNull] Transform? cameraTransform = default;

    [Header("Horizontal Translation")]

    [SerializeField] float maxSpeed = 5f;
    [SerializeField, ReadOnly] float speed;
    [SerializeField] float acceleration = 10f;
    [SerializeField] float damping = 15f;

    [Header("Vertical Translation")]

    [SerializeField] float stepSize = 2f;
    [SerializeField] float zoomDampening = 7.5f;
    [SerializeField] float minHeight = 5f;
    [SerializeField] float maxHeight = 50f;

    [Header("Rotation")]

    [SerializeField] float maxRotationSpeed = 1f;

    [Header("Edge Movement")]

    [Range(0f, 0.1f)]
    [SerializeField] float edgeTolerance = 0.05f;
    [SerializeField] bool useScreenEdge = true;

    public bool IsDragging
    {
        get
        {
            if ((!Mouse.current.middleButton.isPressed || !Keyboard.current.shiftKey.isPressed) &&
                !Mouse.current.middleButton.wasReleasedThisFrame) return false;
            if (UI.IsMouseHandled) return false;
            return (
                Mouse.current.position.ReadValue() - startDragScreen
            ).SqrMagnitude() > 5f;
        }
    }

    public bool IsZooming
    {
        get
        {
            if ((!Mouse.current.middleButton.isPressed || !Keyboard.current.ctrlKey.isPressed) &&
                !Mouse.current.middleButton.wasReleasedThisFrame) return false;
            if (UI.IsMouseHandled) return false;
            return (
                Mouse.current.position.ReadValue() - startDragZoomScreen
            ).SqrMagnitude() > 5f;
        }
    }

    Vector3 velocity;
    float zoomHeight;
    Vector3 horizontalVelocity;
    Vector3 lastPosition;
    Vector3 startDragWorld;
    Vector2 startDragScreen;
    Vector2 startDragZoomScreen;

    protected override void Awake()
    {
        base.Awake();
        cameraActions = new CameraInput();
        cameraTransform = GetComponentInChildren<Camera>().transform;
    }

    void OnEnable()
    {
        zoomHeight = cameraTransform.localPosition.y;

        lastPosition = transform.position;
        movement = cameraActions.Camera.Movement;
        keyZoom = cameraActions.Camera.KeyZoom;

        cameraActions.Camera.Rotate.performed += RotateCamera;
        cameraActions.Camera.ScrollZoom.performed += ZoomCameraWithWheel;

        cameraActions.Camera.Enable();
    }

    void OnDisable()
    {
        cameraActions.Camera.Rotate.performed -= RotateCamera;
        cameraActions.Camera.ScrollZoom.performed -= ZoomCameraWithWheel;

        cameraActions.Camera.Disable();
    }

    void Update()
    {
        //if (UIManager.Instance.AnyUIVisible)
        //{
        //    lastPosition = transform.position;
        //    startDragWorld = default;
        //    startDragScreen = default;
        //    return;
        //}

        GetKeyboardMovement();
        GetKeyZoom();
        ZoomCameraWithMouse();
        if (useScreenEdge)
        { CheckMouseAtScreenEdge(); }
        DragCamera();

        UpdateVelocity();
        UpdateCameraZoom();
        UpdateBasePosition();

        if (TerrainGenerator.Instance.TrySampleFast(new float2(transform.position.x, transform.position.z), out float height))
        {
            transform.position = new Vector3(transform.position.x, Mathf.Lerp(transform.position.y, height, 5f * Time.deltaTime), transform.position.z);
        }

        if (TerrainGenerator.Instance.TrySampleFast(new float2(cameraTransform.position.x, cameraTransform.position.z), out height))
        {
            if (cameraTransform.position.y <= height + minHeight)
            {
                cameraTransform.position = new Vector3(cameraTransform.position.x, height + minHeight, cameraTransform.position.z);
            }
        }

        if (ConnectionManager.ClientWorld != null && Time.timeSinceLevelLoad > 5f)
        {
            PlayerPositionSystemClient.GetInstance(ConnectionManager.ClientWorld.Unmanaged).CurrentPosition = cameraTransform.position;
        }
    }

    void UpdateVelocity()
    {
        if (Time.deltaTime > 0)
        {
            horizontalVelocity = (transform.position - lastPosition) / Time.deltaTime;
            horizontalVelocity.y = 0;
        }
        lastPosition = transform.position;
    }

    void GetKeyboardMovement()
    {
        if (UI.IsUIFocused) return;
        if (startDragWorld != default) return;

        Vector3 inputValue = (
            movement.ReadValue<Vector2>().x * GetCameraRight() +
            movement.ReadValue<Vector2>().y * GetCameraForward());
        inputValue.Normalize();

        if (inputValue.sqrMagnitude <= 0.1f) return;

        if (Keyboard.current.shiftKey.isPressed)
        { inputValue *= 2f; }

        velocity += inputValue * (zoomHeight * 0.1f);
    }

    void GetKeyZoom()
    {
        if (UI.IsUIFocused) return;

        float value = -keyZoom.ReadValue<Vector2>().y;
        if (Mathf.Abs(value) <= 0f) return;

        zoomHeight += value * stepSize * zoomHeight * 0.03f;
        zoomHeight = math.clamp(zoomHeight, minHeight, maxHeight);
    }

    Vector3 GetCameraRight()
    {
        Vector3 right = cameraTransform.right;
        right.y = 0;
        return right;
    }

    Vector3 GetCameraForward()
    {
        Vector3 forward = cameraTransform.forward;
        forward.y = 0;
        return forward;
    }

    void UpdateBasePosition()
    {
        if (velocity.sqrMagnitude > 0.001f)
        {
            speed = Mathf.Lerp(speed, maxSpeed, Time.deltaTime * acceleration);
            transform.position += velocity * (speed * Time.deltaTime);
        }
        else
        {
            speed = Mathf.Lerp(speed, 0f, Time.deltaTime * damping);
            horizontalVelocity = Vector3.Lerp(horizontalVelocity, Vector3.zero, Time.deltaTime * damping);
            transform.position += horizontalVelocity * Time.deltaTime;
        }

        velocity = Vector3.zero;
    }

    void RotateCamera(InputAction.CallbackContext inputValue)
    {
        if (UI.IsMouseHandled) return;

        if (!Mouse.current.middleButton.isPressed || Keyboard.current.shiftKey.isPressed || Keyboard.current.ctrlKey.isPressed) return;

        float x = inputValue.ReadValue<Vector2>().x;
        float y = inputValue.ReadValue<Vector2>().y;
        transform.rotation = Quaternion.Euler(
            math.clamp(y * maxRotationSpeed + transform.rotation.eulerAngles.x, 5f, 85f),
            x * maxRotationSpeed + transform.rotation.eulerAngles.y,
            0f
        );
    }

    void ZoomCameraWithWheel(InputAction.CallbackContext inputValue)
    {
        if (UI.IsMouseHandled) return;

        float value = -inputValue.ReadValue<Vector2>().y;
        if (Mathf.Abs(value) <= 0f) return;

        zoomHeight += value * stepSize;
        zoomHeight = math.clamp(zoomHeight, minHeight, maxHeight);
    }

    void ZoomCameraWithMouse()
    {
        if (UI.IsMouseHandled) return;

        if (!Mouse.current.middleButton.isPressed || !Keyboard.current.ctrlKey.isPressed)
        {
            startDragZoomScreen = default;
            return;
        }

        Vector2 mousePosition = InputUtils.Mouse.ViewportPosition;

        if (startDragZoomScreen == default)
        {
            startDragZoomScreen = mousePosition;
            return;
        }

        float beginDistance = startDragZoomScreen.y;
        float currentDistance = mousePosition.y;
        float delta = beginDistance - currentDistance;

        startDragZoomScreen = mousePosition;

        zoomHeight *= Mathf.Pow(2, delta * 5f);
        zoomHeight = math.clamp(zoomHeight, minHeight, maxHeight);
    }

    void UpdateCameraZoom()
    {
        Vector3 zoomTarget = new(
            0f,
            zoomHeight,
            0f
        );

        cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, zoomTarget, Time.deltaTime * zoomDampening);
    }

    void CheckMouseAtScreenEdge()
    {
        Vector2 mousePosition = Mouse.current.position.ReadValue();
        Vector3 moveDirection = Vector3.zero;

        if (mousePosition.x < edgeTolerance * Screen.width)
        { moveDirection -= GetCameraRight(); }
        else if (mousePosition.x > (1f - edgeTolerance) * Screen.width)
        { moveDirection += GetCameraRight(); }

        if (mousePosition.y < edgeTolerance * Screen.height)
        { moveDirection -= GetCameraForward(); }
        else if (mousePosition.y > (1f - edgeTolerance) * Screen.height)
        { moveDirection += GetCameraForward(); }

        moveDirection.Normalize();

        velocity += moveDirection * (zoomHeight * 0.1f);
    }

    void DragCamera()
    {
        if (!Mouse.current.middleButton.isPressed || !Keyboard.current.shiftKey.isPressed)
        {
            startDragWorld = default;
            startDragScreen = default;
            return;
        }

        if (UI.IsMouseHandled)
        {
            return;
        }

        Plane plane = new(Vector3.up, Vector3.zero);
        UnityEngine.Ray ray = MainCamera.Camera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (plane.Raycast(ray, out float distance) && distance < 300f)
        {
            if (startDragWorld == default)
            {
                startDragWorld = ray.GetPoint(distance);
                startDragScreen = Mouse.current.position.ReadValue();
            }
            else
            {
                velocity += startDragWorld - ray.GetPoint(distance);
            }
        }
    }
}
