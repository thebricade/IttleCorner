using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class DialogueChoice
{
    public string choiceText;
    public bool endsConversation;
    public bool opensDrawingScreen;
    public string autoTag;
    public string setsConversationKey;
    public DialogueLine nextLine;
    public bool triggersRequestCheck;
}

[System.Serializable]
public class DialogueLine
{
    public string npcText;
    public List<DialogueChoice> choices;
}
