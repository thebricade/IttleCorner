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
      DialogueManager.Instance.StartDialogue(npcData);
   }
}
