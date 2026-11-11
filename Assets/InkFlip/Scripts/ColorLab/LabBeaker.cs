using UnityEngine;

namespace InkFlip
{
    // One piece of lab glass in the Colour Lab: a paint beaker, a sample beaker or the mixing flask.
    // The builder makes the glass and the liquid cylinder; this script only fills it to a level, tints it, shows a name tag
    // and a highlight ring when you look at it. The ColorLabGame decides what each one means.
    public class LabBeaker : MonoBehaviour
    {
        public enum Kind { Pigment, Sample, Flask }

        public Kind kind;
        [Tooltip("Name shown on the little tag (and in the hint when you look at it).")]
        public string label;
        public Color color = Color.white;
        [Range(0f, 1f)] public float fill = 0.7f;

        [Header("Parts (made by the scene builder)")]
        [Tooltip("A cylinder (the built-in one, 2 units tall) standing inside the glass. It is stretched to show the level.")]
        public Transform liquid;
        public Renderer liquidRenderer;
        [Tooltip("Height of the liquid when completely full, in metres.")]
        public float fullHeight = 0.24f;
        [Tooltip("Local height of the bottom of the liquid.")]
        public float baseY = 0.02f;
        [Tooltip("A flat ring on the floor of the glass, switched on while you look at it.")]
        public Renderer highlight;
        [Tooltip("Samples only: a little light that turns green once the sample is matched.")]
        public Renderer statusLight;
        public Color unsolvedLight = new Color(0.25f, 0.25f, 0.3f);
        public Color solvedLight = new Color(0.25f, 1f, 0.45f);
        [Tooltip("Where the point of a pouring drop starts from (above the rim). Defaults to just above the glass.")]
        public float rimHeight = 0.34f;

        public bool Solved { get; private set; }
        public float ShownFill => shownFill;

        float shownFill;
        float liquidRadiusScale = 1f;
        MaterialPropertyBlock block;
        Color shownColor;
        float punch;

        void Awake()
        {
            if (liquid != null) liquidRadiusScale = liquid.localScale.x;
            shownFill = fill;
            shownColor = color;
            BuildLabel();
            Apply();
            if (highlight != null) highlight.enabled = false;
            ShowSolved(false);
        }

        void Update()
        {
            bool changed = false;
            if (!Mathf.Approximately(shownFill, fill))
            {
                shownFill = Mathf.MoveTowards(shownFill, fill, Time.deltaTime * 1.2f);
                changed = true;
            }
            if (shownColor != color)
            {
                shownColor = Color.Lerp(shownColor, color, 1f - Mathf.Exp(-Time.deltaTime * 8f));
                if (Mathf.Abs(shownColor.r - color.r) + Mathf.Abs(shownColor.g - color.g) + Mathf.Abs(shownColor.b - color.b) < 0.004f) shownColor = color;
                changed = true;
            }
            if (changed) Apply();

            // a little wobble after something happens to it
            if (punch > 0f)
            {
                punch = Mathf.MoveTowards(punch, 0f, Time.deltaTime * 3f);
                float s = 1f + Mathf.Sin((1f - punch) * 18f) * 0.07f * punch;
                transform.localScale = new Vector3(s, 2f - s, s);
                if (punch <= 0f) transform.localScale = Vector3.one;
            }
        }

        public void Wobble() { punch = 1f; }

        // the fill and colour jump straight to their targets (used when the lab is reset)
        public void Snap()
        {
            shownFill = fill;
            shownColor = color;
            Apply();
        }

        public void SetHighlight(bool on)
        {
            if (highlight != null && highlight.enabled != on) highlight.enabled = on;
        }

        public void ShowSolved(bool solved)
        {
            Solved = solved;
            if (statusLight == null) return;
            block ??= new MaterialPropertyBlock();
            statusLight.GetPropertyBlock(block);
            block.SetColor("_BaseColor", solved ? solvedLight : unsolvedLight);
            statusLight.SetPropertyBlock(block);
        }

        public Vector3 RimPoint => transform.position + Vector3.up * rimHeight;

        void Apply()
        {
            if (liquid == null) return;

            float h = Mathf.Max(0.0005f, shownFill * fullHeight);
            bool visible = shownFill > 0.002f;
            if (liquid.gameObject.activeSelf != visible) liquid.gameObject.SetActive(visible);
            liquid.localScale = new Vector3(liquidRadiusScale, h * 0.5f, liquidRadiusScale);
            liquid.localPosition = new Vector3(0f, baseY + h * 0.5f, 0f);

            if (liquidRenderer != null)
            {
                block ??= new MaterialPropertyBlock();
                liquidRenderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", shownColor);
                liquidRenderer.SetPropertyBlock(block);
            }
        }

        // the tag is made at runtime so the scene doesn't have to store a font reference
        void BuildLabel()
        {
            if (string.IsNullOrEmpty(label)) return;

            var tag = new GameObject("Tag");
            tag.transform.SetParent(transform, false);
            tag.transform.localPosition = new Vector3(0f, 0.045f, 0.125f);
            tag.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // faces the room (the labs are viewed from the north)

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var text = tag.AddComponent<TextMesh>();
            text.font = font;
            text.text = label;
            text.fontSize = 48;
            text.characterSize = 0.0085f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = new Color(0.12f, 0.15f, 0.25f);
            tag.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }
    }
}
