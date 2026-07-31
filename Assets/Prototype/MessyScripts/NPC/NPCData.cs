using UnityEngine;

[CreateAssetMenu(fileName = "New NPC", menuName = "Game/NPC Data")]
public class NPCData : ScriptableObject
{
    public string npcName;
    public DialogueLine startingLine;

    [Header("Request")]
    public bool hasRequest;
    public string requestDescription;
    public string requiredDrawingTag;
    public DialogueLine fulfilledLine;
}