using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class IntroTacker : MonoBehaviour
{
    [Header("Triggers")] 
    public float timeTrigger = 40f;
    public int clickTrigger = 4;
    [Range(0f, 1f)] public float coverageTrigger = 0.15f;

    [Header("References")] 
    public DrawingPad drawingPad;
    public GameObject brushBuddy;
    public GameObject submitButton;

    [Header("Events")] 
    public UnityEvent onBrushBuddyTriggered;

    // counters
    private float elapsedTime = 0f; 
    private int clickCount = 0;

    // flags - one per trigger moment
    private bool brushBuddyTriggered = false;
    private bool submitTriggered = false;

    void Update()
    {
        // coverage check - only until submit is shown
        if (!submitTriggered)
        {
            float coverage = drawingPad.GetCoveragePercentage();
            if (coverage >= coverageTrigger)
            {
                TriggerSubmit();
                return;
            }
        }

        // clicks and time - only until brushbuddy is shown
        if (!brushBuddyTriggered)
        {
            if (Input.GetMouseButtonDown(0))
            {
                clickCount++;
            }

            elapsedTime += Time.deltaTime;

            if (elapsedTime >= timeTrigger)
            {
                TriggerBrushBuddy("time");
                return;
            }

            if (clickCount >= clickTrigger)
            {
                TriggerBrushBuddy("clicks");
                return;
            }
        }
    }

    void TriggerBrushBuddy(string reason)
    {
        brushBuddyTriggered = true;
        brushBuddy.SetActive(true);
        Debug.Log("BrushBuddy triggered via: " + reason);
        onBrushBuddyTriggered.Invoke();
    }

    void TriggerSubmit()
    {
        submitTriggered = true;
        submitButton.SetActive(true);
        Debug.Log("Submit button shown");
    }

    public void SaveIntroDrawing()
    {
        Texture2D snapshot = drawingPad.GetCurrentTextureCopy();
        Texture2D cropped = drawingPad.CropToContent(snapshot);
        DrawingManager.Instance.SaveDrawing(cropped, "IntroDrawing");
        Debug.Log("Saved count: " + DrawingManager.Instance.savedDrawings.Count);
        Debug.Log("Drawing name: " + DrawingManager.Instance.savedDrawings[0].drawingName);
        SceneManager.LoadScene(1);
    }
}
