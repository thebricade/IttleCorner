using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlip
{
    // The "you've entered ..." title that fades in when the player walks into a room (and when the game starts in one).
    // It is an ordinary canvas you can restyle in the editor: open the RoomTitleCard prefab and change the fonts,
    // colours, sizes, layout, background or add images. This script only fills in the text and handles the fade.
    //
    // Each room sets its own wording on its RoomZone (display name, subtitle, accent colour). A room can also point
    // at its own copy of this card for a different look; see RoomZone.customTitleCard.
    public class RoomTitleCard : MonoBehaviour
    {
        [Header("What to fill in")]
        public TMP_Text titleText;
        [Tooltip("Optional. Hidden when a room has no subtitle.")]
        public TMP_Text subtitleText;
        [Tooltip("Optional. Tinted with the room's accent colour - a line, a badge, a glow...")]
        public Graphic accent;
        public CanvasGroup group;

        [Header("Timing (seconds)")]
        public float fadeInSeconds = 0.35f;
        public float holdSeconds = 2.5f;
        public float fadeOutSeconds = 0.8f;

        [Header("Motion")]
        [Tooltip("Slides in from this far below (in canvas pixels). 0 = just fades.")]
        public float slideDistance = 40f;
        [Tooltip("The part that slides. Leave empty to slide the whole card.")]
        public RectTransform slideTarget;

        [Header("Which rooms")]
        [Tooltip("Shows for every room that doesn't have its own custom card. Turn off on a card you only point specific rooms at.")]
        public bool isDefaultCard = true;

        float timer = -1f; // < 0 = not showing
        Vector2 slideRest;
        bool restCached;

        float TotalSeconds => fadeInSeconds + holdSeconds + fadeOutSeconds;

        void Awake()
        {
            if (group == null) group = GetComponent<CanvasGroup>();
            CacheRest();
            Hide();
        }

        void OnEnable()
        {
            RoomZone.Entered += OnRoomEntered;
        }

        void OnDisable()
        {
            RoomZone.Entered -= OnRoomEntered;
        }

        void CacheRest()
        {
            if (restCached) return;
            if (slideTarget == null) slideTarget = transform as RectTransform;
            if (slideTarget != null) slideRest = slideTarget.anchoredPosition;
            restCached = true;
        }

        void OnRoomEntered(RoomZone zone)
        {
            if (!zone.showTitleCard) return;
            if (zone.titleShown && zone.showTitleOnlyFirstTime) return;

            // a room with its own card is shown by that card only; every other room by the default one
            bool mine = zone.customTitleCard != null ? zone.customTitleCard == this : isDefaultCard;
            if (!mine) return;

            zone.titleShown = true;
            Show(zone.displayName, zone.subtitle, zone.titleAccent);
        }

        // Show any title, e.g. from a script: card.Show("Boss Room", "Good luck", Color.red)
        public void Show(string title, string subtitle = "", Color? accentColor = null)
        {
            CacheRest();
            if (titleText != null) titleText.text = title;
            if (subtitleText != null)
            {
                subtitleText.text = subtitle;
                subtitleText.gameObject.SetActive(!string.IsNullOrEmpty(subtitle));
            }
            if (accent != null && accentColor.HasValue) accent.color = accentColor.Value;

            timer = 0f;
            Apply();
        }

        public void Hide()
        {
            timer = -1f;
            if (group != null) group.alpha = 0f;
            if (slideTarget != null && restCached) slideTarget.anchoredPosition = slideRest;
        }

        void Update()
        {
            if (timer < 0f) return;

            timer += Time.unscaledDeltaTime;
            if (timer >= TotalSeconds)
            {
                Hide();
                return;
            }
            Apply();
        }

        void Apply()
        {
            float alpha;
            float slide;
            if (timer < fadeInSeconds)
            {
                float t = fadeInSeconds > 0f ? timer / fadeInSeconds : 1f;
                alpha = Mathf.SmoothStep(0f, 1f, t);
                slide = 1f - Mathf.SmoothStep(0f, 1f, t);
            }
            else if (timer < fadeInSeconds + holdSeconds)
            {
                alpha = 1f;
                slide = 0f;
            }
            else
            {
                float t = fadeOutSeconds > 0f ? (timer - fadeInSeconds - holdSeconds) / fadeOutSeconds : 1f;
                alpha = 1f - Mathf.SmoothStep(0f, 1f, t);
                slide = 0f;
            }

            if (group != null) group.alpha = alpha;
            if (slideTarget != null) slideTarget.anchoredPosition = slideRest + Vector2.down * (slideDistance * slide);
        }
    }
}
