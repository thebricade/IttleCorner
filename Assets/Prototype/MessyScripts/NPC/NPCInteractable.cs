using System;
using UnityEngine;

public class NPCInteractable : MonoBehaviour
{
   public NPCData npcData;

   private void OnMouseDown()
   {
      if (GameModeManager.Instance.currentMode != GameMode.Explore)
      {
         return;
      }

      Debug.Log("NPC clicked, npcData: " + npcData);
      Debug.Log("DialogueManager instance: " + DialogueManager.Instance);

      DialogueManager.Instance.StartDialogue(npcData, transform.position);
   }
}
