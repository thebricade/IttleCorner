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
    private float proximityRadius = 10f;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
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
        Debug.Log("Choice selected: " + choice.choiceText + " | action: " + choice.action + " | actionParam: " + choice.actionParam);

        if (!string.IsNullOrEmpty(choice.setsConversationKey))
        {
            NPCRuntimeState npcState = DrawingManager.Instance.GetNPCState(currentNPC);
            npcState.currentConversationKey = choice.setsConversationKey;
        }

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
            DrawingManager.Instance.drawingForNPC = quest.npcName;
        }
        else
        {
            DrawingManager.Instance.drawingForNPC = "";
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

        if (QuestManager.Instance.IsQuestComplete(questId))
        {
            Debug.Log("Quest already complete: " + questId);
            EndDialogue();
            return;
        }

        bool foundNearby = DrawingManager.Instance.placedDrawings.Exists(p =>
            p.tag == quest.requiredTag &&
            Vector3.Distance(p.worldPosition, currentNPCPosition) <= proximityRadius
        );

        if (!foundNearby)
        {
            Debug.Log("Drawing not found nearby.");
            EndDialogue();
            return;
        }

        switch (quest.questType)
        {
            case QuestType.DrawSomething:
                QuestManager.Instance.CompleteQuest(questId);
                NPCRuntimeState state = DrawingManager.Instance.GetNPCState(currentNPC);
                state.currentConversationKey = quest.setConversationKey;
                ShowLine(currentNPC.GetConversation(quest.setConversationKey));
                break;

            case QuestType.IteratedDraw:
                QuestManager.Instance.IncrementQuestAttempts(questId);
                int attempts = QuestManager.Instance.GetQuestAttempts(questId);

                Debug.Log("Attempt " + attempts + " of " + quest.requiredIterations + " for quest: " + questId);

                if (attempts < quest.requiredIterations)
                {
                    NPCRuntimeState npcState = DrawingManager.Instance.GetNPCState(currentNPC);
                    npcState.currentConversationKey = quest.requiredTag.ToLower() + "_attempt_" + attempts;
                    ShowLine(currentNPC.GetConversation(npcState.currentConversationKey));
                }
                else
                {
                    EndDialogue();
                   // SelectionScreenManager.Instance.Show(quest.requiredTag, questId);
                }
                break;
        }
    }

    void EndDialogue()
    {
        Debug.Log("Ending dialogue");
        dialogueCanvas.SetActive(false);
    }
}