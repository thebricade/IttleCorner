using UnityEngine;
using UnityEngine.InputSystem;

namespace InkFlip
{
    // Sculpt something and put it in the world. Completes when a sculpture is placed while the player is in this room.
    public class SculptObjective : RoomObjective
    {
        [Tooltip("Found automatically if left empty.")]
        public SculptStudio studio;
        [Tooltip("Press this to finish the room while testing (only from inside it). None turns it off.")]
        public Key testKey = Key.None;

        RoomZone zone;

        void Awake()
        {
            zone = GetComponent<RoomZone>();
        }

        // wait until every Awake has run so the studio exists
        void Start()
        {
            if (studio == null) studio = FindAnyObjectByType<SculptStudio>();
            if (studio != null) studio.SculptureCreated += OnSculptureCreated;
        }

        void OnDestroy()
        {
            if (studio != null) studio.SculptureCreated -= OnSculptureCreated;
        }

        void OnSculptureCreated(GameObject sculpture)
        {
            if (IsComplete) return;
            if (zone == null || zone.PlayerInside || zone.Contains(sculpture.transform.position)) MarkComplete();
        }

        void Update()
        {
            if (testKey == Key.None || IsComplete) return;
            if (zone != null && !zone.PlayerInside) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[testKey].wasPressedThisFrame) MarkComplete();
        }
    }
}
