using UnityEngine;

public class NPCInteractable : MonoBehaviour
{
   public NPCData npcData;

   void Start()
   {
      NPCManager.Instance.AddNPC(npcData.npcName, gameObject);
   }
   
   private void OnMouseDown()
   {
      if (GameModeManager.Instance.currentMode != GameMode.Explore) return;
      if (DialogueManager.Instance.dialogueCanvas.activeSelf) return;

      DialogueManager.Instance.StartDialogue(npcData, transform.position);
   }
}
