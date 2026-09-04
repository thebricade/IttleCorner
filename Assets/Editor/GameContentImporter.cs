using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class GameContentImporter : EditorWindow 
{
   [MenuItem("Tools/Import Game Content")]
   public static void ImportAll()
   {
      ImportQuest();
      ImportDialogue(); 
      ImportConditions();
      AssetDatabase.SaveAssets();
      AssetDatabase.Refresh();
      Debug.Log("Import Complete");
   }

   static void ImportQuest()
{
    string path = Path.Combine(Application.streamingAssetsPath, "Quests.csv");
     
    if (!File.Exists(path))
    {
        Debug.LogError("Quests.csv not found at: " + path);
        return;
    }

    string[] lines = File.ReadAllLines(path);

    for (int i = 1; i < lines.Length; i++)
    {
        string line = lines[i].Trim();
        if (string.IsNullOrEmpty(line)) continue;

        string[] columns = ParseCSVLine(line);
        if (columns.Length < 6) continue;

        // read all columns first
        string questId =             columns[0].Trim();
        string displayName =         columns[1].Trim();
        string npcName =             columns[2].Trim();
        string questTypeStr =        columns[3].Trim();
        string requiredTag =         columns[4].Trim();
        string setConversationKey =  columns[5].Trim();
        string requiredIterationsStr = columns.Length > 6 ? columns[6].Trim() : "1";
        int requiredIterations = int.TryParse(requiredIterationsStr, out int result) ? result : 1;

        if (string.IsNullOrEmpty(questId)) continue;

        // find or create asset
        string assetPath = "Assets/Data/Quests/" + questId + ".asset";
        Quest quest = AssetDatabase.LoadAssetAtPath<Quest>(assetPath);

        if (quest == null)
        {
            quest = ScriptableObject.CreateInstance<Quest>();
            EnsureFolderExists("Assets/Data/Quests");
            AssetDatabase.CreateAsset(quest, assetPath);
            Debug.Log("Created quest: " + questId);
        }
        else
        {
            Debug.Log("Updated quest: " + questId);
        }

        // set all fields after quest exists
        quest.questId =            questId;
        quest.displayName =        displayName;
        quest.npcName =            npcName;
        quest.questType =          ParseQuestType(questTypeStr);
        quest.requiredTag =        requiredTag;
        quest.setConversationKey = setConversationKey;
        quest.requiredIterations = requiredIterations;

        EditorUtility.SetDirty(quest);
    }
}

   static void ImportDialogue()
{
    string path = Path.Combine(Application.streamingAssetsPath, "Dialogue.csv");

    if (!File.Exists(path))
    {
        Debug.LogError("Dialogue.csv not found at: " + path);
        return;
    }

    string[] lines = File.ReadAllLines(path);

    Dictionary<string, List<string[]>> npcRows = new Dictionary<string, List<string[]>>();

    for (int i = 1; i < lines.Length; i++)
    {
        string line = lines[i].Trim();
        if (string.IsNullOrEmpty(line)) continue;

        string[] columns = ParseCSVLine(line);
        if (columns.Length < 3) continue;

        string npcName = columns[0].Trim();
        if (string.IsNullOrEmpty(npcName)) continue;

        if (!npcRows.ContainsKey(npcName))
            npcRows[npcName] = new List<string[]>();

        npcRows[npcName].Add(columns);
    }

    foreach (var kvp in npcRows)
    {
        string npcName = kvp.Key;
        List<string[]> rows = kvp.Value;

        string assetPath = "Assets/Data/NPCs/" + npcName + ".asset";
        NPCData npcData = AssetDatabase.LoadAssetAtPath<NPCData>(assetPath);

        if (npcData == null)
        {
            npcData = ScriptableObject.CreateInstance<NPCData>();
            EnsureFolderExists("Assets/Data/NPCs");
            AssetDatabase.CreateAsset(npcData, assetPath);
            Debug.Log("Created NPC: " + npcName);
        }
        else
        {
            Debug.Log("Updated NPC: " + npcName);
        }

        npcData.npcName = npcName;
        npcData.conversations = new List<ConversationEntry>();

        Dictionary<string, ConversationEntry> entryByKey = new Dictionary<string, ConversationEntry>();

        // first pass - build all entries
        foreach (string[] columns in rows)
        {
            string conversationKey = columns.Length > 1 ? columns[1].Trim() : "";
            string npcText =         columns.Length > 2 ? columns[2].Trim() : "";

            if (string.IsNullOrEmpty(conversationKey)) continue;

            ConversationEntry entry = new ConversationEntry();
            entry.key = conversationKey;
            entry.dialogueLine = new DialogueLine();
            entry.dialogueLine.npcText = npcText;
            entry.dialogueLine.choices = new List<DialogueChoice>();

            int[] choiceStarts = { 3, 8, 13 };

            foreach (int start in choiceStarts)
            {
                if (columns.Length <= start) break;

                string choiceText = columns[start].Trim();
                if (string.IsNullOrEmpty(choiceText)) continue;

                DialogueChoice choice = new DialogueChoice();
                choice.choiceText =          choiceText;
                choice.setsConversationKey = columns.Length > start + 1 ? columns[start + 1].Trim() : "";
                choice.action =              columns.Length > start + 2 ? ParseChoiceAction(columns[start + 2].Trim()) : ChoiceAction.None;
                choice.actionParam =         columns.Length > start + 3 ? columns[start + 3].Trim() : "";
                choice.endsConversation =    columns.Length > start + 4 ? columns[start + 4].Trim().ToUpper() == "TRUE" : false;

                entry.dialogueLine.choices.Add(choice);
            }

            npcData.conversations.Add(entry);
            entryByKey[conversationKey] = entry;
        }

        // second pass - wire up nextLine using next_row_key
        foreach (string[] columns in rows)
        {
            string conversationKey = columns.Length > 1 ? columns[1].Trim() : "";
            string nextRowKey =      columns.Length > 18 ? columns[18].Trim() : "";

            if (string.IsNullOrEmpty(conversationKey)) continue;
            if (string.IsNullOrEmpty(nextRowKey)) continue;

            if (!entryByKey.ContainsKey(conversationKey)) continue;
            if (!entryByKey.ContainsKey(nextRowKey))
            {
                Debug.LogWarning("next_row_key '" + nextRowKey + "' not found for conversation '" + conversationKey + "'");
                continue;
            }

            ConversationEntry currentEntry = entryByKey[conversationKey];
            ConversationEntry nextEntry = entryByKey[nextRowKey];

            Debug.Log("Wiring: " + conversationKey + " → " + nextRowKey +
                      " | choices: " + currentEntry.dialogueLine.choices.Count);

            if (currentEntry.dialogueLine.choices == null ||
                currentEntry.dialogueLine.choices.Count == 0)
            {
                currentEntry.dialogueLine.nextLineKey = nextRowKey;
                Debug.Log("Wired nextLineKey: " + conversationKey + " → " + nextRowKey);
            }
            else
            {
                foreach (DialogueChoice choice in currentEntry.dialogueLine.choices)
                {
                    if (choice.action == ChoiceAction.None)
                    {
                        choice.nextLine = nextEntry.dialogueLine;
                    }
                }
                Debug.Log("Wired nextLine on choices for: " + conversationKey);
            }
        }

        EditorUtility.SetDirty(npcData);
    }
} 

   static void ImportConditions()
   {
       string path = Path.Combine(Application.streamingAssetsPath, "Conditions.csv");

       if (!File.Exists(path))
       {
           Debug.LogError("Conditions.csv not found at: " + path);
           return;
       }

       string[] lines = File.ReadAllLines(path);

       // conditions system not fully built yet
       // just log what's in the file so authors can verify it's being read
       Debug.Log("Conditions found: " + (lines.Length - 1));

       for (int i = 1; i < lines.Length; i++)
       {
           string line = lines[i].Trim();
           if (string.IsNullOrEmpty(line)) continue;

           string[] columns = ParseCSVLine(line);
           if (columns.Length < 2) continue;

           Debug.Log("Condition — quest: " + columns[0].Trim() + 
                     " | type: " + columns[1].Trim() + 
                     " | param: " + (columns.Length > 2 ? columns[2].Trim() : "") +
                     " | value: " + (columns.Length > 3 ? columns[3].Trim() : ""));
       }
   }
   
   // parses a single CSV line respecting quoted fields
    // handles: simple,values and "values, with, commas" and "values with ""quotes"""
    static string[] ParseCSVLine(string line)
    {
        List<string> fields = new List<string>();
        bool inQuotes = false;
        string current = "";

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    // escaped quote inside quoted field
                    current += '"';
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(current);
                current = "";
            }
            else
            {
                current += c;
            }
        }

        fields.Add(current);
        return fields.ToArray();
    }

    static QuestType ParseQuestType(string value)
    {
        switch (value)
        {
            case "DrawSomething": return QuestType.DrawSomething;
            case "CreateNPC":     return QuestType.CreateNPC;
            case "IteratedDraw":  return QuestType.IteratedDraw;
            default:
                Debug.LogWarning("Unknown QuestType: " + value + " — defaulting to DrawSomething");
                return QuestType.DrawSomething;
        }
    }

    static ChoiceAction ParseChoiceAction(string value)
    {
        switch (value)
        {
            case "OpenDrawingQuest":  return ChoiceAction.OpenDrawingQuest;
            case "TriggerQuestCheck": return ChoiceAction.TriggerQuestCheck;
            case "None":              return ChoiceAction.None;
            default:
                Debug.LogWarning("Unknown ChoiceAction: " + value + " — defaulting to None");
                return ChoiceAction.None;
        }
    }

    static void EnsureFolderExists(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string[] parts = path.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}
