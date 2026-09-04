using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using System.Collections;

public class IntroTracker : MonoBehaviour
{
    [Header("Triggers")] 
    public float timeTrigger = 15f;
    [Range(0f, 1f)] public float coverageTrigger = 0.15f;
    [Range(0f, 1f)] public float submitCoverageTrigger = 0.6f;

    [Header("References")] 
    public DrawingPad drawingPad;
    public GameObject brushBuddy;
    public GameObject firstTouchHint;   // appears on first touch, vanishes after 6s
    public GameObject secondHint;       // appears after time/coverage threshold, vanishes after 6s
    public GameObject submitButton;

    [Header("Events")] 
    public UnityEvent onBrushBuddyTriggered;

    // counters
    private float elapsedTime = 0f;
    private bool firstTouchFired = false;

    // flags
    private bool brushBuddyTriggered = false;
    private bool secondHintTriggered = false;
    private bool submitTriggered = false;

    void Update()
    {
        // first touch - show firstTouchHint and brushbuddy
        if (!firstTouchFired && Input.GetMouseButtonDown(0))
        {
            firstTouchFired = true;
            brushBuddy.SetActive(true);
            firstTouchHint.SetActive(true);
            StartCoroutine(HideAfterDelay(firstTouchHint, 6f));
            onBrushBuddyTriggered.Invoke();
        }

        // only start counting time after first touch
        if (firstTouchFired)
        {
            elapsedTime += Time.deltaTime;
        }

        // second hint - after time or coverage threshold
        if (firstTouchFired && !secondHintTriggered)
        {
            float coverage = drawingPad.GetCoveragePercentage();
            if (elapsedTime >= timeTrigger || coverage >= coverageTrigger)
            {
                secondHintTriggered = true;
                secondHint.SetActive(true);
                StartCoroutine(HideAfterDelay(secondHint, 6f));
            }
        }

        // submit button - when coverage is near full
        if (!submitTriggered)
        {
            float coverage = drawingPad.GetCoveragePercentage();
            if (coverage >= submitCoverageTrigger)
            {
                TriggerSubmit();
            }
        }
    }

    IEnumerator HideAfterDelay(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (obj != null) obj.SetActive(false);
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
        SceneManager.LoadScene(1);
    }
}
