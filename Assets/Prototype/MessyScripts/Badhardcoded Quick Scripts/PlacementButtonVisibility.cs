using UnityEngine;
using UnityEngine.UI;

public class PlacementButtonVisibility : MonoBehaviour
{
    public GameObject placementButton;

    void Update()
    {
        if (DialogueManager.Instance == null) return;
        
        bool inDialogue = DialogueManager.Instance.dialogueCanvas.activeSelf;
        placementButton.SetActive(!inDialogue);
    }
}