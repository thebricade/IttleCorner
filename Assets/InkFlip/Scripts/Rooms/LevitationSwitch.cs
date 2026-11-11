using UnityEngine;

namespace InkFlip
{
    // A pressure plate for the glove puzzle. It counts as pressed while something the glove can move (a Grabbable) is
    // resting in the box above it. An object being carried doesn't count - it has to be set down.
    public class LevitationSwitch : MonoBehaviour
    {
        [Tooltip("The box above the plate that counts, in this object's own space.")]
        public Vector3 goalCenter = new Vector3(0f, 0.44f, 0f);
        public Vector3 goalSize = new Vector3(0.9f, 0.8f, 0.9f);

        [Header("Look")]
        [Tooltip("The plate; tinted by state.")]
        public Renderer plateRenderer;
        [Tooltip("A pole beside the plate; tinted by state so you can see it from across the room.")]
        public Renderer beaconRenderer;
        public Color sleepingColor = new Color(0.42f, 0.44f, 0.5f);
        public Color waitingColor = new Color(1f, 0.6f, 0.24f);
        public Color pressedColor = new Color(0.28f, 0.9f, 0.48f);

        [Tooltip("The plate does nothing until the puzzle is switched on.")]
        public bool armed;

        public bool Pressed { get; private set; }

        MaterialPropertyBlock plateBlock;
        MaterialPropertyBlock beaconBlock;
        Collider[] hits = new Collider[16];
        float pressedFor;
        int shownState = -1;

        void Start()
        {
            Refresh(true);
        }

        public void SetArmed(bool value)
        {
            armed = value;
            if (!armed) Pressed = false;
            pressedFor = 0f;
            Refresh(true);
        }

        void Update()
        {
            bool on = armed && SomethingOnPlate();
            pressedFor = on ? pressedFor + Time.deltaTime : 0f;
            Pressed = pressedFor >= 0.25f; // a short settle so a crate swung across the plate doesn't count
            Refresh(false);
        }

        bool SomethingOnPlate()
        {
            Vector3 centre = transform.TransformPoint(goalCenter);
            int count = Physics.OverlapBoxNonAlloc(centre, Vector3.Scale(goalSize, transform.lossyScale) * 0.5f, hits,
                                                   transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (hits[i] != null && hits[i].GetComponentInParent<Grabbable>() != null) return true;
            }
            return false;
        }

        void Refresh(bool force)
        {
            int state = !armed ? 0 : (Pressed ? 2 : 1);
            if (!force && state == shownState) return;
            shownState = state;

            Color color = state == 0 ? sleepingColor : (state == 2 ? pressedColor : waitingColor);
            Tint(plateRenderer, ref plateBlock, color);
            Tint(beaconRenderer, ref beaconBlock, color);
        }

        static void Tint(Renderer renderer, ref MaterialPropertyBlock block, Color color)
        {
            if (renderer == null) return;
            block ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(block);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(goalCenter, goalSize);
        }
    }
}
