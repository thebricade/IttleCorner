using UnityEngine;
using UnityEngine.InputSystem;

namespace InkFlip
{
    // A first mini-game: put an item on a pad. The item has to be set down (not still in the glove)
    // and stay there for a moment. Uses the glove, so it makes a good test of a glove-only room.
    public class DeliverObjective : RoomObjective
    {
        public Grabbable item;
        [Tooltip("The pad on the floor. The goal area is a box above it.")]
        public Transform pad;
        [Tooltip("Size of the goal box above the pad, in the pad's own space.")]
        public Vector3 goalSize = new Vector3(1.4f, 1.2f, 1.4f);
        public Vector3 goalCenter = new Vector3(0f, 0.6f, 0f);
        [Tooltip("How long the item has to stay on the pad.")]
        public float holdSeconds = 1f;
        [Tooltip("Tinted when the item is delivered.")]
        public Renderer padRenderer;
        public Color waitingColor = new Color(0.98f, 0.82f, 0.25f);
        public Color doneColor = new Color(0.3f, 0.8f, 0.4f);
        [Tooltip("Press this to finish the room while testing. None turns it off.")]
        public Key testKey = Key.None;

        float timer;
        MaterialPropertyBlock block;
        RoomZone zone;

        public override float Progress => IsComplete ? 1f : Mathf.Clamp01(timer / Mathf.Max(0.01f, holdSeconds));

        void Awake()
        {
            zone = GetComponent<RoomZone>();
        }

        void Start()
        {
            Tint(waitingColor);
        }

        void Update()
        {
            if (IsComplete) return;

            Keyboard keyboard = Keyboard.current;
            bool testAllowed = zone == null || zone.PlayerInside; // only from inside the room
            if (testAllowed && testKey != Key.None && keyboard != null && keyboard[testKey].wasPressedThisFrame)
            {
                Finish();
                return;
            }

            if (item == null || pad == null) return;

            bool onPad = !GloveTool.IsHolding && InsideGoal(item.GetBounds().center);
            timer = onPad ? timer + Time.deltaTime : 0f;
            if (timer >= holdSeconds) Finish();
        }

        bool InsideGoal(Vector3 worldPoint)
        {
            Vector3 local = pad.InverseTransformPoint(worldPoint) - goalCenter;
            Vector3 half = goalSize * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }

        public override void ForceComplete()
        {
            if (!IsComplete) Finish();
        }

        void Finish()
        {
            Tint(doneColor);
            MarkComplete();
        }

        public override void ResetObjective()
        {
            base.ResetObjective();
            timer = 0f;
            Tint(waitingColor);
        }

        void Tint(Color color)
        {
            if (padRenderer == null) return;
            block ??= new MaterialPropertyBlock();
            padRenderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            padRenderer.SetPropertyBlock(block);
        }

        void OnDrawGizmosSelected()
        {
            if (pad == null) return;
            Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.9f);
            Gizmos.matrix = pad.localToWorldMatrix;
            Gizmos.DrawWireCube(goalCenter, goalSize);
        }
    }
}
