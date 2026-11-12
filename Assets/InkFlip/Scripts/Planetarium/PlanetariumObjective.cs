using UnityEngine;
using UnityEngine.InputSystem;

namespace InkFlip
{
    // The planetarium is finished when every planet has been coloured and no two look the same.
    public class PlanetariumObjective : RoomObjective
    {
        public PlanetariumGame game;
        public RoomZone zone;
        [Tooltip("Press this key inside the room to finish it while testing.")]
        public Key testKey = Key.K;

        public override float Progress
        {
            get
            {
                if (IsComplete) return 1f;
                return game != null && game.PlanetCount > 0 ? game.GoodCount / (float)game.PlanetCount : 0f;
            }
        }

        void Update()
        {
            if (IsComplete) return;

            if (testKey != Key.None && zone != null && zone.PlayerInside && Keyboard.current != null && Keyboard.current[testKey].wasPressedThisFrame)
            {
                if (game != null) game.ForceSolve();
                MarkComplete();
                return;
            }
            if (game != null && game.Solved) MarkComplete();
        }
    }
}
