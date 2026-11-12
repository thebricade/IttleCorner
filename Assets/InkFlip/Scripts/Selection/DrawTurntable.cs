using UnityEngine;
using UnityEngine.InputSystem;

namespace InkFlip
{
    // Put this on a Paintable that should be turnable while you draw on it, so you can reach its other sides.
    // In draw mode: hold the right mouse button and drag, or use A / D (turn) and W / S (tip), or the arrow keys,
    // or the right stick. Paint belongs to the object's surface, so what you drew stays where you drew it as it turns.
    public class DrawTurntable : MonoBehaviour
    {
        [Tooltip("Degrees per screen pixel of right-button drag.")]
        public float dragDegreesPerPixel = 0.4f;
        [Tooltip("Degrees per second for the keys and the stick.")]
        public float keyDegreesPerSecond = 110f;
        [Tooltip("Tip it towards or away from you at most this far. 0 turns it only side to side.")]
        public float maxTilt = 70f;

        public float Yaw { get; private set; }
        public float Tilt { get; private set; }

        Quaternion rest;
        bool captured;
        float pendingYaw, pendingTilt;

        // turn it by this many degrees, smoothly over the next moments (used by the on-screen arrow buttons)
        public void Nudge(float yawDegrees, float tiltDegrees)
        {
            pendingYaw += yawDegrees;
            pendingTilt += tiltDegrees;
        }

        void Awake()
        {
            Capture();
        }

        void Capture()
        {
            if (captured) return;
            rest = transform.rotation;
            captured = true;
        }

        // put it back the way it started (used if the object is moved to a new resting rotation)
        public void ResetTurn(Quaternion newRest)
        {
            rest = newRest;
            captured = true;
            Yaw = 0f;
            Tilt = 0f;
        }

        // called every frame by the draw mode while this is the object being drawn on; viewer is the camera looking at it
        public void Turn(Mouse mouse, Keyboard keyboard, Gamepad pad, Transform viewer)
        {
            Capture();

            float yaw = 0f;
            float tilt = 0f;

            if (mouse != null && mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                yaw -= delta.x * dragDegreesPerPixel;
                tilt += delta.y * dragDegreesPerPixel;
            }

            // the arrow buttons: ease towards the turn they asked for
            float easeStep = 540f * Time.unscaledDeltaTime;
            float easeYaw = Mathf.Clamp(pendingYaw, -easeStep, easeStep);
            float easeTilt = Mathf.Clamp(pendingTilt, -easeStep, easeStep);
            pendingYaw -= easeYaw;
            pendingTilt -= easeTilt;
            yaw += easeYaw;
            tilt += easeTilt;

            float step = keyDegreesPerSecond * Time.unscaledDeltaTime;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) yaw += step;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) yaw -= step;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) tilt += step;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) tilt -= step;
            }
            if (pad != null)
            {
                Vector2 stick = pad.rightStick.ReadValue();
                yaw -= stick.x * step;
                tilt += stick.y * step;
            }

            if (Mathf.Approximately(yaw, 0f) && Mathf.Approximately(tilt, 0f)) return;

            Yaw = Mathf.Repeat(Yaw + yaw, 360f);
            Tilt = Mathf.Clamp(Tilt + tilt, -maxTilt, maxTilt);

            // turn about the vertical, then tip about the viewer's right-hand axis (so tipping always leans it towards or away from you)
            Vector3 right = viewer != null ? viewer.right : Vector3.right;
            transform.rotation = Quaternion.AngleAxis(Tilt, right) * Quaternion.AngleAxis(Yaw, Vector3.up) * rest;
        }
    }
}
