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
    public GameObject continueButton;        
    public SelectionScreen selectionScreen;

    private NPCData currentNPC;
    private DialogueLine currentLine;
    private Vector3 currentNPCPosition;
    private float proximityRadius = 10000f;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
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

        // no choices - show continue button instead
        if (line.choices == null || line.choices.Count == 0)
        {
            continueButton.SetActive(true);
            return;
        }

        continueButton.SetActive(false);

        foreach (DialogueChoice choice in line.choices)
        {
            GameObject buttonObj = Instantiate(choiceButtonPrefab, choiceButtonParent);
            TMP_Text buttonText = buttonObj.GetComponentInChildren<TMP_Text>();
            buttonText.text = choice.choiceText;

            Button button = buttonObj.GetComponent<Button>();
            button.onClick.AddListener(() => OnChoiceSelected(choice));
        }
    }

    public void OnContinueClicked() 
    {
        continueButton.SetActive(false);

        if (!string.IsNullOrEmpty(currentLine.nextLineKey))
        {
            DialogueLine nextLine = currentNPC.GetConversation(currentLine.nextLineKey);
            if (nextLine != null)
            {
                ShowLine(nextLine);
                return;
            }
        }
        EndDialogue();
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

        if (quest == null) { Debug.Log("Quest not found: " + questId); EndDialogue(); return; }
        if (QuestManager.Instance.IsQuestComplete(questId)) { Debug.Log("Quest already complete"); EndDialogue(); return; }

        bool questConditionMet = false;

        switch (quest.questType)
        {
            case QuestType.DrawSomething:
                questConditionMet = DrawingManager.Instance.placedDrawings.Exists(p =>
                    p.tag == quest.requiredTag &&
                    Vector3.Distance(p.worldPosition, currentNPCPosition) <= proximityRadius
                );
                break;

            case QuestType.IteratedDraw:
                questConditionMet = DrawingManager.Instance.savedDrawings.Exists(
                    d => d.drawingName == quest.requiredTag
                );
                break;
        }

        Debug.Log("Quest condition met: " + questConditionMet + " | type: " + quest.questType);

        if (!questConditionMet) { Debug.Log("Quest condition not met."); EndDialogue(); return; }

        switch (quest.questType)
        {
            case QuestType.DrawSomething:
                QuestManager.Instance.CompleteQuest(questId);

                if (quest.currencyReward > 0)
                    Wallet.Instance.AddCurrency(quest.currencyReward);

                NPCRuntimeState state = DrawingManager.Instance.GetNPCState(currentNPC);
                state.currentConversationKey = quest.setConversationKey;
                ShowLine(currentNPC.GetConversation(quest.setConversationKey));
                break;

            case QuestType.IteratedDraw:
                QuestManager.Instance.IncrementQuestAttempts(questId);
                int attempts = QuestManager.Instance.GetQuestAttempts(questId);

                Debug.Log("Attempt " + attempts + " of " + quest.requiredIterations);

                if (attempts < quest.requiredIterations)
                {
                    string attemptKey = currentNPC.npcName.ToLower() + "_" + quest.requiredTag.ToLower() + "_attempt_" + attempts;
                    Debug.Log("Looking for key: '" + attemptKey + "'");

                    DialogueLine attemptLine = currentNPC.GetConversation(attemptKey);
                    Debug.Log("Conversation found: " + (attemptLine != null));

                    NPCRuntimeState npcState = DrawingManager.Instance.GetNPCState(currentNPC);
                    npcState.currentConversationKey = attemptKey;
                    ShowLine(attemptLine);
                }
                else
                {
                    EndDialogue();
                    selectionScreen.Show(quest.requiredTag, questId);
                }
                break;
        }
    }

    void EndDialogue()
    {
        Debug.Log("Ending dialogue");
        continueButton.SetActive(false);
        dialogueCanvas.SetActive(false);
    }
}