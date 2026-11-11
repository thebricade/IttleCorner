using UnityEngine;
using UnityEngine.InputSystem;

namespace InkFlip
{
    // Something you pick up to earn an ability, like the glove: it sits on a pedestal, hidden until Reveal() is called
    // (when the room's challenge is done), then you look at it and press F (or click) to take it.
    public class AbilityPickup : MonoBehaviour
    {
        public Ability ability = Ability.InkBlaster;
        [Tooltip("The visible thing on the pedestal (switched off until revealed). It needs a collider so it can be looked at.")]
        public GameObject model;
        public RoomZone zone;
        public string itemName = "Ink Blaster";
        [TextArea] public string pickedUpMessage = "You got the Ink Blaster!";
        public float reach = 3.6f;
        public Key useKey = Key.F;

        public bool Revealed { get; private set; }
        public bool Taken { get; private set; }

        Vector3 restPosition;
        Vector3 restScale;
        float revealTime = 1f;
        float pickTime = -1f;
        bool promptShown;
        Camera cam;

        void Awake()
        {
            if (model == null) return;
            restPosition = model.transform.localPosition;
            restScale = model.transform.localScale;
            model.SetActive(false);
        }

        void OnDestroy()
        {
            if (promptShown && Application.isPlaying) LineRoomUI.Instance.SetPrompt(this, "");
        }

        public void Reveal()
        {
            if (Revealed || Taken || model == null) return;
            Revealed = true;
            revealTime = 0f;
            model.SetActive(true);
        }

        void Update()
        {
            if (model == null || !Revealed || Taken) return;

            if (pickTime >= 0f)
            {
                FlyToPlayer();
                return;
            }

            // pop in, then bob and turn
            revealTime = Mathf.Min(1f, revealTime + Time.deltaTime * 2.5f);
            float pop = Mathf.SmoothStep(0f, 1f, revealTime);
            model.transform.localScale = restScale * pop;
            model.transform.localPosition = restPosition + Vector3.up * (Mathf.Sin(Time.time * 2f) * 0.04f);
            model.transform.Rotate(0f, 60f * Time.deltaTime, 0f, Space.World);

            if (zone == null || !zone.PlayerInside || LineRoomUI.Busy || !FirstPersonController.InputCaptured)
            {
                ShowPrompt("");
                return;
            }

            if (cam == null) cam = Camera.main;
            bool looking = false;
            if (cam != null && Physics.Raycast(cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)), out RaycastHit hit, reach, ~0, QueryTriggerInteraction.Ignore))
            {
                looking = hit.collider.transform.IsChildOf(model.transform);
            }
            ShowPrompt(looking ? $"Press {useKey} to take the {itemName}" : "");
            if (!looking) return;

            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            bool pressed = (keyboard != null && keyboard[useKey].wasPressedThisFrame)
                           || (mouse != null && mouse.leftButton.wasPressedThisFrame)
                           || (Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame);
            if (pressed)
            {
                pickTime = 0f;
                ShowPrompt("");
            }
        }

        // it flies up to the player's hand and shrinks away, then the ability is theirs
        void FlyToPlayer()
        {
            pickTime += Time.deltaTime;
            float t = Mathf.Clamp01(pickTime / 0.8f);
            if (cam == null) cam = Camera.main;
            Vector3 target = cam != null ? cam.transform.position + cam.transform.forward * 0.6f + cam.transform.right * 0.2f - cam.transform.up * 0.2f : model.transform.position;
            model.transform.position = Vector3.Lerp(model.transform.position, target, 1f - Mathf.Pow(0.001f, Time.deltaTime * 3f));
            model.transform.localScale = restScale * (1f - Mathf.SmoothStep(0f, 1f, t));
            if (t < 1f) return;

            Taken = true;
            model.SetActive(false);
            Abilities.Grant(ability);

            // hold it straight away
            var switcher = FindAnyObjectByType<ToolSwitcher>();
            if (switcher != null && switcher.tools != null)
            {
                for (int i = 0; i < switcher.tools.Length; i++)
                {
                    if (switcher.tools[i].requires == ability)
                    {
                        switcher.Select(i);
                        break;
                    }
                }
            }
            LineRoomUI.Instance.Say(pickedUpMessage, 4f);
        }

        void ShowPrompt(string text)
        {
            if (!promptShown && string.IsNullOrEmpty(text)) return;
            promptShown = !string.IsNullOrEmpty(text);
            LineRoomUI.Instance.SetPrompt(this, text);
        }
    }
}
