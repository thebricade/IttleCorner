using UnityEngine;
using UnityEngine.InputSystem;

namespace InkFlip
{
    // The Hand Mix room is finished when enough samples (the game's Colors Needed, three by default) have been matched.
    public class HandMixObjective : RoomObjective
    {
        public HandMixGame game;
        public RoomZone zone;
        [Tooltip("Press this key inside the room to finish it while testing.")]
        public Key testKey = Key.K;

        public override float Progress
        {
            get
            {
                if (IsComplete) return 1f;
                return game != null && game.SampleCount > 0 ? Mathf.Clamp01(game.SolvedCount / (float)game.Goal) : 0f;
            }
        }

        void Update()
        {
            if (IsComplete) return;

            if (testKey != Key.None && zone != null && zone.PlayerInside && Keyboard.current != null && Keyboard.current[testKey].wasPressedThisFrame)
            {
                if (game != null) game.ForceGoal();
                MarkComplete();
                return;
            }

            if (game != null && game.GoalReached) MarkComplete();
        }
    }
}
