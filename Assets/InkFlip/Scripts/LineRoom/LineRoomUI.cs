using System;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlip
{
    // How a conversation starts a mini-game: the DialogueRunner raises the id from the choice's param column.
    // Games listen with MiniGameEvents.Requested += ... and open once the conversation has closed.
    public static class MiniGameEvents
    {
        public static event Action<string> Requested;
        public static void Request(string id) => Requested?.Invoke(id);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Requested = null;
    }

    // The small shared HUD of the Lines room: a hint at the bottom, a status line at the top and a big message
    // in the middle. Built in code on first use, shared by the race, the colouring book and the lights game.
    public class LineRoomUI : MonoBehaviour
    {
        static LineRoomUI instance;

        // true while one of the room's full-screen boards is open (the race board, a colouring page), so the others stay quiet
        public static bool Busy;

        public static LineRoomUI Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<LineRoomUI>();
                    if (instance == null) instance = new GameObject("LineRoomUI").AddComponent<LineRoomUI>();
                }
                return instance;
            }
        }

        Text prompt;
        Text status;
        Text message;
        Image barBack;
        Image barFill;
        float messageTimer;
        bool built;

        void Build()
        {
            if (built) return;
            built = true;
            HudFactory.EnsureEventSystem();
            RectTransform canvas = HudFactory.CreateCanvas("LineRoomCanvas", transform, 38);

            prompt = HudFactory.CreateText("Prompt", canvas, "", 30, TextAnchor.MiddleCenter);
            Place(prompt.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(1200f, 60f));

            status = HudFactory.CreateText("Status", canvas, "", 34, TextAnchor.MiddleCenter);
            Place(status.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(1400f, 60f));

            barBack = HudFactory.CreateImage("BarBack", canvas, null, new Color(0f, 0f, 0f, 0.55f), 0f);
            Place(barBack.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -125f), new Vector2(520f, 22f));
            barFill = HudFactory.CreateImage("BarFill", barBack.rectTransform, null, new Color(0.35f, 1f, 0.9f), 0f);
            barFill.rectTransform.anchorMin = Vector2.zero;
            barFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            barFill.rectTransform.pivot = new Vector2(0f, 0.5f);
            barFill.rectTransform.offsetMin = new Vector2(3f, 3f);
            barFill.rectTransform.offsetMax = new Vector2(3f, -3f);
            barBack.gameObject.SetActive(false);

            message = HudFactory.CreateText("Message", canvas, "", 56, TextAnchor.MiddleCenter);
            Place(message.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(1500f, 200f));
        }

        static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        // Several scripts want the bottom hint (the switch, the colouring pictures). Each one passes itself as the owner
        // and sets its own text, or "" for nothing; the first non-empty one is shown.
        readonly System.Collections.Generic.Dictionary<object, string> prompts = new System.Collections.Generic.Dictionary<object, string>();

        public void SetPrompt(object owner, string text)
        {
            Build();
            if (string.IsNullOrEmpty(text)) prompts.Remove(owner);
            else prompts[owner] = text;

            string shown = "";
            foreach (string t in prompts.Values)
            {
                shown = t;
                break;
            }
            if (prompt.text != shown) prompt.text = shown;
        }

        public void SetStatus(string text)
        {
            Build();
            if (status.text != text) status.text = text;
        }

        // 0-1, or a negative number to hide the bar
        public void SetBar(float fraction, Color color)
        {
            Build();
            bool show = fraction >= 0f;
            if (barBack.gameObject.activeSelf != show) barBack.gameObject.SetActive(show);
            if (!show) return;
            barFill.color = color;
            float width = (barBack.rectTransform.sizeDelta.x - 6f) * Mathf.Clamp01(fraction);
            barFill.rectTransform.sizeDelta = new Vector2(width, barFill.rectTransform.sizeDelta.y);
        }

        public void Say(string text, float seconds = 2.5f)
        {
            Build();
            message.text = text;
            messageTimer = seconds;
        }

        void Update()
        {
            if (messageTimer <= 0f) return;
            messageTimer -= Time.unscaledDeltaTime;
            if (messageTimer <= 0f) message.text = "";
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; Busy = false; }
    }
}
