using System;
using UnityEngine;

namespace InkFlip
{
    // The goal of a room's mini-game. Make a subclass per mechanic (paint a wall, match a colour, sculpt a shape...)
    // and call MarkComplete() when the player has done it. A RoomZone listens and records the room as finished.
    public abstract class RoomObjective : MonoBehaviour
    {
        [Tooltip("Short name shown to the player, e.g. 'Cover the wall'.")]
        public string title = "Objective";
        [TextArea] public string description;

        public bool IsComplete { get; private set; }

        // 0-1, for progress bars
        public virtual float Progress => IsComplete ? 1f : 0f;

        public event Action<RoomObjective> Completed;

        protected void MarkComplete()
        {
            if (IsComplete) return;
            IsComplete = true;
            Completed?.Invoke(this);
        }

        // finish the objective from outside, e.g. a conversation. Subclasses can also update their visuals.
        public virtual void ForceComplete()
        {
            MarkComplete();
        }

        // used by the room-reset feature: go back to not-done (the room's contents are reset separately)
        public virtual void ResetObjective()
        {
            IsComplete = false;
        }
    }
}
