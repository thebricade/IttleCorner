using UnityEngine;
using System.Collections.Generic;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance;
    public List<Quest> allQuests = new List<Quest>();
    
    private List<string> activeQuests = new List<string>();
    private List<string> completedQuests = new List<string>();
    private Dictionary<string, int> questAttempts = new Dictionary<string, int>();


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
        if (!completedQuests.Contains(questId))
        {
            completedQuests.Add(questId);
            activeQuests.Remove(questId);
            Debug.Log("Quest completed: " + questId);
            NPCQueue.Instance.OnQuestCompleted(questId);
        }
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
    public int GetQuestAttempts(string questId)
    {
        return questAttempts.ContainsKey(questId) ? questAttempts[questId] : 0;
    }

    public void IncrementQuestAttempts(string questId)
    {
        if (!questAttempts.ContainsKey(questId))
            questAttempts[questId] = 0;
        questAttempts[questId]++;
        Debug.Log("Quest attempts for " + questId + ": " + questAttempts[questId]);
    }
    
}