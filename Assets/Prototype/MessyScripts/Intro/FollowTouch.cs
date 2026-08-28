using UnityEngine;

public class FollowTouch : MonoBehaviour
{
    
    private Vector2 lastMousePos;
    private float lastAngle;
    private int consistentRotationCount = 0;
    private bool isCircling = false;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButton(0))
        {
            Vector2 mousePos = Input.mousePosition;
            Vector2 mouseDelta = mousePos - lastMousePos;

            if (mouseDelta.magnitude > 0.5f)
            {
                float angle = Mathf.Atan2(mouseDelta.y, mouseDelta.x) * Mathf.Rad2Deg;

                // how much did the angle change since last frame
                float angleDelta = Mathf.DeltaAngle(lastAngle, angle);

                // if angle keeps changing in same direction, count it
                if (Mathf.Abs(angleDelta) > 5f)
                {
                    if (angleDelta > 0)
                        consistentRotationCount++;
                    else
                        consistentRotationCount--;
                }

                // clamp so it doesn't grow forever
                consistentRotationCount = Mathf.Clamp(consistentRotationCount, -10, 10);

                // if count is high enough in either direction, we're circling
                isCircling = Mathf.Abs(consistentRotationCount) >= 6;

                if (isCircling)
                {
                    // roll with the gesture
                    transform.eulerAngles = new Vector3(0, 0, angle);
                }
                else
                {
                    // look at mouse position
                    Vector2 eyeScreenPos = RectTransformUtility.WorldToScreenPoint(null, transform.position);
                    Vector2 direction = mousePos - eyeScreenPos;
                    float lookAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                    transform.eulerAngles = new Vector3(0, 0, lookAngle);
                }

                lastAngle = angle;
            }

            lastMousePos = mousePos;
        }
        else
        {
            lastMousePos = Input.mousePosition;
            // decay the circle count when not drawing
            // so it resets naturally between strokes
            if (consistentRotationCount > 0) consistentRotationCount--;
            if (consistentRotationCount < 0) consistentRotationCount++;
        }
    }
}
