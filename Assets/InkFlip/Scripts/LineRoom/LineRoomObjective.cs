using UnityEngine;
using UnityEngine.InputSystem;

namespace InkFlip
{
    // The Line Room is finished when its three parts are: the room has been powered (the line-tracing panel on the wall),
    // the nanobot race has been run, and the neon line has been drawn in the dark. Wire the parts in; each can be
    // left empty to skip it. (The colouring-book pictures now hang in the main hall and are not part of this room.)
    public class LineRoomObjective : RoomObjective
    {
        public LineTracePuzzle power;
        public LineRaceGame race;
        public NeonLightsGame neonLights;
        public RoomZone zone;
        [Tooltip("Press this key inside the room to finish it while testing.")]
        public Key testKey = Key.K;

        public bool PowerDone => power == null || power.Solved;
        public bool RaceDone => race == null || race.HasRaced;
        public bool NeonDone => neonLights == null || neonLights.Solved;

        int Total => (power != null ? 1 : 0) + (race != null ? 1 : 0) + (neonLights != null ? 1 : 0);

        public override float Progress
        {
            get
            {
                if (IsComplete) return 1f;
                int total = Total;
                if (total == 0) return 0f;
                int done = (power != null && power.Solved ? 1 : 0) + (race != null && race.HasRaced ? 1 : 0) + (neonLights != null && neonLights.Solved ? 1 : 0);
                return done / (float)total;
            }
        }

        bool announcedPower, announcedRace, announcedNeon;

        void Update()
        {
            if (IsComplete) return;

            if (testKey != Key.None && zone != null && zone.PlayerInside && Keyboard.current != null && Keyboard.current[testKey].wasPressedThisFrame)
            {
                if (power != null) power.ForceSolve();
                MarkComplete();
                return;
            }

            // one short message per finished part, then the room itself
            int done = (power != null && PowerDone ? 1 : 0) + (race != null && RaceDone ? 1 : 0) + (neonLights != null && NeonDone ? 1 : 0);
            // the power message comes from the panel itself
            if (!announcedPower && power != null && power.Solved) announcedPower = true;
            if (!announcedRace && race != null && race.HasRaced) { announcedRace = true; Announce("Nanobot race", done); }
            if (!announcedNeon && neonLights != null && neonLights.Solved) { announcedNeon = true; Announce("Lights out", done); }

            if (PowerDone && RaceDone && NeonDone) MarkComplete();
        }

        void Announce(string part, int done)
        {
            int total = Total;
            if (done < total) LineRoomUI.Instance.Say($"{part} done!  ({done} / {total})", 2.5f);
        }
    }
}
