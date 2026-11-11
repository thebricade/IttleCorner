using UnityEngine;
using UnityEngine.InputSystem;

namespace InkFlip
{
    // The Glove Room is finished when the glove has been decorated, picked up, and the switch puzzle solved.
    public class GloveRoomObjective : RoomObjective
    {
        public GloveRoomGame game;
        public RoomZone zone;
        [Tooltip("Press this key inside the room to finish it while testing (you still get the glove).")]
        public Key testKey = Key.K;

        public override float Progress => IsComplete ? 1f : (game != null ? game.Progress : 0f);

        void Update()
        {
            if (IsComplete) return;

            if (testKey != Key.None && zone != null && zone.PlayerInside && Keyboard.current != null && Keyboard.current[testKey].wasPressedThisFrame)
            {
                ForceComplete();
                return;
            }

            if (game != null && game.Solved) MarkComplete();
        }

        // a conversation can finish the room too; the player still ends up with the glove
        public override void ForceComplete()
        {
            if (game != null) game.SkipToSolved();
            MarkComplete();
        }
    }
}
