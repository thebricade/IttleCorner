using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class ConversationEntry
{
    public string key;
    public DialogueLine dialogueLine;
}

[CreateAssetMenu(fileName = "New NPC", menuName = "Game/NPC Data")]
public class NPCData : ScriptableObject
{
    public string npcName;
    public string defaultConversationKey = "default";
    public string completionConversationKey;

    [Header("Request")]
    public bool hasRequest;
    public string requestDescription;
    public string requiredDrawingTag;
    
    [Header("Conversations")]
    public List<ConversationEntry> conversations;

    public DialogueLine GetConversation(string key)
    {
        ConversationEntry entry = conversations.Find(c => c.key == key);

        if (entry != null)
        {
            return entry.dialogueLine;
        }
        
        //fallback
        ConversationEntry defaultEntry = conversations.Find(c => c.key == defaultConversationKey);
        return defaultEntry?.dialogueLine;
    }
}