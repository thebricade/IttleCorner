using UnityEngine;
using UnityEngine.UI;

public class EraserCrumb : MonoBehaviour
{
    private Image image;
    private float lifetime = 0.6f; // How many seconds the crumb lasts
    private float timer = 0f;
    private Vector2 velocity;
    private Color startColor;

    // --- Tactile Tumble Physics Variable ---
    private float tumbleSpeed;

    void Start()
    {
        image = GetComponent<Image>();
        if (image != null)
        {
            startColor = image.color;
        }

        // Give the crumb a random gentle drift outwards and downwards
        velocity = new Vector2(Random.Range(-40f, 40f), Random.Range(-60f, -20f));

        // Pick a random rotational spin speed (clockwise or counter-clockwise)
        tumbleSpeed = Random.Range(-350f, 350f);
    }

    void Update()
    {
        timer += Time.deltaTime;
        float t = timer / lifetime;

        // Self-destruct once the lifetime is over
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        transform.Translate(velocity * Time.deltaTime, Space.World); // Using Space.World keeps the crumb drifting DOWNWARD while its local Z-axis spins and tumbles rapidly.
        transform.Rotate(0f, 0f, tumbleSpeed * Time.deltaTime);      // Rotate the curly crumb on its Z-axis as it falls!

        // Fade the alpha out smoothly
        if (image != null)
        {
            Color c = startColor;
            c.a = Mathf.Lerp(startColor.a, 0f, t);
            image.color = c;
        }
    }
}