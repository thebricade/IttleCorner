using UnityEngine;
using UnityEngine.InputSystem;

namespace InkFlip
{
    // A stand-in objective: completes when something calls Complete() (a UnityEvent, a trigger, another script)
    // or when you press the test key. Handy for wiring up a room before its real mini-game exists.
    public class ManualObjective : RoomObjective
    {
        [Tooltip("Press this to complete the room while testing. None turns it off.")]
        public Key testKey = Key.None;

        RoomZone zone;

        public void Complete()
        {
            MarkComplete();
        }

        void Awake()
        {
            zone = GetComponent<RoomZone>();
        }

        void Update()
        {
            if (testKey == Key.None || IsComplete) return;
            // when it sits on a room, the test key only works from inside that room
            if (zone != null && !zone.PlayerInside) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[testKey].wasPressedThisFrame) MarkComplete();
        }
    }
}
