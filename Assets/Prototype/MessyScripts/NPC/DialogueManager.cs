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

        //Debug.Log("StartDialogue called. npc: " + npc + " | npcNameText: " + npcNameText);

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
        Debug.Log("Choice selected: " + choice.choiceText + " | action: " + choice.action + " | actionParam: " + choice.actionParam);

        // set conversation key first if this choice advances state
        if (!string.IsNullOrEmpty(choice.setsConversationKey))
        {
            NPCRuntimeState npcState = DrawingManager.Instance.GetNPCState(currentNPC);
            npcState.currentConversationKey = choice.setsConversationKey;
        }

        // handle the choice action
        switch (choice.action)
        {
            case ChoiceAction.OpenDrawingQuest:
                OpenDrawingQuest(choice.actionParam);
                return;

            case ChoiceAction.TriggerQuestCheck:
                TryCompleteQuest(choice.actionParam);
                return;

            case ChoiceAction.None:
            default:
                break;
        }

        // handle conversation flow
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

    void OpenDrawingQuest(string questId)
    {
        Debug.Log("OpenDrawingQuest called with questId: " + questId);

        Quest quest = QuestManager.Instance.GetQuest(questId);

        if (quest == null)
        {
            Debug.Log("Quest not found: " + questId);
            EndDialogue();
            return;
        }

        QuestManager.Instance.ActivateQuest(questId);
        DrawingManager.Instance.SetPendingTag(quest.requiredTag);

        if (quest.questType == QuestType.CreateNPC)
        {
            DrawingManager.Instance.drawingForNPC = quest.npcName; // get the name of the npc you are drawing for
        }
        else
        {
            DrawingManager.Instance.drawingForNPC = ""; //clear so it doesn't store
        }

        EndDialogue();
        GameModeManager.Instance.SetGameMode(GameMode.Drawing);
    }

    void TryCompleteQuest(string questId)
    {
        Quest quest = QuestManager.Instance.GetQuest(questId);

        if (quest == null)
        {
            Debug.Log("Quest not found: " + questId);
            EndDialogue();
            return;
        }
        
        Debug.Log("Required tag: " + quest.requiredTag);
        Debug.Log("Placed drawings count: " + DrawingManager.Instance.placedDrawings.Count);
        foreach (var p in DrawingManager.Instance.placedDrawings)
        {
            Debug.Log("Placed tag: '" + p.tag + "' | Required: '" + quest.requiredTag + "' | Match: " + (p.tag == quest.requiredTag));
        }

        if (QuestManager.Instance.IsQuestComplete(questId))
        {
            Debug.Log("Quest already complete: " + questId);
            EndDialogue();
            return;
        }

        float proximityRadius = 10f;

        Debug.Log("Checking for tag: " + quest.requiredTag);
        Debug.Log("Placed drawings count: " + DrawingManager.Instance.placedDrawings.Count);

        foreach (var p in DrawingManager.Instance.placedDrawings)
        {
            Debug.Log("Placed: " + p.tag + " at " + p.worldPosition +
                      " | distance: " + Vector3.Distance(p.worldPosition, currentNPCPosition));
        }

        bool foundNearby = DrawingManager.Instance.placedDrawings.Exists(p =>
            p.tag == quest.requiredTag &&
            Vector3.Distance(p.worldPosition, currentNPCPosition) <= proximityRadius
        );

        Debug.Log("Found nearby: " + foundNearby);

        if (foundNearby)
        {
            QuestManager.Instance.CompleteQuest(questId);

            NPCRuntimeState npcState = DrawingManager.Instance.GetNPCState(currentNPC);
            npcState.currentConversationKey = quest.setConversationKey;

            Debug.Log("Quest complete: " + questId);
            ShowLine(currentNPC.GetConversation(quest.setConversationKey));
        }
        else
        {
            Debug.Log("Drawing not found nearby.");
            EndDialogue();
        }
    }

    void EndDialogue()
    {
        Debug.Log("Ending dialogue");
        dialogueCanvas.SetActive(false);
    }
}