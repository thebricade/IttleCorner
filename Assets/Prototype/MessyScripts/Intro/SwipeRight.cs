using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class SwipeHint : MonoBehaviour
{
    [Header("Settings")]
    public Color hintColor = new Color(1f, 1f, 1f, 0.8f);
    public float circleSize = 60f;
    public float trailCircleSize = 30f;
    public float swipeDistance = 200f;
    public float animationDuration = 1.5f;
    public float pauseBetweenLoops = 0.5f;
    public int trailCount = 6;

    [Header("Idle Settings")]
    public float idleTimeBeforeShow = 3f;

    private GameObject[] trailObjects;
    private GameObject mainCircle;
    private RectTransform mainRect;
    private Image mainImage;
    private Vector2 startPos;

    private float idleTimer = 0f;
    private bool isDrawing = false;
    private bool isVisible = false;

    void Start()
    {
        startPos = Vector2.zero;
        BuildHint();
        HideHint();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            isDrawing = true;
            idleTimer = 0f;

            if (isVisible)
                HideHint();
        }

        if (Input.GetMouseButtonUp(0))
        {
            isDrawing = false;
        }

        if (!isDrawing)
        {
            idleTimer += Time.deltaTime;

            if (idleTimer >= idleTimeBeforeShow && !isVisible)
            {
                ShowHint();
            }
        }
        else
        {
            idleTimer = 0f;
        }
    }

    void ShowHint()
    {
        isVisible = true;
        Reset();
        StartCoroutine(AnimateLoop());
    }

    void HideHint()
    {
        isVisible = false;
        StopAllCoroutines();
        if (mainImage != null)
            mainImage.color = new Color(hintColor.r, hintColor.g, hintColor.b, 0f);
        if (trailObjects != null)
            foreach (GameObject trail in trailObjects)
                trail.GetComponent<Image>().color = Color.clear;
    }

    void BuildHint()
    {
        mainCircle = CreateCircle("MainCircle", circleSize, hintColor);
        mainRect = mainCircle.GetComponent<RectTransform>();
        mainImage = mainCircle.GetComponent<Image>();
        mainRect.anchoredPosition = startPos;

        trailObjects = new GameObject[trailCount];
        for (int i = 0; i < trailCount; i++)
        {
            float t = (i + 1f) / trailCount;
            float size = Mathf.Lerp(trailCircleSize, circleSize * 0.5f, 1f - t);
            trailObjects[i] = CreateCircle("Trail_" + i, size, new Color(hintColor.r, hintColor.g, hintColor.b, 0f));
        }
    }

    GameObject CreateCircle(string name, float size, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(transform, false);

        Image img = obj.AddComponent<Image>();
        img.color = color;
        img.sprite = CreateCircleSprite();

        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(size, size);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        return obj;
    }

    Sprite CreateCircleSprite()
    {
        int res = 64;
        Texture2D tex = new Texture2D(res, res);
        Vector2 center = new Vector2(res / 2f, res / 2f);
        float radius = res / 2f;

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float alpha = dist <= radius ? 1f : 0f;
                if (dist > radius - 2f && dist <= radius)
                    alpha = 1f - ((dist - (radius - 2f)) / 2f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f));
    }

    IEnumerator AnimateLoop()
    {
        while (true)
        {
            yield return StartCoroutine(PulseAtStart());
            yield return StartCoroutine(SwipeRight());
            yield return StartCoroutine(FadeOut());
            yield return new WaitForSeconds(pauseBetweenLoops);
            Reset();
        }
    }

    IEnumerator PulseAtStart()
    {
        mainRect.anchoredPosition = startPos;
        float elapsed = 0f;
        float pulseDuration = 0.4f;

        while (elapsed < pulseDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / pulseDuration;
            float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.3f;
            mainRect.localScale = new Vector3(scale, scale, 1f);
            mainImage.color = new Color(hintColor.r, hintColor.g, hintColor.b, hintColor.a);
            yield return null;
        }

        mainRect.localScale = Vector3.one;
    }

    IEnumerator SwipeRight()
    {
        float elapsed = 0f;

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / animationDuration);
            float smoothT = t * t * (3f - 2f * t);

            float currentX = Mathf.Lerp(startPos.x, startPos.x + swipeDistance, smoothT);
            mainRect.anchoredPosition = new Vector2(currentX, startPos.y);

            for (int i = 0; i < trailCount; i++)
            {
                float trailT = Mathf.Clamp01(t - (i + 1) * 0.08f);
                float trailSmoothT = trailT * trailT * (3f - 2f * trailT);
                float trailX = Mathf.Lerp(startPos.x, startPos.x + swipeDistance, trailSmoothT);

                RectTransform trailRect = trailObjects[i].GetComponent<RectTransform>();
                trailRect.anchoredPosition = new Vector2(trailX, startPos.y);

                float trailAlpha = Mathf.Lerp(hintColor.a * 0.6f, 0f, (float)i / trailCount);
                trailAlpha *= Mathf.Clamp01(t * 3f);
                trailObjects[i].GetComponent<Image>().color = new Color(hintColor.r, hintColor.g, hintColor.b, trailAlpha);
            }

            yield return null;
        }
    }

    IEnumerator FadeOut()
    {
        float elapsed = 0f;
        float fadeDuration = 0.3f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeDuration;
            float alpha = Mathf.Lerp(hintColor.a, 0f, t);
            mainImage.color = new Color(hintColor.r, hintColor.g, hintColor.b, alpha);
            foreach (GameObject trail in trailObjects)
                trail.GetComponent<Image>().color = Color.clear;
            yield return null;
        }
    }

    void Reset()
    {
        mainRect.anchoredPosition = startPos;
        mainRect.localScale = Vector3.one;
        mainImage.color = new Color(hintColor.r, hintColor.g, hintColor.b, 0f);
        foreach (GameObject trail in trailObjects)
        {
            trail.GetComponent<RectTransform>().anchoredPosition = startPos;
            trail.GetComponent<Image>().color = Color.clear;
        }
    }
}