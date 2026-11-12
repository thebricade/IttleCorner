using System;
using System.Collections.Generic;
using UnityEngine;

namespace InkFlip
{
    // What the player is allowed to do. Each room (RoomZone) picks a set of these; the free-play room allows all.
    [Flags]
    public enum Ability
    {
        None = 0,
        InkBlaster = 1 << 0,
        PaintBrush = 1 << 1,
        Pencil = 1 << 2,
        Glove = 1 << 3,
        Sculpt = 1 << 4,
        Shape = 1 << 5,
        All = ~0,
    }

    // The current set of abilities, decided by whichever room the player is standing in.
    // Nested or overlapping zones work: the one entered most recently wins, and leaving it falls back
    // to the one before. Outside every zone the player can do everything.
    public static class Abilities
    {
        static readonly List<RoomZone> activeZones = new List<RoomZone>();

        // abilities handed to the player (by a character, say) that work in every room
        static Ability granted = Ability.None;

        // abilities that have to be earned: they don't work anywhere, whatever a room allows, until Grant gives them.
        // The glove is earned in the Glove Room (by decorating it and picking it up); the ink blaster in the Hand Mix Studio
        // (by mixing three colours).
        static Ability locked = Ability.Glove | Ability.InkBlaster;

        // fired whenever the allowed set may have changed
        public static event Action Changed;

        public static Ability Current => ((activeZones.Count > 0 ? activeZones[activeZones.Count - 1].abilities : Ability.All) & ~locked) | granted;

        // true while the player hasn't earned this ability yet
        public static bool IsLocked(Ability ability)
        {
            return (locked & ability) != 0;
        }
        public static RoomZone CurrentZone => activeZones.Count > 0 ? activeZones[activeZones.Count - 1] : null;

        // true if every ability in 'needed' is allowed (None is always allowed)
        public static bool Can(Ability needed)
        {
            return needed == Ability.None || (Current & needed) == needed;
        }

        // give the player an ability in every room from now on
        public static void Grant(Ability ability)
        {
            if (ability == Ability.None || (granted & ability) == ability) return;
            granted |= ability;
            locked &= ~ability;
            Changed?.Invoke();
        }

        public static void Enter(RoomZone zone)
        {
            if (zone == null || activeZones.Contains(zone)) return;
            activeZones.Add(zone);
            Changed?.Invoke();
        }

        public static void Exit(RoomZone zone)
        {
            if (zone == null || !activeZones.Remove(zone)) return;
            Changed?.Invoke();
        }

        // static state survives play sessions when domain reload is switched off, so start each one clean
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            activeZones.Clear();
            granted = Ability.None;
            locked = Ability.Glove | Ability.InkBlaster;
            Changed = null;
        }
    }
}
