using UnityEngine;

namespace InkFlip
{
    // The final challenge: a planetarium full of blank planets. Colour every planet, and make each one different.
    //
    // Every few moments one planet at a time is checked: how much of its surface has paint on it, and what colour that
    // paint is on average. A planet counts once enough of it is covered, and it is "too alike" if another planet's
    // colour is within Distinct Delta E of it (so two blues are fine until they look the same). Each planet has a small
    // beacon above it: grey = empty, orange = looks like another planet, green = good.
    // The room is solved when every planet is green. Painting works with any tool that paints.
    public class PlanetariumGame : MonoBehaviour
    {
        public RoomZone zone;
        public Paintable[] planets;
        [Tooltip("One beacon light per planet, in the same order.")]
        public Renderer[] beacons;

        [Header("Rules")]
        [Tooltip("How much of a planet must be covered with paint to count as coloured.")]
        [Range(0.05f, 1f)] public float coverageNeeded = 0.3f;
        [Tooltip("Colours closer than this (Delta E) count as the same. About 2 is invisible, 15 is easy to tell apart.")]
        public float distinctDeltaE = 14f;
        [Tooltip("Seconds between checking one planet.")]
        public float checkInterval = 0.2f;

        public Color emptyColor = new Color(0.45f, 0.47f, 0.55f);
        public Color alikeColor = new Color(1f, 0.6f, 0.15f);
        public Color goodColor = new Color(0.3f, 1f, 0.5f);

        public int PlanetCount => planets != null ? planets.Length : 0;
        public int GoodCount { get; private set; }
        public bool Solved { get; private set; }
        public event System.Action SolvedEvent;

        float[] coverage;
        Color[] average;
        bool[] painted;
        bool[] alike;
        int next;
        float timer;
        bool statusShown;
        Texture2D readback;
        MaterialPropertyBlock block;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        void Start()
        {
            int n = PlanetCount;
            coverage = new float[n];
            average = new Color[n];
            painted = new bool[n];
            alike = new bool[n];
            block = new MaterialPropertyBlock();
            ShowBeacons();
        }

        void OnDestroy()
        {
            if (readback != null) Destroy(readback);
        }

        void Update()
        {
            if (Solved || PlanetCount == 0) return;

            bool inside = zone != null && zone.PlayerInside;
            if (!inside)
            {
                if (statusShown)
                {
                    LineRoomUI.Instance.SetStatus("");
                    LineRoomUI.Instance.SetBar(-1f, Color.white);
                    statusShown = false;
                }
                return;
            }

            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                timer = checkInterval;
                MeasurePlanet(next);
                next = (next + 1) % PlanetCount;
                Recompute();
            }

            LineRoomUI ui = LineRoomUI.Instance;
            int clashing = 0;
            for (int i = 0; i < PlanetCount; i++) if (alike[i]) clashing++;
            ui.SetStatus($"Planets coloured: {GoodCount} / {PlanetCount}" + (clashing > 0 ? "   (some look too alike!)" : ""));
            ui.SetBar(GoodCount / (float)PlanetCount, goodColor);
            statusShown = true;
        }

        void MeasurePlanet(int i)
        {
            Paintable planet = planets[i];
            if (planet == null || !planet.IsAllocated)
            {
                coverage[i] = 0f;
                painted[i] = false;
                return;
            }

            Color32[] mask = Read(planet.MaskTexture);
            Color32[] island = Read(planet.IslandTexture);
            float alphaSum = 0f, surface = 0f, r = 0f, g = 0f, b = 0f;
            for (int p = 0; p < mask.Length; p++)
            {
                alphaSum += mask[p].a;
                r += mask[p].r;
                g += mask[p].g;
                b += mask[p].b;
                surface += island[p].r;
            }

            coverage[i] = surface > 1f ? Mathf.Clamp01(alphaSum / surface) : 0f;
            painted[i] = coverage[i] >= coverageNeeded && alphaSum > 1f;
            // the mask stores colour multiplied by amount, so dividing by the amount gives the average ink colour
            if (alphaSum > 1f) average[i] = new Color(r / alphaSum, g / alphaSum, b / alphaSum, 1f);
        }

        Color32[] Read(RenderTexture source)
        {
            int size = Mathf.Min(source.width, 128);
            RenderTexture small = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Graphics.Blit(source, small);
            if (readback == null || readback.width != size) readback = new Texture2D(size, size, TextureFormat.RGBA32, false, true);

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = small;
            readback.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(small);
            return readback.GetPixels32();
        }

        void Recompute()
        {
            for (int i = 0; i < PlanetCount; i++) alike[i] = false;
            for (int i = 0; i < PlanetCount; i++)
            {
                if (!painted[i]) continue;
                for (int j = i + 1; j < PlanetCount; j++)
                {
                    if (!painted[j]) continue;
                    if (PaintMix.DeltaE(average[i], average[j]) < distinctDeltaE) alike[i] = alike[j] = true;
                }
            }

            GoodCount = 0;
            for (int i = 0; i < PlanetCount; i++) if (painted[i] && !alike[i]) GoodCount++;
            ShowBeacons();

            if (GoodCount >= PlanetCount) Finish();
        }

        void ShowBeacons()
        {
            if (beacons == null || painted == null) return;
            for (int i = 0; i < beacons.Length && i < PlanetCount; i++)
            {
                if (beacons[i] == null) continue;
                Color c = !painted[i] ? emptyColor : alike[i] ? alikeColor : goodColor;
                beacons[i].GetPropertyBlock(block);
                block.SetColor(BaseColorId, c);
                beacons[i].SetPropertyBlock(block);
            }
        }

        void Finish()
        {
            if (Solved) return;
            Solved = true;
            LineRoomUI ui = LineRoomUI.Instance;
            ui.SetStatus("");
            ui.SetBar(-1f, Color.white);
            ui.Say("Every planet has its own colour!", 4f);
            SolvedEvent?.Invoke();
        }

        // for testing: call it from the inspector's right-click menu or the objective's test key
        [ContextMenu("Solve now")]
        public void ForceSolve()
        {
            if (Solved) return;
            if (painted != null)
            {
                for (int i = 0; i < PlanetCount; i++)
                {
                    painted[i] = true;
                    alike[i] = false;
                }
                GoodCount = PlanetCount;
                ShowBeacons();
            }
            Finish();
        }
    }
}
