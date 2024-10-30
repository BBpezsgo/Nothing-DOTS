using System;
using System.Diagnostics.CodeAnalysis;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraControl : MonoBehaviour
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
    [SerializeField] float zoomSpeed = 2f;

    [Header("Rotation")]

    [SerializeField] float maxRotationSpeed = 1f;

    [Header("Edge Movement")]

    [Range(0f, 0.1f)]
    [SerializeField] float edgeTolerance = 0.05f;
    [SerializeField] bool useScreenEdge = true;

    Vector3 velocity;
    float zoomHeight;
    Vector3 horizontalVelocity;
    Vector3 lastPosition;
    Vector3 startDrag;

    void Awake()
    {
        cameraActions = new CameraInput();
        cameraTransform = GetComponentInChildren<Camera>().transform;
    }

    void OnEnable()
    {
        zoomHeight = cameraTransform.localPosition.y;
        cameraTransform.LookAt(transform);
        // cameraTransform.localRotation = Quaternion.Euler(45f, 0f, 0f);

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
        GetKeyboardMovement();
        GetKeyZoom();
        if (useScreenEdge)
        { CheckMouseAtScreenEdge(); }
        DragCamera();

        UpdateVelocity();
        UpdateCameraZoom();
        UpdateBasePosition();
    }

    void UpdateVelocity()
    {
        horizontalVelocity = (transform.position - lastPosition) / Time.deltaTime;
        horizontalVelocity.y = 0;
        lastPosition = transform.position;
    }

    void GetKeyboardMovement()
    {
        Vector3 inputValue = (
            movement.ReadValue<Vector2>().x * GetCameraRight() +
            movement.ReadValue<Vector2>().y * GetCameraForward());
        inputValue.Normalize();

        if (inputValue.sqrMagnitude <= 0.1f) return;

        if (Keyboard.current.shiftKey.isPressed)
        { inputValue *= 2f; }

        velocity += inputValue;
    }

    void GetKeyZoom()
    {
        float value = -keyZoom.ReadValue<Vector2>().y;
        if (Mathf.Abs(value) <= 0f) return;

        zoomHeight += value * stepSize * zoomHeight * 0.03f;
        zoomHeight = Math.Clamp(zoomHeight, minHeight, maxHeight);
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
        velocity *= zoomHeight * 0.1f;

        if (velocity.sqrMagnitude > 0.001f)
        {
            speed = Mathf.Lerp(speed, maxSpeed, Time.deltaTime * acceleration);
            transform.position += velocity * (speed * Time.deltaTime);
        }
        else
        {
            horizontalVelocity = Vector3.Lerp(horizontalVelocity, Vector3.zero, Time.deltaTime * damping);
            transform.position += horizontalVelocity * Time.deltaTime;
        }

        velocity = Vector3.zero;
    }

    void RotateCamera(InputAction.CallbackContext inputValue)
    {
        if (!Mouse.current.middleButton.isPressed || UI.IsMouseHandled) return;

        float x = inputValue.ReadValue<Vector2>().x;
        float y = inputValue.ReadValue<Vector2>().y;
        transform.rotation = Quaternion.Euler(
            Math.Clamp(y * maxRotationSpeed + transform.rotation.eulerAngles.x, 5f, 85f),
            x * maxRotationSpeed + transform.rotation.eulerAngles.y,
            0f
        );
    }

    void ZoomCameraWithWheel(InputAction.CallbackContext inputValue)
    {
        float value = -inputValue.ReadValue<Vector2>().y;
        if (Mathf.Abs(value) <= 0f) return;

        zoomHeight += value * stepSize;
        zoomHeight = Math.Clamp(zoomHeight, minHeight, maxHeight);
    }

    void UpdateCameraZoom()
    {
        Vector3 zoomTarget = new(
            0f,
            zoomHeight,
            0f
        );

        cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, zoomTarget, Time.deltaTime * zoomDampening);
        cameraTransform.LookAt(transform);
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

        velocity += moveDirection;
    }

    void DragCamera()
    {
        if (!Mouse.current.rightButton.isPressed || UI.IsMouseHandled) return;

        Plane plane = new(Vector3.up, Vector3.zero);
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (plane.Raycast(ray, out float distance))
        {
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                startDrag = ray.GetPoint(distance);
            }
            else
            {
                velocity += startDrag - ray.GetPoint(distance);
            }
        }
    }
}
