using System;
using System.Collections.Generic;
using UnityEngine;

namespace InkFlip
{
    // Which rooms are finished. Held in memory for now; the final area will ask this whether to open.
    public static class GameProgress
    {
        static readonly HashSet<string> completed = new HashSet<string>();

        public static event Action<string> RoomCompleted;

        public static bool IsComplete(string roomId) => completed.Contains(roomId);
        public static int CompletedCount => completed.Count;

        // true if every id in the list is finished
        public static bool AllComplete(IEnumerable<string> roomIds)
        {
            foreach (string id in roomIds)
            {
                if (!completed.Contains(id)) return false;
            }
            return true;
        }

        public static void MarkComplete(string roomId)
        {
            if (string.IsNullOrEmpty(roomId) || !completed.Add(roomId)) return;
            RoomCompleted?.Invoke(roomId);
        }

        // for the later "reset a room" feature and for testing
        public static void Reset(string roomId)
        {
            completed.Remove(roomId);
        }

        public static void ResetAll()
        {
            completed.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            completed.Clear();
            RoomCompleted = null;
        }
    }
}
