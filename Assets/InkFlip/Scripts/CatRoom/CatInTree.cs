using UnityEngine;
using UnityEngine.InputSystem;

namespace InkFlip
{
    // The cat stuck up the tree. It is out of reach from the ground: the player has to build something to stand on
    // (with the Shaper and the glove), get up next to it, and press F to bring it down.
    public class CatInTree : MonoBehaviour
    {
        public RoomZone zone;
        public Transform cat;
        [Tooltip("Where the cat lands when it is rescued.")]
        public Transform landSpot;
        [Tooltip("How close (sideways) the player must be.")]
        public float reachDistance = 2.6f;
        [Tooltip("How high the player's feet must be above the floor to reach the cat.")]
        public float minFeetHeight = 1.35f;
        public Key interactKey = Key.F;
        public float hopSeconds = 1.3f;

        public bool Rescued { get; private set; }
        public bool Landed { get; private set; }

        FirstPersonController player;
        CharacterController body;
        Vector3 start;
        float hopTime;
        float clock;
        bool promptShown;

        void Start()
        {
            if (cat != null) start = cat.position;
        }

        void OnDestroy()
        {
            if (promptShown && Application.isPlaying) LineRoomUI.Instance.SetPrompt(this, "");
        }

        public void ForceRescue()
        {
            if (Rescued) return;
            Rescued = true;
            hopTime = 0f;
        }

        void Update()
        {
            if (cat == null) return;
            clock += Time.deltaTime;

            if (Rescued)
            {
                if (promptShown) ShowPrompt("");
                if (!Landed) Hop();
                return;
            }

            // a nervous little wobble
            cat.position = start + Vector3.up * (Mathf.Sin(clock * 3f) * 0.01f);

            if (zone == null || !zone.PlayerInside)
            {
                if (promptShown) ShowPrompt("");
                return;
            }
            if (player == null)
            {
                player = FindAnyObjectByType<FirstPersonController>();
                if (player != null) body = player.GetComponent<CharacterController>();
            }
            if (player == null || body == null) return;

            Vector3 p = player.transform.position;
            Vector3 c = cat.position;
            float sideways = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(c.x, c.z));
            float feet = body.bounds.min.y;

            if (sideways <= reachDistance && feet >= minFeetHeight)
            {
                ShowPrompt("Press F to rescue the cat!");
                Keyboard keyboard = Keyboard.current;
                if (keyboard != null && keyboard[interactKey].wasPressedThisFrame) ForceRescue();
            }
            else if (sideways <= reachDistance + 2.5f)
            {
                ShowPrompt("The cat is too high to reach - make something to stand on!");
            }
            else if (promptShown)
            {
                ShowPrompt("");
            }
        }

        void Hop()
        {
            hopTime += Time.deltaTime;
            float t = Mathf.Clamp01(hopTime / hopSeconds);
            Vector3 end = landSpot != null ? landSpot.position : new Vector3(start.x, 0f, start.z);
            Vector3 position = Vector3.Lerp(start, end, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 1.0f);
            Vector3 flat = end - start;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.01f) cat.rotation = Quaternion.Slerp(cat.rotation, Quaternion.LookRotation(flat), 8f * Time.deltaTime);
            cat.position = position;

            if (t >= 1f)
            {
                Landed = true;
                LineRoomUI.Instance.Say("Mew!  The cat is safe!", 3.5f);
            }
        }

        void ShowPrompt(string text)
        {
            promptShown = !string.IsNullOrEmpty(text);
            LineRoomUI.Instance.SetPrompt(this, text);
        }
    }
}
