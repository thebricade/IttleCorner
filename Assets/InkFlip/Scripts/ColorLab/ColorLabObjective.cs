using UnityEngine;
using UnityEngine.InputSystem;

namespace InkFlip
{
    // The Colour Room is finished when every sample in the Colour Lab has been matched.
    public class ColorLabObjective : RoomObjective
    {
        public ColorLabGame lab;
        public RoomZone zone;
        [Tooltip("Press this key inside the room to finish it while testing.")]
        public Key testKey = Key.K;

        public override float Progress
        {
            get
            {
                if (IsComplete) return 1f;
                return lab != null && lab.SampleCount > 0 ? lab.SolvedCount / (float)lab.SampleCount : 0f;
            }
        }

        void Update()
        {
            if (IsComplete) return;

            if (testKey != Key.None && zone != null && zone.PlayerInside && Keyboard.current != null && Keyboard.current[testKey].wasPressedThisFrame)
            {
                MarkComplete();
                return;
            }

            if (lab != null && lab.AllSolved) MarkComplete();
        }
    }
}
