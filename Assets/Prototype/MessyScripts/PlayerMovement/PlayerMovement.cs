using UnityEngine;
using UnityEngine.AI;           // For NavMeshAgent
using UnityEngine.InputSystem;  // For New Input System (Mouse, Keyboard, Touch)

public class PlayerMovement : MonoBehaviour
{
    [Header("Settings")]
    public float moveSpeed = 5f; // public so i can adjust in the inspector
    public LayerMask groundLayer;

    private NavMeshAgent agent;
    private Camera mainCamera;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        mainCamera = Camera.main;

        // Make sure the NavMeshAgent uses your custom speed for clicking
        agent.speed = moveSpeed;
    }

    // Update is called once per frame
    void Update()
    {
        // 1. Respect your GameModeManager
        if (GameModeManager.Instance.currentMode != GameMode.Explore)
        {
            // If we aren't exploring, stop the agent from moving and ignore input
            if (agent.hasPath) agent.ResetPath();
            return;
        }

        // 2. Check for inputs
        HandleKeyboardMovement();
        HandleClickMovement();
    }

    private void HandleKeyboardMovement()
    {
        float horizontal = 0f;
        float vertical = 0f; // this may need to be looked at with the new input system

        // Read WASD or Arrow Keys using the New Input System
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) vertical += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) vertical -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) horizontal += 1f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) horizontal -= 1f;
        }

        // Combine inputs and normalize so diagonal movement isn't twice as fast
        Vector3 moveDirection = new Vector3(horizontal, 0f, vertical).normalized;

        // If the player is pressing a movement key...
        if (moveDirection.magnitude > 0.1f)
        {
            // IMPORTANT: Cancel any active click-to-move path so they don't fight
            if (agent.hasPath)
            {
                agent.ResetPath();
            }

            // Manually move the agent. This acts just like CharacterController.Move() 
            // but respects your baked NavMesh!
            agent.Move(moveDirection * moveSpeed * Time.deltaTime);
        }
    }

    private void HandleClickMovement()
    {
        Vector2 pointerPosition = Vector2.zero;
        bool isPointerPressed = false;

        // Check Mouse/Touch inputs
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            pointerPosition = Mouse.current.position.ReadValue();
            isPointerPressed = true;
        }
        else if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            pointerPosition = Touchscreen.current.primaryTouch.position.ReadValue();
            isPointerPressed = true;
        }

        if (isPointerPressed)
        {
            Ray ray = mainCamera.ScreenPointToRay(pointerPosition);
            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, groundLayer))
            {
                // Tell the agent to take the wheel and drive to the clicked spot
                agent.SetDestination(hit.point);
            }
        }
    }
}