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
    
    public AudioSource audioSource;
    private int currentVoiceIndex = 0; 
    
    public GameObject eraserButton;
    public GameObject DrawboardEraser;
    public SpriteRenderer walrusHatDrawingRenderer; 
    private string pendingOneLineQuestId = "";

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
        currentVoiceIndex = 0;

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
        PlayVoice(currentNPC);

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
            Debug.Log("Next line found: " + (nextLine != null));
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
        if (!string.IsNullOrEmpty(choice.setsConversationKey))
        {
            NPCRuntimeState npcState = DrawingManager.Instance.GetNPCState(currentNPC);
            npcState.currentConversationKey = choice.setsConversationKey;
            CheckConversationTriggers(choice.setsConversationKey); // check immediately after setting
        }
        
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

    // find the DrawingPad in the scene
    DrawingPad pad = FindObjectOfType<DrawingPad>();

    switch (quest.questType)
    {
        case QuestType.CreateNPC:
            DrawingManager.Instance.drawingForNPC = quest.npcName;
            break;

        case QuestType.ContinueDrawing:
            // load existing player drawing by tag
            Drawing existing = DrawingManager.Instance.GetSavedDrawing(quest.requiredTag);
            if (existing != null && pad != null)
                pad.LoadDrawing(existing.texture);
            DrawingManager.Instance.drawingForNPC = "";
            break;

        case QuestType.NPCStartedDrawing:
        case QuestType.NPCIterativeDrawing:
            // load game-authored preset drawing by tag
            Drawing preset = DrawingManager.Instance.GetGameDrawing(quest.requiredTag);
            if (preset != null && pad != null)
                pad.LoadDrawing(preset.texture);
            DrawingManager.Instance.drawingForNPC = "";
            break;

        case QuestType.SequentialPrompt:
            // load existing drawing if one exists, otherwise start fresh
            Drawing previous = DrawingManager.Instance.GetSavedDrawing(quest.requiredTag);
            if (previous != null && pad != null)
                pad.LoadDrawing(previous.texture);
            DrawingManager.Instance.drawingForNPC = "";
            break;

        case QuestType.OneLineDrawing:
            if (pad != null)
                pad.SetOneLineMode(true);
            DrawingManager.Instance.drawingForNPC = "";
            StartOneLineQuest(questId);
            break;  

        default:
            DrawingManager.Instance.drawingForNPC = "";
            break;
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
        case QuestType.ContinueDrawing:
        case QuestType.NPCStartedDrawing:
        case QuestType.OneLineDrawing:
            // all these just need a placed drawing with matching tag
            questConditionMet = DrawingManager.Instance.placedDrawings.Exists(p =>
                p.tag == quest.requiredTag &&
                Vector3.Distance(p.worldPosition, currentNPCPosition) <= proximityRadius
            );
            break;

        case QuestType.IteratedDraw:
        case QuestType.NPCIterativeDrawing:
            // check saved drawings count meets required iterations
            int count = DrawingManager.Instance.savedDrawings.FindAll(
                d => d.drawingName == quest.requiredTag
            ).Count;
            questConditionMet = count >= quest.requiredIterations;
            break;

        case QuestType.SequentialPrompt:
            // just needs one saved drawing with the tag
            questConditionMet = DrawingManager.Instance.savedDrawings.Exists(
                d => d.drawingName == quest.requiredTag
            );
            break;
    }

    if (!questConditionMet) { Debug.Log("Quest condition not met."); EndDialogue(); return; }

    switch (quest.questType)
    {
        case QuestType.DrawSomething:
        case QuestType.ContinueDrawing:
        case QuestType.NPCStartedDrawing:
        case QuestType.OneLineDrawing:
            QuestManager.Instance.CompleteQuest(questId);
            if (quest.currencyReward > 0)
                Wallet.Instance.AddCurrency(quest.currencyReward);
            NPCRuntimeState state = DrawingManager.Instance.GetNPCState(currentNPC);
            state.currentConversationKey = quest.setConversationKey;
            ShowLine(currentNPC.GetConversation(quest.setConversationKey));
            break;

        case QuestType.IteratedDraw:
        case QuestType.NPCIterativeDrawing:
            EndDialogue();
            selectionScreen.Show(quest.requiredTag, questId);
            break;

        case QuestType.SequentialPrompt:
            QuestManager.Instance.IncrementQuestAttempts(questId);
            int attempts = QuestManager.Instance.GetQuestAttempts(questId);

            if (attempts >= quest.requiredIterations)
            {
                QuestManager.Instance.CompleteQuest(questId);
                if (quest.currencyReward > 0)
                    Wallet.Instance.AddCurrency(quest.currencyReward);
                NPCRuntimeState seqState = DrawingManager.Instance.GetNPCState(currentNPC);
                seqState.currentConversationKey = quest.setConversationKey;
                ShowLine(currentNPC.GetConversation(quest.setConversationKey));
            }
            else
            {
                // advance to next prompt conversation key
                NPCRuntimeState npcState = DrawingManager.Instance.GetNPCState(currentNPC);
                npcState.currentConversationKey = quest.requiredTag.ToLower() + "_prompt_" + attempts;
                ShowLine(currentNPC.GetConversation(npcState.currentConversationKey));
            }
            break;
    }
}

    void CheckConversationTriggers(string key)
    {
        Debug.Log("Checking triggers for key: " + key);
    
        if (key == "ittle_erasing_c")
        {
            DrawboardEraser.SetActive(true);
            UnlockPopup.Instance.Show(
                "New Tool: Eraser!",
                "You can now erase parts of your drawings.",
                new GameObject[] { eraserButton }
            );
        }
        if (key == "ittle_first_placement_0")
        {
            Debug.Log("found conversation for giving hat");
            ApplyDrawingToWalrusHat();
        }

    }
    
    void ApplyDrawingToWalrusHat()
    {
        if (DrawingManager.Instance.savedDrawings.Count == 0) return;

        Drawing introDrawing = DrawingManager.Instance.savedDrawings[0];

        Sprite drawingSprite = Sprite.Create(
            introDrawing.texture,
            new Rect(0, 0, introDrawing.texture.width, introDrawing.texture.height),
            new Vector2(0.5f, 0.5f),
            100f
        );

        walrusHatDrawingRenderer.sprite = drawingSprite;

        // get the SpriteMask on the parent and match its size
        SpriteMask mask = walrusHatDrawingRenderer.GetComponentInParent<SpriteMask>();
        if (mask != null)
        {
            // scale the drawing renderer to match the mask's world size
            Vector3 maskSize = mask.bounds.size;
            Vector3 spriteSize = walrusHatDrawingRenderer.bounds.size;

            if (spriteSize.x > 0 && spriteSize.y > 0)
            {
                walrusHatDrawingRenderer.transform.localScale = new Vector3(
                    maskSize.x / spriteSize.x,
                    maskSize.y / spriteSize.y,
                    1f
                );
            }
        }
    }
    
    void PlayVoice(NPCData npc)
    {
        if (npc.voiceClips == null || npc.voiceClips.Length == 0) return;
        if (audioSource == null) return;

        audioSource.clip = npc.voiceClips[currentVoiceIndex];
        audioSource.Play();

        currentVoiceIndex = (currentVoiceIndex + 1) % npc.voiceClips.Length;
    }

    public void StartOneLineQuest(string questId)
    {
        pendingOneLineQuestId = questId;
        DrawingPad pad = FindObjectOfType<DrawingPad>();
        if (pad != null)
        {
            pad.onOneLineComplete = () => OnOneLineComplete(pad);
        }
    }

    void OnOneLineComplete(DrawingPad pad)
    {
        // auto save the drawing
        Texture2D snapshot = pad.GetCurrentTextureCopy();
        Texture2D cropped = pad.CropToContent(snapshot);
    
        Quest quest = QuestManager.Instance.GetQuest(pendingOneLineQuestId);
        string tag = quest != null ? quest.requiredTag : "OneLine";
    
        DrawingManager.Instance.SaveDrawing(cropped, tag);
        DrawingManager.Instance.pendingDrawingTag = "";
        DrawingManager.Instance.drawingForNPC = "";

        // disable one line mode
        pad.SetOneLineMode(false);
        pad.onOneLineComplete = null;

        // switch back to explore
        GameModeManager.Instance.SetGameMode(GameMode.Explore);
    
        pendingOneLineQuestId = "";
    }
    
    void EndDialogue()
    {
        Debug.Log("Ending dialogue");
        continueButton.SetActive(false);
        dialogueCanvas.SetActive(false);
        
    }
}