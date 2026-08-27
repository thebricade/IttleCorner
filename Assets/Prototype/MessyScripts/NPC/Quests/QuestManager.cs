using UnityEngine;
using System.Collections.Generic;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance;
    public List<Quest> allQuests = new List<Quest>();
    
    private List<string> activeQuests = new List<string>();
    private List<string> completedQuests = new List<string>();

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this; 
    }

    public Quest GetQuest(string questId)
    {
        return allQuests.Find(q => q.questId == questId);
    }

    public void ActivateQuest(string questId)
    {
        if (activeQuests.Contains(questId) || completedQuests.Contains(questId))
        {
            return;
        }
        activeQuests.Add(questId);
        Debug.Log("Quest activated: " + questId);
    }

    public void CompleteQuest(string questId)
    {
        activeQuests.Remove(questId);
        completedQuests.Add(questId);
        Debug.Log("Quest completed: " + questId);
    }

    public bool IsQuestActive(string questId)
    {
        return activeQuests.Contains(questId);
    }

    public bool IsQuestComplete(string questId)
    {
        return completedQuests.Contains(questId);
    }

    public Quest GetActiveQuestForNPC(string npcName)
    {
        foreach (string questId in activeQuests)
        {
            Quest quest = GetQuest(questId);
            if (quest != null && quest.npcName == npcName)
            {
                return quest;
            }
        }
        return null;
    }
}