using UnityEngine;

public class StickerListUI : MonoBehaviour
{
    public GameObject stickerButtonPrefab;
    public Transform contentParent;

    void OnEnable()
    {
        RefreshList();
    }

    void RefreshList()
    {
        // clear out any old buttons first
        foreach (Transform child in contentParent)
        {
            Destroy(child.gameObject);
        }

        // create one button per saved drawing
        foreach (Drawing d in DrawingManager.Instance.savedDrawings)
        {
            GameObject newButton = Instantiate(stickerButtonPrefab, contentParent);
            StickerButton sb = newButton.GetComponent<StickerButton>();
            sb.Setup(d);
        }
    }
}