using UnityEngine;

public class MoveUp : MonoBehaviour
{
   
    [Header("Float Settings")]
    public float amplitude = 0.5f;    // how far up and down it travels
    public float speed = 1f;          // how fast it completes one cycle

    private Vector3 startPosition;

    void Start()
    {
        startPosition = transform.position;
    }

    void Update()
    {
        float t = (Mathf.Sin(Time.time * speed) + 1f) / 2f;
        float smoothT = t * t * (3f - 2f * t);
        float newY = Mathf.Lerp(startPosition.y - amplitude, startPosition.y + amplitude, smoothT);
        transform.position = new Vector3(startPosition.x, newY, startPosition.z);
    }
}
