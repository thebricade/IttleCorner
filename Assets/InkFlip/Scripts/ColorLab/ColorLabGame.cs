using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace InkFlip
{
    // The Colour Lab: match the colour in each sample beaker by mixing paints in the flask.
    //
    //   - Look at a paint beaker and press F: one part of that paint drops into the flask.
    //   - Look at the flask and press F: empty it and start over.
    //   - Mix a colour close enough to one of the samples on the shelf and that sample is matched.
    //
    // The mixing uses PaintMix (the paint-like blend and the "how close do these look" distance from the colour mixer),
    // so blue + yellow really makes green, and white / black lighten and darken.
    public class ColorLabGame : MonoBehaviour
    {
        [Serializable]
        public class Sample
        {
            public string name = "Sample";
            [Tooltip("How many parts of each paint (same order as the Paint Beakers list) make this colour. This is what the sample's colour is made from.")]
            public int[] parts = new int[5];
            [Tooltip("The Hand Mix Studio only: any colour the player mixes counts, there is no colour to match.")]
            public bool anyColor;
        }

        public RoomZone zone;
        [Tooltip("The paints you pour from. Their Colour is the paint.")]
        public LabBeaker[] paintBeakers;
        public Sample[] samples;
        [Tooltip("One beaker per sample, in the same order as the Samples list.")]
        public LabBeaker[] sampleBeakers;
        public LabBeaker flask;
        [Tooltip("Colour of the little drops that fall into the flask (an unlit material).")]
        public Material dropMaterial;

        [Header("Rules")]
        [Tooltip("How many parts fit in the flask.")]
        public int capacity = 12;
        [Tooltip("Colour distance (Delta E) that counts as a match. About 2 is hard to see, 10 is clearly different.")]
        public float matchDeltaE = 7f;
        [Tooltip("Colour distance at or beyond which the closeness bar reads empty.")]
        public float farDeltaE = 45f;
        public Key useKey = Key.F;
        public float reach = 3.2f;
        public float pourSeconds = 0.35f;

        public int SampleCount => samples != null ? samples.Length : 0;
        public int SolvedCount { get; private set; }
        public bool AllSolved => SampleCount > 0 && SolvedCount >= SampleCount;
        public event Action SolvedEvent;

        bool[] solved;
        int[] flaskParts;
        Color[] sampleColors;
        float attemptStart = -1f; // for comparing with the Hand Mix room: time and pours per sample (logged on a match)
        int pours;
        bool busy;            // a drop is falling or a match is being shown
        bool barShown;
        bool statusShown;
        Camera lookCamera;
        LabBeaker looked;
        SelectionDrawMode drawMode;
        SculptStudio studio;

        int FlaskTotal
        {
            get
            {
                int total = 0;
                foreach (int p in flaskParts) total += p;
                return total;
            }
        }

        void Start()
        {
            int paints = paintBeakers != null ? paintBeakers.Length : 0;
            flaskParts = new int[paints];
            solved = new bool[SampleCount];
            sampleColors = new Color[SampleCount];

            var paintColors = PaintColors();
            for (int i = 0; i < SampleCount; i++)
            {
                sampleColors[i] = MixColor(paintColors, samples[i].parts);
                if (sampleBeakers != null && i < sampleBeakers.Length && sampleBeakers[i] != null)
                {
                    sampleBeakers[i].label = samples[i].name;
                    sampleBeakers[i].color = sampleColors[i];
                    sampleBeakers[i].Snap();
                }
            }

            if (flask != null)
            {
                flask.fill = 0f;
                flask.Snap();
            }
        }

        // ---------- colour maths ----------

        List<Color> PaintColors()
        {
            var list = new List<Color>();
            if (paintBeakers != null)
            {
                foreach (LabBeaker b in paintBeakers) list.Add(b != null ? b.color : Color.white);
            }
            return list;
        }

        // The colour of a recipe: the paints are blended in order, each weighted by its parts, so the result only
        // depends on the amounts. (Also used by the scene builder, so the sample shown in the editor is the right colour.)
        public static Color MixColor(IList<Color> paints, IList<int> parts)
        {
            Color result = Color.white;
            float weight = 0f;
            for (int i = 0; i < paints.Count && i < parts.Count; i++)
            {
                if (parts[i] <= 0) continue;
                if (weight <= 0f)
                {
                    result = paints[i];
                    weight = parts[i];
                }
                else
                {
                    result = PaintMix.Mix(result, paints[i], parts[i] / (weight + parts[i]));
                    weight += parts[i];
                }
            }
            result.a = 1f;
            return result;
        }

        // ---------- every frame ----------

        void Update()
        {
            LineRoomUI ui = LineRoomUI.Instance;
            if (zone == null || !zone.PlayerInside || LineRoomUI.Busy || !CanInteract())
            {
                ClearLook();
                ClearUI();
                return;
            }

            UpdateLook(ui);
            UpdateStatus(ui);
        }

        bool CanInteract()
        {
            if (drawMode == null) drawMode = FindAnyObjectByType<SelectionDrawMode>();
            if (studio == null) studio = FindAnyObjectByType<SculptStudio>();
            if ((drawMode != null && drawMode.IsActive) || (studio != null && studio.IsActive)) return false;
            if (DialogueRunner.Instance != null && DialogueRunner.Instance.IsActive) return false;
            return FirstPersonController.InputCaptured;
        }

        void UpdateLook(LineRoomUI ui)
        {
            if (lookCamera == null) lookCamera = Camera.main;
            LabBeaker target = null;
            if (lookCamera != null)
            {
                Ray ray = lookCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                if (Physics.Raycast(ray, out RaycastHit hit, reach, ~0, QueryTriggerInteraction.Ignore))
                {
                    target = hit.collider.GetComponentInParent<LabBeaker>();
                }
            }

            if (target != looked)
            {
                if (looked != null) looked.SetHighlight(false);
                looked = target;
                if (looked != null) looked.SetHighlight(true);
            }

            if (looked == null)
            {
                ui.SetPrompt(this, "");
                return;
            }

            ui.SetPrompt(this, PromptFor(looked));

            Keyboard keyboard = Keyboard.current;
            Gamepad pad = Gamepad.current;
            bool pressed = (keyboard != null && keyboard[useKey].wasPressedThisFrame) || (pad != null && pad.buttonWest.wasPressedThisFrame);
            if (pressed && !busy) Use(looked, ui);
        }

        string PromptFor(LabBeaker beaker)
        {
            switch (beaker.kind)
            {
                case LabBeaker.Kind.Pigment:
                    return FlaskTotal >= capacity ? $"{beaker.label}: the flask is full" : $"Press {useKey}: add a part of {beaker.label}";
                case LabBeaker.Kind.Flask:
                    return FlaskTotal > 0 ? $"Press {useKey}: empty the flask" : "The mixing flask - add some paint";
                default:
                    return beaker.Solved ? $"{beaker.label} - matched!" : $"Sample: {beaker.label} - mix this colour";
            }
        }

        void Use(LabBeaker beaker, LineRoomUI ui)
        {
            switch (beaker.kind)
            {
                case LabBeaker.Kind.Pigment:
                    if (FlaskTotal >= capacity)
                    {
                        ui.Say("The flask is full - empty it and try again", 2f);
                        return;
                    }
                    int index = Array.IndexOf(paintBeakers, beaker);
                    if (index >= 0) StartCoroutine(Pour(index));
                    break;

                case LabBeaker.Kind.Flask:
                    if (FlaskTotal > 0) EmptyFlask();
                    break;
            }
        }

        // ---------- pouring ----------

        IEnumerator Pour(int paint)
        {
            busy = true;
            if (attemptStart < 0f) attemptStart = Time.time;
            pours++;
            LabBeaker from = paintBeakers[paint];
            from.Wobble();

            // a drop of paint arcs from the beaker's rim to the flask's mouth
            Vector3 start = from.RimPoint;
            Vector3 end = flask != null ? flask.RimPoint : start;
            GameObject drop = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(drop.GetComponent<Collider>());
            drop.transform.localScale = Vector3.one * 0.055f;
            var renderer = drop.GetComponent<MeshRenderer>();
            if (dropMaterial != null) renderer.sharedMaterial = dropMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", from.color);
            renderer.SetPropertyBlock(block);

            for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.05f, pourSeconds))
            {
                Vector3 p = Vector3.Lerp(start, end, t);
                p.y += Mathf.Sin(t * Mathf.PI) * 0.18f;
                drop.transform.position = p;
                yield return null;
            }
            Destroy(drop);

            flaskParts[paint]++;
            RefreshFlask();
            if (flask != null) flask.Wobble();

            if (!CheckMatch()) busy = false;
        }

        void RefreshFlask()
        {
            if (flask == null) return;
            int total = FlaskTotal;
            flask.fill = Mathf.Clamp01(total / (float)Mathf.Max(1, capacity));
            if (total > 0) flask.color = MixColor(PaintColors(), flaskParts);
        }

        public void EmptyFlask()
        {
            for (int i = 0; i < flaskParts.Length; i++) flaskParts[i] = 0;
            RefreshFlask();
            if (flask != null) flask.Wobble();
        }

        // ---------- matching ----------

        // Closest unsolved sample to what is in the flask (-1 if there isn't one)
        int ClosestSample(out float deltaE)
        {
            deltaE = float.MaxValue;
            int best = -1;
            if (FlaskTotal == 0) return best;

            Color mix = MixColor(PaintColors(), flaskParts);
            for (int i = 0; i < SampleCount; i++)
            {
                if (solved[i]) continue;
                float d = PaintMix.DeltaE(mix, sampleColors[i]);
                if (d < deltaE)
                {
                    deltaE = d;
                    best = i;
                }
            }
            return best;
        }

        // returns true if the flask matched a sample (the match is then shown and the flask emptied)
        bool CheckMatch()
        {
            int best = ClosestSample(out float d);
            if (best < 0 || d > matchDeltaE) return false;

            StartCoroutine(Matched(best));
            return true;
        }

        IEnumerator Matched(int index)
        {
            busy = true;
            solved[index] = true;
            SolvedCount++;
            Debug.Log($"ColorLab: {samples[index].name} matched in {Time.time - attemptStart:0.0}s with {pours} pours");
            attemptStart = -1f;
            pours = 0;
            if (sampleBeakers != null && index < sampleBeakers.Length && sampleBeakers[index] != null)
            {
                sampleBeakers[index].ShowSolved(true);
                sampleBeakers[index].Wobble();
            }

            LineRoomUI.Instance.Say(SolvedCount >= SampleCount
                ? "Every sample matched!"
                : $"Matched {samples[index].name}!  ({SolvedCount} / {SampleCount})", 2.5f);

            yield return new WaitForSeconds(1.1f);
            EmptyFlask();
            busy = false;
            if (AllSolved) SolvedEvent?.Invoke();
        }

        // put the lab back to the start (used when a room is reset)
        public void ResetLab()
        {
            StopAllCoroutines();
            busy = false;
            attemptStart = -1f;
            pours = 0;
            SolvedCount = 0;
            if (solved != null) Array.Clear(solved, 0, solved.Length);
            if (sampleBeakers != null)
            {
                foreach (LabBeaker b in sampleBeakers)
                {
                    if (b != null) b.ShowSolved(false);
                }
            }
            if (flaskParts != null) EmptyFlask();
        }

        // ---------- HUD ----------

        void UpdateStatus(LineRoomUI ui)
        {
            int best = ClosestSample(out float d);
            if (AllSolved)
            {
                ui.SetStatus("Colour Lab complete!");
                statusShown = true;
                HideBar(ui);
                return;
            }

            if (best < 0)
            {
                ui.SetStatus($"Matched {SolvedCount} / {SampleCount} - mix paints in the flask to match a sample");
                statusShown = true;
                HideBar(ui);
                return;
            }

            float closeness = d <= matchDeltaE ? 1f : Mathf.Clamp01(1f - d / farDeltaE);
            ui.SetStatus($"Closest sample: {samples[best].name}  ({Mathf.RoundToInt(closeness * 100f)}%)");
            statusShown = true;
            ui.SetBar(closeness, Color.Lerp(new Color(1f, 0.35f, 0.3f), new Color(0.35f, 1f, 0.55f), closeness));
            barShown = true;
        }

        void HideBar(LineRoomUI ui)
        {
            if (!barShown) return;
            ui.SetBar(-1f, Color.white);
            barShown = false;
        }

        void ClearLook()
        {
            if (looked != null) looked.SetHighlight(false);
            looked = null;
        }

        void ClearUI()
        {
            LineRoomUI ui = LineRoomUI.Instance;
            ui.SetPrompt(this, "");
            if (statusShown)
            {
                ui.SetStatus("");
                statusShown = false;
            }
            HideBar(ui);
        }
    }
}
