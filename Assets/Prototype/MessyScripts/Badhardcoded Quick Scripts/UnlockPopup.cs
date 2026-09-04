using UnityEngine;
using TMPro;

public class UnlockPopup : MonoBehaviour
{
    public static UnlockPopup Instance;

    public GameObject popupPanel;
   // public TMP_Text titleText;
    //public TMP_Text descriptionText;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        popupPanel.SetActive(false);
    }

    public void Show(string title, string description, GameObject[] activate = null)
    {
        //titleText.text = title;
        //descriptionText.text = description;
        popupPanel.SetActive(true);

        if (activate != null)
        {
            foreach (GameObject obj in activate)
            {
                if (obj != null) obj.SetActive(true);
            }
        }

        StartCoroutine(AutoDismiss());
    }

    System.Collections.IEnumerator AutoDismiss()
    {
        yield return new WaitForSeconds(3f);
        popupPanel.SetActive(false);
    }
}