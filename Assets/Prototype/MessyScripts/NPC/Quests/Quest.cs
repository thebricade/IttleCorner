using UnityEngine;



//this is if we eventually have more quests types
public enum QuestType
{
    DrawSomething,
    CreateNPC,
    IteratedDraw,
}

public enum QuestRewardType
{
    None,
    BecomeSky,
}


[CreateAssetMenu(fileName = "New Quest", menuName = "Game/Quest")]
public class Quest : ScriptableObject
{
    public string questId;
    public string displayName;
    public string npcName;
    public QuestType questType;
    public string requiredTag;
    public string setConversationKey;
    public int requiredIterations = 3;
    public QuestRewardType rewardType;
    
}
