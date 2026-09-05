using UnityEngine;

[System.Serializable]
public class NPCQueueEntry
{
    public GameObject npc;
    public string unlockAfterQuestId;
}

public class NPCQueue : MonoBehaviour
{
    public static NPCQueue Instance;
    public NPCQueueEntry[] npcs;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        for (int i = 0; i < npcs.Length; i++)
            npcs[i].npc.SetActive(i == 0);
    }

    public void OnQuestCompleted(string questId)
    {
        for (int i = 1; i < npcs.Length; i++)
        {
            if (npcs[i].unlockAfterQuestId == questId)
            {
                npcs[i].npc.SetActive(true);
                Debug.Log("Activated NPC: " + npcs[i].npc.name);
            }
        }
    }
}