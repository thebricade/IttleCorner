using UnityEngine;
using UnityEngine.InputSystem;

namespace InkFlip
{
    // WASD + mouse look (or gamepad sticks), Shift to sprint, Space to jump.
    // Click the game view to capture the mouse, Esc to release it.
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        public Transform cameraPivot;

        [Header("Movement")]
        public float walkSpeed = 4.5f;
        public float sprintSpeed = 7.5f;
        public float jumpHeight = 1.1f;
        public float gravity = -20f;

        [Header("Look")]
        public float mouseSensitivity = 0.12f;   // degrees per pixel of mouse movement
        public float gamepadLookSpeed = 180f;    // degrees per second at full stick
        public float maxPitch = 85f;

        // true while the mouse is locked to the game - other scripts (the ink gun) check this
        public static bool InputCaptured => Cursor.lockState == CursorLockMode.Locked;

        // lets another system (e.g. leaving draw mode) use this frame's Esc press without it also releasing the mouse
        static int escapeConsumedFrame = -1;
        public static void ConsumeEscape() => escapeConsumedFrame = Time.frameCount;

        CharacterController controller;
        float pitch;
        float verticalVelocity;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        void Start()
        {
            SetCursorCaptured(true);
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            bool escape = keyboard != null && keyboard.escapeKey.wasPressedThisFrame && escapeConsumedFrame != Time.frameCount;
            if (escape) SetCursorCaptured(false);
            else if (mouse != null && mouse.leftButton.wasPressedThisFrame && !InputCaptured) SetCursorCaptured(true);

            if (InputCaptured) Look(mouse);
            Move(keyboard);
        }

        void Look(Mouse mouse)
        {
            Vector2 delta = Vector2.zero;
            if (mouse != null) delta += mouse.delta.ReadValue() * mouseSensitivity;
            if (Gamepad.current != null) delta += Gamepad.current.rightStick.ReadValue() * gamepadLookSpeed * Time.deltaTime;

            transform.Rotate(0f, delta.x, 0f);
            pitch = Mathf.Clamp(pitch - delta.y, -maxPitch, maxPitch);
            if (cameraPivot != null) cameraPivot.localEulerAngles = new Vector3(pitch, 0f, 0f);
        }

        void Move(Keyboard keyboard)
        {
            Vector2 input = Vector2.zero;
            bool sprint = false;
            bool jump = false;

            if (InputCaptured && keyboard != null)
            {
                if (keyboard.wKey.isPressed) input.y += 1f;
                if (keyboard.sKey.isPressed) input.y -= 1f;
                if (keyboard.dKey.isPressed) input.x += 1f;
                if (keyboard.aKey.isPressed) input.x -= 1f;
                sprint = keyboard.leftShiftKey.isPressed;
                jump = keyboard.spaceKey.wasPressedThisFrame;
            }

            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                input += pad.leftStick.ReadValue();
                sprint |= pad.leftStickButton.isPressed;
                jump |= pad.buttonSouth.wasPressedThisFrame;
            }

            input = Vector2.ClampMagnitude(input, 1f);
            float speed = sprint ? sprintSpeed : walkSpeed;
            Vector3 horizontal = (transform.right * input.x + transform.forward * input.y) * speed;

            if (controller.isGrounded)
            {
                verticalVelocity = -2f; // keep the controller pressed onto slopes/steps
                if (jump) verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
            verticalVelocity += gravity * Time.deltaTime;

            controller.Move((horizontal + Vector3.up * verticalVelocity) * Time.deltaTime);
        }

        public static void SetCursorCaptured(bool captured)
        {
            Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !captured;
        }
    }
}
