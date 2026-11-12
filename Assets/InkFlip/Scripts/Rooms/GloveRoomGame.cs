using System;
using System.Collections;
using Unity.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace InkFlip
{
    // The Glove Room, in three steps:
    //   1. DECORATE - a big blank glove stands on a pedestal. Use the pencil to draw all over it.
    //   2. PICK UP  - once it's decorated enough, press F on it. It flies into your hand, becomes your glove (with your
    //                 drawing on it), and the glove is unlocked for the rest of the game.
    //   3. PUZZLE   - now you can carry and resize things. Put something on every switch to finish.
    // Until step 2 the player has no glove anywhere (Abilities keeps it locked).
    public class GloveRoomGame : MonoBehaviour
    {
        public enum Phase { Decorate, Ready, PickingUp, Puzzle, Done }

        public RoomZone zone;
        public SelectionDrawMode drawMode;
        [Tooltip("The big blank glove on the pedestal (a Paintable the pencil can draw on).")]
        public Paintable glove;
        [Tooltip("Everything on the pedestal; it sinks into the floor once the glove has been taken.")]
        public Transform pedestal;
        public LevitationSwitch[] switches;

        [Header("Rules")]
        [Tooltip("How much of the glove has to be drawn on (0-1) before it can be taken.")]
        [Range(0.02f, 0.6f)] public float coverageNeeded = 0.12f;
        [Tooltip("The switches all have to stay pressed this long.")]
        public float solveHoldSeconds = 1.2f;
        public Key useKey = Key.F;
        public float reach = 3.6f;

        public Phase CurrentPhase { get; private set; } = Phase.Decorate;
        public bool Solved => CurrentPhase == Phase.Done;
        public float Coverage { get; private set; }
        public event Action SolvedEvent;

        public float Progress
        {
            get
            {
                switch (CurrentPhase)
                {
                    case Phase.Decorate: return 0.3f * Mathf.Clamp01(Coverage / Mathf.Max(0.01f, coverageNeeded));
                    case Phase.Ready:
                    case Phase.PickingUp: return 0.3f;
                    case Phase.Puzzle: return 0.4f + 0.6f * (switches != null && switches.Length > 0 ? PressedCount / (float)switches.Length : 0f);
                    default: return 1f;
                }
            }
        }

        public int PressedCount
        {
            get
            {
                int n = 0;
                if (switches != null)
                {
                    foreach (LevitationSwitch s in switches)
                    {
                        if (s != null && s.Pressed) n++;
                    }
                }
                return n;
            }
        }

        bool drawingGlove;
        float measureTimer;
        float holdTimer;
        bool statusShown;
        Camera lookCamera;
        Collider gloveCollider;
        Vector3 gloveHome;
        Quaternion gloveHomeRotation;
        Vector3 gloveHomeScale;
        Texture2D readback;

        void Start()
        {
            if (drawMode == null) drawMode = FindAnyObjectByType<SelectionDrawMode>();
            if (drawMode != null) drawMode.ModeChanged += OnDrawModeChanged;

            if (glove != null)
            {
                gloveCollider = glove.GetComponent<Collider>();
                gloveHome = glove.transform.position;
                gloveHomeRotation = glove.transform.rotation;
                gloveHomeScale = glove.transform.localScale;
            }
            if (switches != null)
            {
                foreach (LevitationSwitch s in switches)
                {
                    if (s != null) s.SetArmed(false);
                }
            }
        }

        void OnDestroy()
        {
            if (drawMode != null) drawMode.ModeChanged -= OnDrawModeChanged;
            if (readback != null) Destroy(readback);
        }

        // ---------- every frame ----------

        void Update()
        {
            bool inside = zone != null && zone.PlayerInside;
            LineRoomUI ui = LineRoomUI.Instance;

            if (!inside)
            {
                ClearUI(ui);
                return;
            }

            switch (CurrentPhase)
            {
                case Phase.Decorate: UpdateDecorate(ui); break;
                case Phase.Ready: UpdateReady(ui); break;
                case Phase.Puzzle: UpdatePuzzle(ui); break;
                default: ClearUI(ui); break;
            }
        }

        // ---------- step 1: decorate ----------

        void UpdateDecorate(LineRoomUI ui)
        {
            // while drawing, keep an eye on how much of the glove is covered
            if (drawingGlove)
            {
                measureTimer -= Time.unscaledDeltaTime;
                if (measureTimer <= 0f)
                {
                    measureTimer = 0.5f;
                    Coverage = Measure();
                }
            }

            float fraction = Mathf.Clamp01(Coverage / Mathf.Max(0.01f, coverageNeeded));
            if (drawingGlove)
            {
                // while drawing: remind how to get to the other side
                ui.SetStatus(Coverage >= coverageNeeded
                    ? "Looking great! Press Esc when you're happy with it  (turn it: right-drag or A / D, tip: W / S)"
                    : $"Decorating - {Mathf.RoundToInt(fraction * 100f)}%   (turn it: right-drag or A / D, tip: W / S)");
            }
            else
            {
                ui.SetStatus(Coverage >= coverageNeeded
                    ? "Looking great! Finish drawing when you're happy with it"
                    : $"Decorate the glove with your pencil (C) - {Mathf.RoundToInt(fraction * 100f)}%");
            }
            ui.SetBar(Coverage > 0f ? fraction : -1f, Color.Lerp(new Color(1f, 0.6f, 0.25f), new Color(0.4f, 1f, 0.6f), fraction));
            statusShown = true;
        }

        void OnDrawModeChanged(bool active)
        {
            if (CurrentPhase != Phase.Decorate) return;

            if (active)
            {
                drawingGlove = drawMode != null && drawMode.Target == glove;
                measureTimer = 0f;
                return;
            }

            if (!drawingGlove) return;
            drawingGlove = false;
            Coverage = Measure();

            if (Coverage >= coverageNeeded)
            {
                CurrentPhase = Phase.Ready;
                LineRoomUI.Instance.SetBar(-1f, Color.white);
                LineRoomUI.Instance.Say("Beautiful! Look at the glove and press F", 3f);
            }
            else
            {
                LineRoomUI.Instance.Say($"Keep going - it needs more decoration ({Mathf.RoundToInt(Coverage / coverageNeeded * 100f)}%)", 2.5f);
            }
        }

        // ---------- step 2: pick it up ----------

        void UpdateReady(LineRoomUI ui)
        {
            if (glove != null)
            {
                // the finished glove bobs gently on its pedestal
                float t = Time.time;
                glove.transform.position = gloveHome + Vector3.up * (Mathf.Sin(t * 2f) * 0.04f);
                glove.transform.rotation = gloveHomeRotation * Quaternion.Euler(0f, Mathf.Sin(t * 0.8f) * 12f, 0f);
            }

            ui.SetBar(-1f, Color.white);
            bool looking = LookingAtGlove();
            ui.SetStatus("Your glove is ready!");
            ui.SetPrompt(this, looking ? $"Press {useKey}: pick up your glove" : "");
            statusShown = true;

            Keyboard keyboard = Keyboard.current;
            Gamepad pad = Gamepad.current;
            bool pressed = (keyboard != null && keyboard[useKey].wasPressedThisFrame) || (pad != null && pad.buttonWest.wasPressedThisFrame);
            if (looking && pressed && FirstPersonController.InputCaptured) StartCoroutine(PickUp());
        }

        bool LookingAtGlove()
        {
            if (glove == null || gloveCollider == null) return false;
            if (lookCamera == null) lookCamera = Camera.main;
            if (lookCamera == null) return false;

            Ray ray = lookCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            return gloveCollider.Raycast(ray, out RaycastHit hit, reach);
        }

        IEnumerator PickUp()
        {
            CurrentPhase = Phase.PickingUp;
            LineRoomUI.Instance.SetPrompt(this, "");

            Transform cam = lookCamera != null ? lookCamera.transform : Camera.main.transform;
            Vector3 startPosition = glove.transform.position;
            Quaternion startRotation = glove.transform.rotation;
            Vector3 startScale = glove.transform.localScale;

            // it flies to where the glove sits in your view, shrinking to hand size
            const float Seconds = 1.1f;
            for (float t = 0f; t < 1f; t += Time.deltaTime / Seconds)
            {
                float e = Mathf.SmoothStep(0f, 1f, t);
                Vector3 goal = cam.position + cam.forward * 0.55f + cam.right * 0.2f - cam.up * 0.2f;
                glove.transform.position = Vector3.Lerp(startPosition, goal, e);
                glove.transform.rotation = Quaternion.Slerp(startRotation, cam.rotation * Quaternion.Euler(90f, 0f, 0f), e);
                glove.transform.localScale = Vector3.Lerp(startScale, startScale * 0.17f, e);
                yield return null;
            }

            TakeGlove();
            LineRoomUI.Instance.Say("New ability: Glove!\nPress V, hold left mouse to carry, scroll to resize", 5f);
            StartCoroutine(SinkPedestal());
        }

        // the in-hand glove becomes the decorated one, and the glove is unlocked
        void TakeGlove()
        {
            DressViewModel();
            if (glove != null) glove.Renderer.enabled = false;
            Abilities.Grant(Ability.Glove);
            CurrentPhase = Phase.Puzzle;
            if (switches != null)
            {
                foreach (LevitationSwitch s in switches)
                {
                    if (s != null) s.SetArmed(true);
                }
            }
        }

        // The player's glove (the one in front of the camera) is swapped for a small copy of the big glove, wearing the same drawing.
        // The big glove object stays in the scene (hidden): its paint texture belongs to it.
        void DressViewModel()
        {
            GloveTool tool = FindAnyObjectByType<GloveTool>(FindObjectsInactive.Include);
            if (tool == null || tool.gloveModel == null || glove == null) return;

            foreach (Renderer r in tool.gloveModel.GetComponentsInChildren<Renderer>(true)) r.enabled = false;

            var copy = new GameObject("DecoratedGlove");
            copy.layer = 2; // Ignore Raycast, like the other viewmodel parts
            copy.transform.SetParent(tool.gloveModel, false);
            copy.transform.localPosition = Vector3.zero;
            copy.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // fingers forward, back of the hand up
            copy.transform.localScale = Vector3.one * 0.17f;

            copy.AddComponent<MeshFilter>().sharedMesh = glove.GetComponent<MeshFilter>().sharedMesh;
            var renderer = copy.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = glove.Renderer.sharedMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var block = new MaterialPropertyBlock();
            glove.Renderer.GetPropertyBlock(block); // carries the paint texture
            renderer.SetPropertyBlock(block);
        }

        IEnumerator SinkPedestal()
        {
            if (pedestal == null) yield break;
            Vector3 start = pedestal.position;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 1.0f)
            {
                pedestal.position = start + Vector3.down * (Mathf.SmoothStep(0f, 1f, t) * 1.6f);
                yield return null;
            }
            pedestal.gameObject.SetActive(false);
        }

        // ---------- step 3: the puzzle ----------

        void UpdatePuzzle(LineRoomUI ui)
        {
            int total = switches != null ? switches.Length : 0;
            int pressed = PressedCount;
            ui.SetStatus($"Put something on every switch  ({pressed} / {total})");
            ui.SetBar(-1f, Color.white);
            statusShown = true;

            if (total > 0 && pressed >= total)
            {
                holdTimer += Time.deltaTime;
                if (holdTimer >= solveHoldSeconds) Solve();
            }
            else
            {
                holdTimer = 0f;
            }
        }

        void Solve()
        {
            if (CurrentPhase == Phase.Done) return;
            CurrentPhase = Phase.Done;
            LineRoomUI.Instance.Say("Puzzle solved!", 3f);
            SolvedEvent?.Invoke();
        }

        // Finish everything at once (used when a conversation or the test key completes the room): the player still gets the glove.
        public void SkipToSolved()
        {
            StopAllCoroutines();
            if (CurrentPhase != Phase.Puzzle && CurrentPhase != Phase.Done)
            {
                if (glove != null) glove.Renderer.enabled = false;
                Abilities.Grant(Ability.Glove);
                if (pedestal != null) pedestal.gameObject.SetActive(false);
            }
            CurrentPhase = Phase.Done;
            drawingGlove = false;
            SolvedEvent?.Invoke();
        }

        // ---------- measuring the drawing ----------

        // Fraction of the glove's surface that has pencil on it: the paint mask's alpha, averaged, over the area the mesh covers.
        float Measure()
        {
            if (glove == null || !glove.IsAllocated) return 0f;
            float painted = SumChannel(glove.MaskTexture, useAlpha: true);
            float surface = SumChannel(glove.IslandTexture, useAlpha: false);
            return surface > 0.0001f ? Mathf.Clamp01(painted / surface) : 0f;
        }

        // sums one channel over the texture, after shrinking it 2x (which is an exact average of each 2x2 block)
        float SumChannel(RenderTexture source, bool useAlpha)
        {
            int size = Mathf.Max(64, source.width / 2);
            RenderTexture small = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Graphics.Blit(source, small);

            if (readback == null || readback.width != size)
            {
                if (readback != null) Destroy(readback);
                readback = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            }

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = small;
            readback.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(small);

            NativeArray<Color32> pixels = readback.GetRawTextureData<Color32>();
            float sum = 0f;
            for (int i = 0; i < pixels.Length; i++) sum += useAlpha ? pixels[i].a : pixels[i].r;
            return sum / 255f;
        }

        // ---------- HUD ----------

        void ClearUI(LineRoomUI ui)
        {
            ui.SetPrompt(this, "");
            if (!statusShown) return;
            ui.SetStatus("");
            ui.SetBar(-1f, Color.white);
            statusShown = false;
        }
    }
}
