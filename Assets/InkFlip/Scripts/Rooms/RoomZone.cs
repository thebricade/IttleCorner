using System;
using System.Collections.Generic;
using UnityEngine;

namespace InkFlip
{
    // A room, as a box in the world. While the player is inside, the room decides which abilities they have.
    // Everything lives in one scene, so what you paint and place in a room is still there when you come back.
    //
    // To make a room: add this to an empty object, size the box over the room, tick the abilities allowed in there,
    // and optionally add a RoomObjective. The entry room uses All abilities, which is free play.
    public class RoomZone : MonoBehaviour
    {
        [Tooltip("Unique id, used to remember whether the room is finished.")]
        public string roomId = "room";
        [Tooltip("The room's name, shown as the big title when you walk in.")]
        public string displayName = "Room";

        [Header("Area")]
        [Tooltip("Size of the box, in the zone's own space.")]
        public Vector3 size = new Vector3(8f, 4f, 8f);
        [Tooltip("Centre of the box, in the zone's own space.")]
        public Vector3 center = new Vector3(0f, 2f, 0f);

        [Header("Rules")]
        public Ability abilities = Ability.All;
        [Tooltip("The goal of this room's mini-game. Leave empty for a free-play room.")]
        public RoomObjective objective;
        [Tooltip("Does finishing this room count toward unlocking the final area?")]
        public bool countsForFinalArea = true;

        [Header("Title card")]
        public bool showTitleCard = true;
        [Tooltip("A smaller line under the title, e.g. what to do in here. Leave empty for none.")]
        public string subtitle;
        [Tooltip("Colour of the card's accent line or badge.")]
        public Color titleAccent = Color.white;
        [Tooltip("Only show the title the first time the player enters.")]
        public bool showTitleOnlyFirstTime = false;
        [Tooltip("A different title card for this room. Leave empty to use the scene's default card.")]
        public RoomTitleCard customTitleCard;

        public Transform player;

        // remembered by the title card for 'only the first time'
        [NonSerialized] public bool titleShown;

        public bool PlayerInside { get; private set; }
        public bool IsComplete => GameProgress.IsComplete(roomId);

        public static event Action<RoomZone> Entered;
        public static event Action<RoomZone> Exited;
        // fired when this room's objective is met
        public static event Action<RoomZone> Completed;

        // every active room, so the final gate and HUD can count them
        static readonly List<RoomZone> all = new List<RoomZone>();

        // rooms that must be finished to open the final area, and how many of those are done
        public static int CountedTotal
        {
            get
            {
                int count = 0;
                foreach (RoomZone zone in all) if (zone.countsForFinalArea) count++;
                return count;
            }
        }

        public static int CountedComplete
        {
            get
            {
                int count = 0;
                foreach (RoomZone zone in all) if (zone.countsForFinalArea && zone.IsComplete) count++;
                return count;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            all.Clear();
            Entered = Exited = Completed = null;
        }

        // finish a room by its id (used by conversations). Returns false if there's no such room.
        public static bool CompleteRoom(string id)
        {
            foreach (RoomZone zone in all)
            {
                if (zone.roomId != id) continue;
                zone.CompleteNow();
                return true;
            }
            return false;
        }

        public void CompleteNow()
        {
            if (IsComplete) return;
            if (objective != null)
            {
                objective.ForceComplete(); // fires Completed, which records it and shows the message
                return;
            }
            GameProgress.MarkComplete(roomId);
            Completed?.Invoke(this);
        }

        void OnEnable()
        {
            if (!all.Contains(this)) all.Add(this);
            if (objective != null) objective.Completed += OnObjectiveCompleted;
        }

        void OnDisable()
        {
            all.Remove(this);
            if (objective != null) objective.Completed -= OnObjectiveCompleted;
            if (PlayerInside) Leave();
        }

        void Update()
        {
            if (player == null)
            {
                var controller = FindAnyObjectByType<FirstPersonController>();
                if (controller == null) return;
                player = controller.transform;
            }

            // test the player's middle, not their feet, so standing in a doorway doesn't flicker
            bool inside = Contains(player.position + Vector3.up * 0.9f);
            if (inside == PlayerInside) return;

            if (inside) Enter();
            else Leave();
        }

        public bool Contains(Vector3 worldPoint)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint) - center;
            Vector3 half = size * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }

        void Enter()
        {
            PlayerInside = true;
            Abilities.Enter(this);
            Entered?.Invoke(this);
        }

        void Leave()
        {
            PlayerInside = false;
            Abilities.Exit(this);
            Exited?.Invoke(this);
        }

        void OnObjectiveCompleted(RoomObjective finished)
        {
            GameProgress.MarkComplete(roomId);
            Completed?.Invoke(this);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(center, size);
        }
    }
}
