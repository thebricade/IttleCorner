using UnityEngine;
using System.Collections.Generic;

public enum ChoiceAction
{
    None,
    OpenDrawingQuest,
    TriggerQuestCheck
}

[System.Serializable]
public class DialogueChoice
{
    public string choiceText;
    public bool endsConversation;
    public string setsConversationKey;
    public ChoiceAction action;
    public string actionParam;
    public DialogueLine nextLine;
}

[System.Serializable]
public class DialogueLine
{
    public string npcText;
    public List<DialogueChoice> choices;
}
