using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace InkFlip
{
    // The Hand Mix room: the same colour-matching idea as the Colour Lab, but mixed by hand.
    //
    // Look at a sample on the shelf and click it (or press F). A mixing pool opens: pour paint onto it, swirl it around with
    // the mouse (a finger, on a phone), and pick a colour out of it. The big panel on the right is the sample; the circle
    // inside it is your colour, so you can compare them side by side. Let go of a pick that is close enough and it's matched.
    //
    // Mixing three colours earns the Ink Blaster (it is locked until then). The palette station, a machine in the corner,
    // opens the same pool in a second mode: mix a colour, pick it, and click a slot to put it in the ink palette at the
    // bottom of the screen - those are the colours the blaster fires.
    //
    // This is a port of the original colour mixer (ColorMixerScreen / MixingCanvas), built for the first-person rooms.
    public class HandMixGame : MonoBehaviour
    {
        public RoomZone zone;
        public ColorLabGame.Sample[] samples;
        [Tooltip("One beaker per sample, in the same order as the Samples list.")]
        public LabBeaker[] sampleBeakers;
        [Tooltip("The paints on the pool's palette. Same set as the Colour Lab so the samples can be made the same way.")]
        public Color[] paints =
        {
            new Color(0.85f, 0.12f, 0.15f), new Color(0.98f, 0.85f, 0.10f), new Color(0.10f, 0.30f, 0.80f),
            new Color(0.97f, 0.97f, 0.97f), new Color(0.08f, 0.08f, 0.08f),
        };

        [Header("Rules")]
        [Tooltip("Colour distance (Delta E) that counts as a match. About 2 is hard to see, 10 is clearly different.")]
        public float matchDeltaE = 28f;
        [Tooltip("Colour distance at or beyond which the closeness bar reads empty.")]
        public float farDeltaE = 70f;
        [Tooltip("How many samples must be matched to finish the room and earn the Ink Blaster.")]
        public int colorsNeeded = 3;
        [Tooltip("The Ink Blaster on its pedestal: it appears once enough colours are matched.")]
        public AbilityPickup blasterPickup;
        [Tooltip("The machine that opens the ink palette maker.")]
        public Transform paletteStation;
        [Tooltip("For \"any colour\" samples: how different from the blank pool a mixed colour has to be.")]
        public float anyColorDeltaE = 10f;
        public Key useKey = Key.F;
        public float reach = 3.2f;

        public int SampleCount => samples != null ? samples.Length : 0;
        public int SolvedCount { get; private set; }
        public bool AllSolved => SampleCount > 0 && SolvedCount >= SampleCount;
        public int Goal => Mathf.Min(Mathf.Max(1, colorsNeeded), SampleCount);
        public bool GoalReached => SampleCount > 0 && SolvedCount >= Goal;
        public bool IsOpen { get; private set; }
        public event Action SolvedEvent;

        // ---- state ----
        bool[] solved;
        Color[] sampleColors;
        int current = -1;
        Color yourColor;
        bool hasYourColor;
        bool finishing;
        bool paletteMode;
        bool earnedBlaster;
        PaintColorSelector selector;
        float attemptStart;
        int strokes;

        // ---- looking at samples ----
        Camera lookCamera;
        LabBeaker looked;
        SelectionDrawMode drawMode;
        SculptStudio studio;
        FirstPersonController player;
        ToolSwitcher tools;

        // ---- the overlay (built the first time it is needed) ----
        RectTransform root;
        MixingPool pool;
        Image targetPanel;
        Text titleText;
        Image barBack;
        Image ring;
        RectTransform slotRow;
        Image[] slotImages;
        Image yourCircle;
        Image barFill;
        Text statusText;
        Text nameText;
        Image[] potImages;
        Image[] toolImages;
        Text[] toolLabels;
        static readonly Color Paper = new Color(0.96f, 0.95f, 0.92f);
        static readonly Color AnyColorPlaceholder = new Color(0.72f, 0.62f, 1f);

        bool IsAny(int index) => index >= 0 && index < SampleCount && samples[index].anyColor;
        bool CountsAsAny(Color c) => PaintMix.DeltaE(c, Paper) > anyColorDeltaE;

        void Start()
        {
            solved = new bool[SampleCount];
            sampleColors = new Color[SampleCount];
            var paintList = new System.Collections.Generic.List<Color>(paints);
            for (int i = 0; i < SampleCount; i++)
            {
                sampleColors[i] = samples[i].anyColor ? AnyColorPlaceholder : ColorLabGame.MixColor(paintList, samples[i].parts);
                if (sampleBeakers != null && i < sampleBeakers.Length && sampleBeakers[i] != null)
                {
                    sampleBeakers[i].color = sampleColors[i];
                    sampleBeakers[i].Snap();
                }
            }
        }

        void OnDestroy()
        {
            if (IsOpen) Freeze(false);
        }

        // ---------- every frame ----------

        void Update()
        {
            LineRoomUI ui = LineRoomUI.Instance;

            if (IsOpen)
            {
                ClearLook(ui);
                Keyboard keyboard = Keyboard.current;
                if (!finishing && keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Close();
                return;
            }

            if (zone == null || !zone.PlayerInside || LineRoomUI.Busy || !CanInteract())
            {
                ClearLook(ui);
                return;
            }

            UpdateLook(ui);
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
            int index = -1;
            bool atStation = false;
            if (lookCamera != null)
            {
                Ray ray = lookCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                if (Physics.Raycast(ray, out RaycastHit hit, reach, ~0, QueryTriggerInteraction.Ignore))
                {
                    LabBeaker beaker = hit.collider.GetComponentInParent<LabBeaker>();
                    if (beaker != null && sampleBeakers != null) index = Array.IndexOf(sampleBeakers, beaker);
                    if (paletteStation != null && hit.collider.transform.IsChildOf(paletteStation)) atStation = true;
                }
            }

            LabBeaker target = index >= 0 ? sampleBeakers[index] : null;
            if (target != looked)
            {
                if (looked != null) looked.SetHighlight(false);
                looked = target;
                if (looked != null) looked.SetHighlight(true);
            }

            if (index < 0 && !atStation)
            {
                ui.SetPrompt(this, "");
                return;
            }

            if (atStation) ui.SetPrompt(this, $"Click or press {useKey}: make your ink palette");
            else ui.SetPrompt(this, solved[index]
                ? $"{samples[index].name} - matched!"
                : IsAny(index) ? $"Click or press {useKey}: mix any colour you like"
                : $"Click or press {useKey}: mix {samples[index].name} by hand");

            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            bool pressed = (keyboard != null && keyboard[useKey].wasPressedThisFrame)
                           || (mouse != null && mouse.leftButton.wasPressedThisFrame)
                           || (Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame);
            if (!pressed) return;

            if (atStation) Open(-1);
            else if (solved[index]) ui.Say("Already matched - try another sample", 1.8f);
            else Open(index);
        }

        void ClearLook(LineRoomUI ui)
        {
            if (looked != null) looked.SetHighlight(false);
            looked = null;
            ui.SetPrompt(this, "");
        }

        // ---------- opening and closing ----------

        void Open(int index)
        {
            if (selector == null) selector = FindAnyObjectByType<PaintColorSelector>();
            EnsureUI();
            current = index;
            paletteMode = index < 0;
            hasYourColor = false;
            finishing = false;
            attemptStart = Time.time;
            strokes = 0;

            if (paletteMode)
            {
                targetPanel.color = new Color(0.25f, 0.21f, 0.32f);
                titleText.text = "Make your ink palette";
                nameText.text = "Pick a colour, then click a slot";
                RefreshSlots();
            }
            else
            {
                targetPanel.color = sampleColors[index];
                titleText.text = IsAny(index) ? "Make any colour!" : "Match this colour!";
                nameText.text = IsAny(index) ? "Mix anything you like, then pick it" : samples[index].name;
            }
            barBack.gameObject.SetActive(!paletteMode);
            slotRow.gameObject.SetActive(paletteMode);
            ring.rectTransform.localScale = Vector3.one * (paletteMode ? 0.8f : 1f);
            yourCircle.color = Paper;
            pool.Clear();
            SelectPaint(0);
            UpdateCloseness();

            root.gameObject.SetActive(true);
            IsOpen = true;
            Freeze(true);
        }

        void Close()
        {
            if (root != null) root.gameObject.SetActive(false);
            IsOpen = false;
            Freeze(false);
        }

        void Freeze(bool frozen)
        {
            if (player == null) player = FindAnyObjectByType<FirstPersonController>();
            if (tools == null) tools = FindAnyObjectByType<ToolSwitcher>();

            LineRoomUI.Busy = frozen;
            if (player != null) player.enabled = !frozen;
            if (tools != null) tools.enabled = !frozen;
            FirstPersonController.SetCursorCaptured(!frozen);
        }

        // ---------- behaviour ----------

        void SelectPaint(int index)
        {
            pool.tool = MixingPool.Tool.Pour;
            pool.pourColor = paints[index];
            for (int i = 0; i < potImages.Length; i++) potImages[i].transform.localScale = Vector3.one * (i == index ? 1.25f : 1f);
            HighlightTool(0);
        }

        void SelectTool(MixingPool.Tool tool)
        {
            pool.tool = tool;
            HighlightTool(tool == MixingPool.Tool.Swirl ? 1 : 2);
            foreach (Image pot in potImages) pot.transform.localScale = Vector3.one;
        }

        void HighlightTool(int active)
        {
            for (int i = 0; i < toolImages.Length; i++)
            {
                bool on = i == active;
                toolImages[i].color = on ? new Color(0.95f, 0.95f, 1f, 0.95f) : new Color(0.1f, 0.1f, 0.12f, 0.8f);
                toolLabels[i].color = on ? new Color(0.12f, 0.12f, 0.16f) : Color.white;
            }
        }

        void OnColorPicked(Color c)
        {
            if (finishing) return;
            yourColor = c;
            hasYourColor = true;
            yourCircle.color = c;
            UpdateCloseness();
        }

        void OnPickReleased()
        {
            if (paletteMode || current < 0) return;
            if (hasYourColor && !finishing && (IsAny(current) ? CountsAsAny(yourColor) : PaintMix.DeltaE(yourColor, sampleColors[current]) <= matchDeltaE))
                StartCoroutine(Finish());
        }

        IEnumerator Finish()
        {
            finishing = true;
            statusText.text = "Matched!";
            yield return new WaitForSeconds(1.2f);

            int index = current;
            solved[index] = true;
            SolvedCount++;
            Debug.Log($"HandMix: {samples[index].name} matched in {Time.time - attemptStart:0.0}s with {strokes} strokes");

            if (sampleBeakers != null && index < sampleBeakers.Length && sampleBeakers[index] != null)
            {
                sampleBeakers[index].ShowSolved(true);
                sampleBeakers[index].Wobble();
            }

            Close();
            if (GoalReached && !earnedBlaster)
            {
                RevealBlaster();
                LineRoomUI.Instance.Say("Colours matched!  The Ink Blaster has appeared -\ngo and take it from the pedestal.", 4.5f);
            }
            else
            {
                LineRoomUI.Instance.Say(AllSolved ? "Every sample matched!" : $"Matched {samples[index].name}!  ({SolvedCount} / {Goal})", 2.5f);
            }
        }

        void UpdateCloseness()
        {
            if (paletteMode)
            {
                statusText.text = hasYourColor ? "Click a slot to put this colour in your palette" : "Pour and swirl, then pick a colour";
                return;
            }
            float closeness = 0f;
            if (hasYourColor)
            {
                if (IsAny(current))
                {
                    closeness = CountsAsAny(yourColor) ? 1f : 0.3f;
                }
                else
                {
                    float dE = PaintMix.DeltaE(yourColor, sampleColors[current]);
                    closeness = dE <= matchDeltaE ? 1f : Mathf.Clamp01(1f - dE / farDeltaE);
                }
            }

            barFill.rectTransform.anchorMax = new Vector2(closeness, 1f);
            barFill.color = Color.Lerp(new Color(1f, 0.4f, 0.35f), new Color(0.4f, 1f, 0.6f), closeness);

            if (finishing) return;
            if (!hasYourColor) statusText.text = IsAny(current) ? "Pour and swirl - any colour works!" : "Pour and swirl to match the colour";
            else if (closeness >= 1f) statusText.text = "Let go to match it!";
            else if (closeness > 0.85f) statusText.text = "So close!";
            else if (closeness > 0.6f) statusText.text = "Getting warmer";
            else statusText.text = "Keep mixing";
        }

        void RevealBlaster()
        {
            if (earnedBlaster) return;
            earnedBlaster = true;
            if (blasterPickup != null) blasterPickup.Reveal();
            else Abilities.Grant(Ability.InkBlaster);
            SolvedEvent?.Invoke();
        }

        // for testing: skip the mixing and make the blaster appear
        public void ForceGoal()
        {
            RevealBlaster();
        }

        // ---------- the palette station ----------

        void RefreshSlots()
        {
            if (selector == null || slotImages == null) return;
            for (int i = 0; i < slotImages.Length && i < selector.palette.Length; i++)
            {
                slotImages[i].color = selector.palette[i].color;
                slotImages[i].transform.localScale = Vector3.one * (i == selector.SelectedIndex ? 1.2f : 1f);
            }
        }

        void StoreInSlot(int slot)
        {
            if (selector == null) return;
            if (!hasYourColor)
            {
                statusText.text = "Pick a colour first - use the Pick tool on the pool!";
                return;
            }
            selector.SetColor(slot, yourColor, "Mix " + (slot + 1));
            selector.Select(slot);
            RefreshSlots();
            statusText.text = $"Saved to slot {slot + 1}!";
        }

        // put the room back to the start (used when a room is reset)
        public void ResetGame()
        {
            StopAllCoroutines();
            if (IsOpen) Close();
            SolvedCount = 0;
            if (solved != null) Array.Clear(solved, 0, solved.Length);
            if (sampleBeakers != null)
            {
                foreach (LabBeaker b in sampleBeakers)
                {
                    if (b != null) b.ShowSolved(false);
                }
            }
        }

        // ---------- building the overlay ----------

        static void Place(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = pivot;
            r.anchoredPosition = position;
            r.sizeDelta = size;
        }

        void EnsureUI()
        {
            if (root != null) return;

            HudFactory.EnsureEventSystem();
            RectTransform canvas = HudFactory.CreateCanvas("HandMixCanvas", transform, 60);
            root = HudFactory.CreateGroup("Root", canvas);

            // dim the room and swallow clicks behind the panels
            Image dim = HudFactory.CreateImage("Dim", root, null, new Color(0f, 0f, 0f, 0.82f), 0f);
            dim.raycastTarget = true;
            dim.rectTransform.anchorMin = Vector2.zero;
            dim.rectTransform.anchorMax = Vector2.one;
            dim.rectTransform.offsetMin = dim.rectTransform.offsetMax = Vector2.zero;

            BuildMixerHalf();
            BuildTargetHalf();
            root.gameObject.SetActive(false);
        }

        void BuildMixerHalf()
        {
            Image panel = HudFactory.CreateImage("MixerPanel", root, null, new Color(0.30f, 0.26f, 0.24f), 0f);
            Place(panel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(900f, 980f));

            // the pool
            var poolObject = new GameObject("Pool", typeof(RectTransform), typeof(RawImage));
            poolObject.transform.SetParent(panel.transform, false);
            Place((RectTransform)poolObject.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(700f, 700f));
            pool = poolObject.AddComponent<MixingPool>();
            pool.onColorPicked = OnColorPicked;
            pool.onPickReleased = OnPickReleased;
            pool.onStroke = () => strokes++;

            // paint pots
            potImages = new Image[paints.Length];
            for (int i = 0; i < paints.Length; i++)
            {
                int index = i;
                Image pot = HudFactory.CreateImage("Pot" + i, panel.transform, HudFactory.Circle, paints[i], 100f);
                Place(pot.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                      new Vector2((i - (paints.Length - 1) * 0.5f) * 150f, -815f), new Vector2(100f, 100f));
                pot.raycastTarget = true;
                var button = pot.gameObject.AddComponent<Button>();
                button.targetGraphic = pot;
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => SelectPaint(index));
                potImages[i] = pot;
            }

            // tools
            string[] names = { "Pour", "Swirl", "Pick" };
            toolImages = new Image[3];
            toolLabels = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                int toolIndex = i;
                Button button = HudFactory.CreateButton("Tool" + names[i], panel.transform, names[i], new Vector2(190f, 64f),
                    () => { if (toolIndex == 0) SelectPaint(CurrentPourIndex()); else SelectTool(toolIndex == 1 ? MixingPool.Tool.Swirl : MixingPool.Tool.Pick); },
                    out Image background, out Text label);
                Place(background.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2((i - 1.5f) * 210f, -930f), new Vector2(190f, 64f));
                label.fontSize = 30;
                toolImages[i] = background;
                toolLabels[i] = label;
            }

            Button clear = HudFactory.CreateButton("Clear", panel.transform, "Clear", new Vector2(190f, 64f), () => { pool.Clear(); hasYourColor = false; yourCircle.color = Paper; UpdateCloseness(); },
                                                   out Image clearBackground, out Text clearLabel);
            Place(clearBackground.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(1.5f * 210f, -930f), new Vector2(190f, 64f));
            clearLabel.fontSize = 30;
        }

        int CurrentPourIndex()
        {
            for (int i = 0; i < paints.Length; i++)
            {
                if (paints[i] == pool.pourColor) return i;
            }
            return 0;
        }

        void BuildTargetHalf()
        {
            targetPanel = HudFactory.CreateImage("TargetPanel", root, null, Color.gray, 0f);
            Place(targetPanel.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-40f, 0f), new Vector2(900f, 980f));

            titleText = HudFactory.CreateText("Title", targetPanel.transform, "Match this colour!", 54, TextAnchor.MiddleCenter);
            Place(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(800f, 80f));

            nameText = HudFactory.CreateText("Name", targetPanel.transform, "", 40, TextAnchor.MiddleCenter);
            Place(nameText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(800f, 60f));

            ring = HudFactory.CreateImage("Ring", targetPanel.transform, HudFactory.Circle, Color.white, 580f);
            Place(ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(580f, 580f));
            yourCircle = HudFactory.CreateImage("YourColor", ring.transform, HudFactory.Circle, Paper, 540f);
            Place(yourCircle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(540f, 540f));

            barBack = HudFactory.CreateImage("BarBack", targetPanel.transform, null, new Color(0f, 0f, 0f, 0.4f), 0f);
            Place(barBack.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 170f), new Vector2(700f, 30f));
            barFill = HudFactory.CreateImage("BarFill", barBack.transform, null, Color.white, 0f);
            barFill.rectTransform.anchorMin = Vector2.zero;
            barFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            barFill.rectTransform.offsetMin = barFill.rectTransform.offsetMax = Vector2.zero;

            statusText = HudFactory.CreateText("Status", targetPanel.transform, "", 40, TextAnchor.MiddleCenter);
            Place(statusText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 100f), new Vector2(800f, 60f));

            // the ink palette slots, for the palette station
            slotRow = HudFactory.CreateGroup("Slots", targetPanel.transform);
            int slotCount = selector != null ? selector.palette.Length : 9;
            slotImages = new Image[slotCount];
            for (int i = 0; i < slotCount; i++)
            {
                int slot = i;
                Image rim = HudFactory.CreateImage("SlotRim" + i, slotRow, HudFactory.Circle, new Color(1f, 1f, 1f, 0.9f), 84f);
                Place(rim.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2((i - (slotCount - 1) * 0.5f) * 94f, 190f), new Vector2(84f, 84f));
                slotImages[i] = HudFactory.CreateImage("Ink", rim.transform, HudFactory.Circle, Color.gray, 72f);
                Place(slotImages[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(72f, 72f));
                rim.raycastTarget = true;
                var slotButton = rim.gameObject.AddComponent<Button>();
                slotButton.transition = Selectable.Transition.None;
                slotButton.targetGraphic = rim;
                slotButton.onClick.AddListener(() => StoreInSlot(slot));
            }
            slotRow.gameObject.SetActive(false);

            Text hint = HudFactory.CreateText("Hint", targetPanel.transform, "Esc to close", 26, TextAnchor.MiddleCenter);
            Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(600f, 40f));

            Button close = HudFactory.CreateButton("Close", targetPanel.transform, "X", new Vector2(80f, 80f), Close, out Image closeBackground, out Text closeLabel);
            Place(closeBackground.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(80f, 80f));
            closeLabel.fontSize = 40;
        }
    }
}
