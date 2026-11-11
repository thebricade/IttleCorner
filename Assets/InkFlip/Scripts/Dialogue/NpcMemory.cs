using System.Collections.Generic;
using UnityEngine;

namespace InkFlip
{
    // Where each character is in their conversation. A character remembers the key you reached,
    // so talking to them again picks up from there (replaces the 2D game's DrawingManager.GetNPCState).
    public static class NpcMemory
    {
        static readonly Dictionary<string, string> keys = new Dictionary<string, string>();

        public static string GetKey(NPCData npc)
        {
            if (npc != null && keys.TryGetValue(npc.npcName, out string key)) return key;
            return npc != null && !string.IsNullOrEmpty(npc.defaultConversationKey) ? npc.defaultConversationKey : "default";
        }

        public static void SetKey(NPCData npc, string key)
        {
            if (npc == null || string.IsNullOrEmpty(key)) return;
            keys[npc.npcName] = key;
        }

        // back to the start of everyone's conversations (for the later room-reset feature)
        public static void ResetAll()
        {
            keys.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            keys.Clear();
        }
    }
}
