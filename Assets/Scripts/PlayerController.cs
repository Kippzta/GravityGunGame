using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float mouseSense = 0.1f;

    public Transform cameraTransform;

    private CharacterController characterController;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private float verticalRotation = 0f;

    void Start()
    {
        characterController = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;

        // Om du glömmer att dra in kameran i Unity letar skriptet upp Main Camera automatiskt
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    void Update()
    {
        handleMovement();
        handleLook();
    }

    // Denna funktion lyssnar på Unitys inbyggda WASD-system automatisk
    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    public void OnLook(InputValue value)
    {
        lookInput = value.Get<Vector2>();
    }

    void handleMovement()
    {
        Vector3 moveDirection = transform.right * moveInput.x + transform.forward * moveInput.y;
        characterController.Move(moveDirection * moveSpeed * Time.deltaTime);
    }

    void handleLook()
    {
        // lookInput.x är musen i sidled, lookInput.y är musen i höjdled
        float mouseX = lookInput.x * mouseSense;
        float mouseY = lookInput.y * mouseSense;

        // Rotera gubben i sidled (Axlarna)
        transform.Rotate(Vector3.up * mouseX);

        // Rotera kameran upp och ner
        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -90f, 90f);

        if (cameraTransform != null)
        {
            cameraTransform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
        }
    }

}