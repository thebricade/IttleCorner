using UnityEngine;
using UnityEngine.UI;

public class EraserCrumb : MonoBehaviour
{
    private Image image;
    private float lifetime = 0.6f; // How many seconds the crumb lasts
    private float timer = 0f;
    private Vector2 velocity;
    private Color startColor;

    void Start()
    {
        image = GetComponent<Image>();
        if (image != null)
        {
            startColor = image.color;
        }

        // Give the crumb a random gentle drift outwards and downwards
        velocity = new Vector2(Random.Range(-40f, 40f), Random.Range(-60f, -20f));
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

        // Move the crumb downward over time
        transform.Translate(velocity * Time.deltaTime);

        // Fade the alpha out smoothly
        if (image != null)
        {
            Color c = startColor;
            c.a = Mathf.Lerp(startColor.a, 0f, t);
            image.color = c;
        }
    }
}