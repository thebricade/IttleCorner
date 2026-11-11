using UnityEngine;
using System.Collections.Generic;

public enum ChoiceAction
{
    None,
    OpenDrawingQuest,
    TriggerQuestCheck,
    CompleteRoom,   // 3D rooms: marks the room whose id is in actionParam as finished
    GrantAbility,   // 3D rooms: gives the player the ability named in actionParam (e.g. InkBlaster) everywhere
    StartGame       // 3D rooms: starts the mini-game whose id is in actionParam (e.g. line_race)
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
    public string nextLineKey; 
}
