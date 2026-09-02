using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class SelectionScreen : MonoBehaviour
{
    public GameObject selectionCanvas;
    public RawImage[] slots;
    public Button[] slotButtons;

    private string currentQuestId;
    private List<Drawing> currentDrawings = new List<Drawing>();

    public void Show(string tag, string questId)
    {
        currentQuestId = questId;
        currentDrawings.Clear();

        currentDrawings = DrawingManager.Instance.savedDrawings.FindAll(
            d => d.drawingName == tag
        );

        Debug.Log("Selection screen showing " + currentDrawings.Count + " drawings tagged: " + tag);

        for (int i = 0; i < slots.Length; i++)
        {
            if (i < currentDrawings.Count)
            {
                slots[i].texture = currentDrawings[i].texture;
                slots[i].gameObject.SetActive(true);

                int index = i;
                slotButtons[i].onClick.RemoveAllListeners();
                slotButtons[i].onClick.AddListener(() => OnDrawingSelected(index));
            }
            else
            {
                slots[i].gameObject.SetActive(false);
            }
        }

        selectionCanvas.SetActive(true);
    }

    void OnDrawingSelected(int index)
    {
        Drawing selected = currentDrawings[index];
        Debug.Log("Player selected: " + selected.drawingName);

        Quest quest = QuestManager.Instance.GetQuest(currentQuestId);

        // handle reward based on quest reward type
        if (quest != null)
        {
            switch (quest.rewardType)
            {
                case QuestRewardType.BecomeSky:
                    ApplyAsSkybox(selected.texture);
                    break;
            }
        }

        // complete quest and advance conversation
        QuestManager.Instance.CompleteQuest(currentQuestId);

        if (quest != null)
        {
            foreach (NPCRuntimeState state in DrawingManager.Instance.npcStates)
            {
                if (state.npcName == quest.npcName)
                {
                    state.currentConversationKey = quest.setConversationKey;
                    break;
                }
            }
        }

        selectionCanvas.SetActive(false);
        GameModeManager.Instance.SetGameMode(GameMode.Explore);
    }

    void ApplyAsSkybox(Texture2D texture)
    {
        // create a new material using Unity's skybox shader
        Material skyboxMaterial = new Material(Shader.Find("Skybox/Panoramic"));
        skyboxMaterial.SetTexture("_MainTex", texture);
        RenderSettings.skybox = skyboxMaterial;
        DynamicGI.UpdateEnvironment();
        Debug.Log("Skybox applied!");
    }
    
}