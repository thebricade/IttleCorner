using UnityEngine;
using System.Collections.Generic;

public class NPCManager : MonoBehaviour
{
    public static NPCManager Instance;
    public Dictionary<string, GameObject> npcDictionary = new Dictionary<string, GameObject>();
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    
    //need a function that adds an npc to the dictionary
    public void AddNPC(string npcName, GameObject npc)
    {
        if (!npcDictionary.ContainsKey(npcName))
        {
            npcDictionary.Add(npcName, npc);
            Debug.Log("NPC registered: " + npcName);
        }
        else
        {
            Debug.LogWarning("NPC already registered: " + npcName);
        }
    }
    
    public GameObject FindNPC(string npcName)
    {
        npcDictionary.TryGetValue(npcName, out GameObject result);
        return result;
    }
}
