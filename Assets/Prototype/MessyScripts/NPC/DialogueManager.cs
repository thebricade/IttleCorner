using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance;

    public GameObject dialogueCanvas;
    public TMP_Text npcNameText;
    public TMP_Text dialogueText;
    public Transform choiceButtonParent;
    public GameObject choiceButtonPrefab;

    private NPCData currentNPC;
    private DialogueLine currentLine;
    private Vector3 currentNPCPosition;

    void Awake()
    {
        Instance = this;
    }

    public void StartDialogue(NPCData npc, Vector3 npcPosition)
    {
        currentNPC = npc;
        currentNPCPosition = npcPosition;
        
        NPCRuntimeState npcState = DrawingManager.Instance.GetNPCState(npc);
        DialogueLine conversation = npc.GetConversation(npcState.currentConversationKey);

        ShowLine(conversation);
        dialogueCanvas.SetActive(true);
    }

    void ShowLine(DialogueLine line)
    {
        currentLine = line;
        dialogueText.text = line.npcText;
        npcNameText.text = currentNPC.npcName;

        foreach (Transform child in choiceButtonParent)
        {
            Destroy(child.gameObject);
        }

        foreach (DialogueChoice choice in line.choices)
        {
            GameObject buttonObj = Instantiate(choiceButtonPrefab, choiceButtonParent);
            TMP_Text buttonText = buttonObj.GetComponentInChildren<TMP_Text>();
            buttonText.text = choice.choiceText;

            Button button = buttonObj.GetComponent<Button>();
            button.onClick.AddListener(() => OnChoiceSelected(choice));
        }
    }

    void OnChoiceSelected(DialogueChoice choice)
    {
        if (!string.IsNullOrEmpty(choice.setsConversationKey))
        {
            NPCRuntimeState npcState = DrawingManager.Instance.GetNPCState(currentNPC);
            npcState.currentConversationKey = choice.setsConversationKey;
        }
        
        if (choice.triggersRequestCheck)
        {
            TryCompleteRequest();
            return;
        }

        if (choice.opensDrawingScreen)
        {
            OpenDrawingScreen(choice.autoTag);
            return;
        }

        if (choice.endsConversation)
        {
            EndDialogue();
        }
        else if (choice.nextLine != null && !string.IsNullOrEmpty(choice.nextLine.npcText))
        {
            ShowLine(choice.nextLine);
        }
        else
        {
            EndDialogue();
        }
    }

    void OpenDrawingScreen(string tag)
    {
        DrawingManager.Instance.SetPendingTag(tag);
        
        EndDialogue();
        GameModeManager.Instance.SetGameMode(GameMode.Drawing);
    }

    void TryCompleteRequest()
    {
        if (!currentNPC.hasRequest)
        {
            Debug.Log("NPC has no request");
            return;
        }

        NPCRuntimeState npcState = DrawingManager.Instance.GetNPCState(currentNPC);

        if (npcState.currentConversationKey == currentNPC.completionConversationKey)
        {
            Debug.Log("NPC has complete request");
            return;
        }

            Debug.Log("Checking for drawing tag: " + currentNPC.requiredDrawingTag);
            Debug.Log("Placed drawings count: " + DrawingManager.Instance.placedDrawings.Count);
            Debug.Log("NPC position: " + currentNPCPosition);

            foreach (var p in DrawingManager.Instance.placedDrawings)
            {
                Debug.Log("Placed: " + p.tag + " at " + p.worldPosition +
                          " | distance: " + Vector3.Distance(p.worldPosition, currentNPCPosition));
            }

            float proximityRadius = 10f;

            bool foundNearby = DrawingManager.Instance.placedDrawings.Exists(p =>
                p.tag == currentNPC.requiredDrawingTag &&
                Vector3.Distance(p.worldPosition, currentNPCPosition) <= proximityRadius
            );

            Debug.Log("Found nearby: " + foundNearby);

            if (foundNearby)
            {
                npcState.currentConversationKey = currentNPC.completionConversationKey;
                ShowLine(currentNPC.GetConversation(currentNPC.completionConversationKey));
            }
            else
            {
                Debug.Log("No matching drawing placed nearby.");
                EndDialogue();
            }
     
    }

    void EndDialogue()
    {
        Debug.Log("Ending dialogue");
        dialogueCanvas.SetActive(false);
    }
}