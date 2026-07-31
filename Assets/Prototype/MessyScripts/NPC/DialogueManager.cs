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

        Debug.Log("StartDialogue called. npc: " + npc + " | npcNameText: " + npcNameText);

        NPCRuntimeState npcState = DrawingManager.Instance.GetNPCState(npc);

        if (npcState.requestComplete && npc.fulfilledLine != null
                                    && !string.IsNullOrEmpty(npc.fulfilledLine.npcText))
        {
            ShowLine(npc.fulfilledLine);
        }
        else
        {
            ShowLine(npc.startingLine);
        }

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

        if (npcState.requestComplete)
        {
            Debug.Log("Request already complete");
            return;
        }

        float proximityRadius = 1000f;

        bool foundNearby = DrawingManager.Instance.placedDrawings.Exists(p =>
            p.tag == currentNPC.requiredDrawingTag &&
            Vector3.Distance(p.worldPosition, currentNPCPosition) <= proximityRadius
        );

        if (foundNearby)
        {
            npcState.requestComplete = true;
            Debug.Log("Request complete for " + currentNPC.npcName);
            ShowLine(currentNPC.fulfilledLine);
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