using System.Collections.Generic;
using UnityEngine;

namespace InkFlip
{
    // Put this on a character to make them talkable: walk up, look at them, press F (or tap the prompt).
    // The DialogueRunner finds every NPCTalker and handles the prompt and the conversation.
    //
    // To add a character: write their lines in StreamingAssets/Dialogue.csv, run Tools > Import Game Content,
    // then drag the new asset from Assets/Data/NPCs into the NPC slot here.
    public class NPCTalker : MonoBehaviour
    {
        [Tooltip("The character's conversations (made by Tools > Import Game Content).")]
        public NPCData npc;
        [Tooltip("How close the player has to be.")]
        public float talkRange = 2.6f;
        [Tooltip("How directly the player has to be looking at them (1 = dead centre, 0 = sideways).")]
        [Range(-1f, 1f)] public float facingNeeded = 0.35f;
        [Tooltip("Where the player's camera is pointed when talking, e.g. the character's face. Defaults to the character.")]
        public Transform lookPoint;

        public static readonly List<NPCTalker> All = new List<NPCTalker>();

        public string DisplayName => npc != null ? npc.npcName : name;
        public Vector3 Point => lookPoint != null ? lookPoint.position : transform.position + Vector3.up * 0.6f;

        void OnEnable()
        {
            if (!All.Contains(this)) All.Add(this);
        }

        void OnDisable()
        {
            All.Remove(this);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, talkRange);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            All.Clear();
        }
    }
}
