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

    void Awake()
    {
        Instance = this;
    }

    public void StartDialogue(NPCData npc)
    {
        currentNPC = npc;
        ShowLine(npc.startingLine);
        dialogueCanvas.SetActive(true);
    }

    void ShowLine(DialogueLine line)
    {
        currentLine = line;
        dialogueText.text = line.npcText;
        npcNameText.text = currentNPC.npcName;

        // clear old choice buttons
        foreach (Transform child in choiceButtonParent)
        {
            Destroy(child.gameObject);
        }

        // create a button for each choice
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
        }

        if (choice.endsConversation)
        {
            EndDialogue();
        }
        else
        {
            ShowLine(choice.nextLine);
        }
    }

    void TryCompleteRequest()
    {
        if (!currentNPC.hasRequest || currentNPC.requestComplete)
        {
            return;
        }

        bool hasDrawing = DrawingManager.Instance.savedDrawings.Exists(
            d => d.drawingName == currentNPC.requiredDrawingName
        );

        if (hasDrawing)
        {
            currentNPC.requestComplete = true;
            Debug.Log("Request complete for " + currentNPC.npcName);
        }
        else
        {
            Debug.Log("Player doesn't have the required drawing yet.");
        }
    }

    void EndDialogue()
    {
        Debug.Log("Ending dialogue");
        dialogueCanvas.SetActive(false);
    }
}