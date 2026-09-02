using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class IntroTracker : MonoBehaviour
{
    [Header("Triggers")] 
    public float timeTrigger = 40f; //seconds

    public int clickTrigger = 4;
    [Range(0f, 1f)] public float coverageTrigger = 0.15f; //percent of canvas
    [Header("Drawing Pad ref")] 
    public DrawingPad drawingPad;

    [Header("Drawing Pad Ref")] 
    public UnityEvent onTriggerMet;
    
    //counters
    private float elapsedTime = 0f; 
    private int clickCount = 0;
    private bool triggered = false;

    public GameObject brushBuddy;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        float coverage = drawingPad.GetCoveragePercentage();
        //Debug.Log("Coverage: " + coverage + " | Threshold: " + coverageTrigger);
        if (coverage >= coverageTrigger)
        {
            TriggerFull();
            return;
        }
        
        if (triggered) return;
        
        // track clicks directly here since whole screen is canvas in intro
        if (Input.GetMouseButtonDown(0))
        {
            clickCount++;
        }
        
        //track time
        elapsedTime += Time.deltaTime;
        
        //check triggers
        if (elapsedTime >= timeTrigger)
        {
            Trigger("time");
            return;
        }

        if (clickCount >= clickTrigger)
        {
            Trigger("clicks");
            return;
        }
       
        
    }

    public void RegisterClick()
    {
        clickCount++;
    }
    public void Trigger(string reason)
    {
        triggered = true;
        brushBuddy.SetActive(true);
        Debug.Log("We've triggered " + reason);
        onTriggerMet.Invoke();
    }

    public void TriggerFull()
    {
        gameObject.SetActive(false);
        brushBuddy.SetActive(false);
        SceneManager.LoadScene(1);
    }
}
