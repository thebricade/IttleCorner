using UnityEngine;
using UnityEngine.InputSystem;

namespace InkFlip
{
    // The cat room is finished when the cat has been brought down from the tree.
    public class CatRoomObjective : RoomObjective
    {
        public CatInTree cat;
        public RoomZone zone;
        [Tooltip("Press this key inside the room to finish it while testing.")]
        public Key testKey = Key.K;

        void Update()
        {
            if (IsComplete) return;

            if (testKey != Key.None && zone != null && zone.PlayerInside && Keyboard.current != null && Keyboard.current[testKey].wasPressedThisFrame)
            {
                if (cat != null) cat.ForceRescue();
                MarkComplete();
                return;
            }
            if (cat != null && cat.Landed) MarkComplete();
        }

        public override void ForceComplete()
        {
            if (cat != null) cat.ForceRescue();
            base.ForceComplete();
        }
    }
}
