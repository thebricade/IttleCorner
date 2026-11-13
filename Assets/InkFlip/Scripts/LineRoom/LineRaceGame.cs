using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace InkFlip
{
    // Mini-game 1 of the Line Room: the nanobot race.
    // The room has to be powered first (the line-tracing panel on the wall), then the character asks you to build nanobots.
    // Each one is a ball with up to four legs, all drawn on a special board: draw a leg by dragging out from the ball,
    // then drag googly eyes onto it. When the field is full the robots stand up on the legs you drew and race along the
    // track. Last you draw its speed lines, comic-book style: the shape of that line sets how fast the nanobot is
    // (long and straight is quick, short and curly is slow) and it is flashed behind the robot a few times as it dashes.
    public class LineRaceGame : MonoBehaviour
    {
        public const string GameId = "line_race";

        public FirstPersonController player;
        public ToolSwitcher tools;

        [Tooltip("The race can only start once this puzzle is solved (the room has power). Leave empty for no power needed.")]
        public LineTracePuzzle power;

        [Header("Track (world space)")]
        [Tooltip("Middle of the starting line, on the table top. The racers run along this object's forward direction.")]
        public Transform trackStart;
        public float trackLength = 3.8f;
        public float laneSpacing = 0.3f;
        [Tooltip("How many nanobots there are in all.")]
        public int racerCount = 4;
        [Tooltip("Seconds an average racer needs for the whole track.")]
        public float averageRaceSeconds = 9f;
        [Tooltip("1 = normal speed, lower = the race plays in slow motion (0.5 is half speed).")]
        [Range(0.1f, 1.5f)] public float raceSpeed = 0.65f;
        [Tooltip("Press this to run the same race again once it has finished. The nanobots stay on the table until the next race.")]
        public Key replayKey = Key.R;

        [Header("Nanobot")]
        [Tooltip("Size of the robot in the world: metres for each pixel of the drawing board.")]
        public float metersPerPixel = 0.0006f;
        public int minLegs = 2;
        public int maxLegs = 4;
        [Tooltip("How far a foot swings forward and back, in metres, at the very end of a leg.")]
        public float stride = 0.05f;
        [Tooltip("How high a foot lifts off the table while it steps, in metres.")]
        public float stepHeight = 0.012f;
        [Tooltip("Steps per second.")]
        public float stepsPerSecond = 2.2f;

        [Header("Speed lines")]
        [Tooltip("How many times each nanobot dashes during the race.")]
        public int dashes = 3;
        public float dashSeconds = 0.8f;
        [Tooltip("How much faster a dash is. A bold speed line dashes harder.")]
        public float dashBoost = 0.9f;
        [Tooltip("Seconds each flash of speed lines stays before it is gone.")]
        public float streakSeconds = 0.55f;

        [Header("Look")]
        public Material lineMaterial;
        public Material eyeWhiteMaterial;
        public Material eyePupilMaterial;
        public Color[] racerColors =
        {
            new Color(1f, 0.35f, 0.62f), new Color(0.2f, 0.85f, 0.95f), new Color(1f, 0.85f, 0.2f), new Color(0.45f, 0.9f, 0.4f),
        };
        public string[] racerColorNames = { "Pink", "Cyan", "Yellow", "Green" };

        public bool HasRaced { get; private set; }
        public bool IsOpen => phase == Phase.Draw || phase == Phase.Eyes || phase == Phase.Motion;
        public event Action RaceFinished;

        enum Phase { Idle, Draw, Eyes, Motion, Racing }

        class Leg
        {
            public LineRenderer line;
            public Transform foot;
            public Vector3[] basePoints;
            public float phaseOffset;
        }

        class Racer
        {
            public GameObject root;
            public Transform body;
            public Transform torso;
            public Transform[] pupils;
            public Transform[] eyeWhites;
            public Vector3 torsoRest;
            public readonly List<Leg> legs = new List<Leg>();
            public Material material;
            public float speed;
            public float seed;
            public float distance;
            public float phase;
            public bool finished;
            public bool atRest = true;
            public string name;
            public string kind;
            public Color color;
            public float score;        // 0-1 from the speed line
            public Vector3[] streak;   // the speed line, laid out behind the robot
            public float bodyHeight, bodyRadius;
            public float[] dashAt;
            public int dashIndex;
            public float dashTimer, emitTimer;
        }

        class Streak
        {
            public GameObject root;
            public LineRenderer line;
            public float age, life, width;
        }

        class Eye
        {
            public RectTransform rect;
            public RectTransform pupil;
            public Vector2 pupilPosition;
            public Vector2 pupilVelocity;
            public Vector2 lastPosition;
        }

        Phase phase = Phase.Idle;
        bool pending;
        readonly List<Racer> racers = new List<Racer>();
        readonly List<Racer> finishOrder = new List<Racer>();
        readonly List<Streak> streaks = new List<Streak>();
        float raceTime;

        // draw board
        RectTransform boardRoot;
        RectTransform paper;
        Image bodyImage;
        readonly List<UIStroke> legStrokes = new List<UIStroke>();
        int legCount;
        UIStroke activeStroke;
        UIStroke motionStroke;
        Text titleText;
        Text hintText;
        Button doneButton;
        Text doneLabel;
        readonly Eye[] eyes = new Eye[2];
        bool drawing;
        int draggingEye = -1;

        // the ball sits a little above the middle of the paper so there is room for legs underneath
        static readonly Vector2 BodyCentre = new Vector2(0f, 70f);
        const float BodyRadius = 90f;
        const float LegGrabRange = 60f;      // a leg can start this far outside the ball
        const float MinLegLength = 70f;
        const float EyeRadius = 30f;
        const float PupilRadius = 14f;
        const int LegSamples = 14;
        const float FootRadius = 0.012f;

        void OnEnable() => MiniGameEvents.Requested += OnRequested;
        void OnDisable() => MiniGameEvents.Requested -= OnRequested;

        void OnDestroy()
        {
            ClearRacers();
            if (IsOpen) LineRoomUI.Busy = false;
        }

        void OnRequested(string id)
        {
            if (id == GameId) pending = true;
        }

        // run the finished race again with the same nanobots (also callable from the inspector's right-click menu)
        [ContextMenu("Replay race")]
        public void Replay()
        {
            if (phase != Phase.Idle || racers.Count == 0 || LineRoomUI.Busy) return;
            StartRace();
        }

        void Update()
        {
            if (phase == Phase.Idle && racers.Count > 0 && replayKey != Key.None && Keyboard.current != null
                && Keyboard.current[replayKey].wasPressedThisFrame && FirstPersonController.InputCaptured)
            {
                Replay();
            }

            // wait for the conversation to close, so it doesn't hand control back over our board
            if (pending && (DialogueRunner.Instance == null || !DialogueRunner.Instance.IsActive))
            {
                pending = false;
                if (power != null && !power.Solved)
                {
                    LineRoomUI.Instance.Say("No power in here yet - trace the dotted lines on the wall panel with your paint brush!", 3.5f);
                }
                else
                {
                    Begin();
                }
            }

            switch (phase)
            {
                case Phase.Draw: UpdateDrawing(); break;
                case Phase.Eyes: UpdateEyes(); break;
                case Phase.Motion: UpdateMotion(); break;
                case Phase.Racing: UpdateRace(); break;
            }
            UpdateStreaks();
        }

        // ---------- starting / stopping ----------

        public void Begin()
        {
            if (phase != Phase.Idle || LineRoomUI.Busy || trackStart == null) return;

            ClearRacers();
            HasRaced = false;
            BuildBoard();
            Freeze(true);
            boardRoot.gameObject.SetActive(true);
            StartOver();
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

        void CloseBoard()
        {
            if (boardRoot != null) boardRoot.gameObject.SetActive(false);
            Freeze(false);
            draggingEye = -1;
            drawing = false;
        }

        void Cancel()
        {
            CloseBoard();
            ClearRacers();
            phase = Phase.Idle;
            LineRoomUI.Instance.Say("Come back when you want to race!", 2f);
        }

        // ---------- the draw board ----------

        void BuildBoard()
        {
            if (boardRoot != null) return;

            HudFactory.EnsureEventSystem();
            RectTransform canvas = HudFactory.CreateCanvas("LineRaceBoard", transform, 60);
            boardRoot = canvas;

            Image dim = HudFactory.CreateImage("Dim", canvas, null, new Color(0.05f, 0.04f, 0.12f, 0.88f), 0f);
            Stretch(dim.rectTransform);

            titleText = HudFactory.CreateText("Title", canvas, "", 46, TextAnchor.MiddleCenter);
            Anchor(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(1500f, 80f));

            hintText = HudFactory.CreateText("Hint", canvas, "", 28, TextAnchor.MiddleCenter);
            Anchor(hintText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -135f), new Vector2(1500f, 50f));
            hintText.color = new Color(1f, 1f, 1f, 0.75f);

            Image paperImage = HudFactory.CreateImage("Paper", canvas, null, new Color(0.9f, 0.95f, 1f), 0f);
            paper = paperImage.rectTransform;
            Anchor(paper, new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(1000f, 640f));

            // the body: a ball you draw legs out of
            bodyImage = HudFactory.CreateImage("Body", paper, HudFactory.Circle, Color.white, BodyRadius * 2f);
            bodyImage.rectTransform.anchoredPosition = BodyCentre;
            bodyImage.raycastTarget = false;
            Image bodyRim = HudFactory.CreateImage("Rim", bodyImage.rectTransform, HudFactory.Ring, new Color(0.1f, 0.1f, 0.2f), BodyRadius * 2f);
            bodyRim.rectTransform.anchoredPosition = Vector2.zero;
            bodyRim.raycastTarget = false;

            for (int i = 0; i < maxLegs; i++)
            {
                var strokeObject = new GameObject("Leg" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(UIStroke));
                strokeObject.transform.SetParent(paper, false);
                UIStroke stroke = strokeObject.GetComponent<UIStroke>();
                stroke.raycastTarget = false;
                stroke.color = new Color(0.12f, 0.1f, 0.2f);
                stroke.thickness = 16f;
                Stretch(stroke.rectTransform);
                legStrokes.Add(stroke);
            }
            var motionObject = new GameObject("SpeedLine", typeof(RectTransform), typeof(CanvasRenderer), typeof(UIStroke));
            motionObject.transform.SetParent(paper, false);
            motionStroke = motionObject.GetComponent<UIStroke>();
            motionStroke.raycastTarget = false;
            motionStroke.color = new Color(1f, 0.5f, 0.1f);
            motionStroke.thickness = 12f;
            Stretch(motionStroke.rectTransform);

            // the rim is drawn after the legs so the hips tuck neatly under it
            bodyImage.transform.SetAsLastSibling();

            for (int i = 0; i < 2; i++) eyes[i] = BuildEye(paper, i);

            Button clear = HudFactory.CreateButton("StartOver", canvas, "Start over", new Vector2(300f, 80f), StartOver, out _, out Text clearLabel);
            clearLabel.fontSize = 30;
            Anchor((RectTransform)clear.transform, new Vector2(0.5f, 0f), new Vector2(-330f, 70f), new Vector2(300f, 80f));

            doneButton = HudFactory.CreateButton("Done", canvas, "", new Vector2(420f, 80f), OnDone, out _, out doneLabel);
            doneLabel.fontSize = 30;
            Anchor((RectTransform)doneButton.transform, new Vector2(0.5f, 0f), new Vector2(180f, 70f), new Vector2(420f, 80f));

            boardRoot.gameObject.SetActive(false);
        }

        Eye BuildEye(RectTransform parent, int index)
        {
            Image white = HudFactory.CreateImage("Eye" + index, parent, HudFactory.Circle, Color.white, EyeRadius * 2f);
            Image rim = HudFactory.CreateImage("Rim", white.rectTransform, HudFactory.Ring, new Color(0.1f, 0.1f, 0.15f), EyeRadius * 2f);
            rim.rectTransform.anchoredPosition = Vector2.zero;
            Image pupil = HudFactory.CreateImage("Pupil", white.rectTransform, HudFactory.Circle, new Color(0.08f, 0.08f, 0.1f), PupilRadius * 2f);
            return new Eye { rect = white.rectTransform, pupil = pupil.rectTransform };
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        static void Anchor(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        Color CurrentColor(int index) =>
            racerColors != null && racerColors.Length > 0 ? racerColors[index % racerColors.Length] : Color.white;

        void StartOver()
        {
            phase = Phase.Draw;
            legCount = 0;
            activeStroke = null;
            foreach (UIStroke stroke in legStrokes)
            {
                stroke.points.Clear();
                stroke.Changed();
            }
            motionStroke.points.Clear();
            motionStroke.Changed();
            drawing = false;
            draggingEye = -1;
            foreach (Eye eye in eyes) eye.rect.gameObject.SetActive(false);
            bodyImage.color = CurrentColor(racers.Count);
            titleText.text = $"Nanobot {racers.Count + 1} of {racerCount}:  draw its legs";
            RefreshLegUi();
        }

        void RefreshLegUi()
        {
            bool enough = legCount >= minLegs;
            doneButton.interactable = enough;
            doneLabel.text = enough ? "Fewer legs - add eyes now" : $"Draw at least {minLegs} legs";
            hintText.text = $"Drag out from the ball to draw a leg  ({legCount} / {maxLegs}).  Each one you let go of counts. Long legs take big steps!";
        }

        void OnDone()
        {
            if (phase == Phase.Draw && legCount >= minLegs) EnterEyes();
            else if (phase == Phase.Eyes) EnterMotion();
        }

        void UpdateDrawing()
        {
            if (CheckCancel()) return;

            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 screen = mouse.position.ReadValue();

            if (mouse.leftButton.wasPressedThisFrame && legCount < maxLegs
                && RectTransformUtility.RectangleContainsScreenPoint(paper, screen, null))
            {
                Vector2 local = LocalOnPaper(screen);
                if ((local - BodyCentre).magnitude <= BodyRadius + LegGrabRange)
                {
                    drawing = true;
                    activeStroke = legStrokes[legCount];
                    activeStroke.points.Clear();
                    // every leg grows out of the edge of the ball
                    Vector2 outward = local - BodyCentre;
                    if (outward.sqrMagnitude < 1f) outward = Vector2.down;
                    activeStroke.points.Add(BodyCentre + outward.normalized * (BodyRadius - 6f));
                    activeStroke.Changed();
                }
                else
                {
                    hintText.text = "Start each leg at the ball - drag out from its edge!";
                }
            }

            if (drawing && mouse.leftButton.isPressed)
            {
                Vector2 local = LocalOnPaper(screen);
                List<Vector2> points = activeStroke.points;
                bool outsideBall = (local - BodyCentre).magnitude >= BodyRadius - 4f;
                if (outsideBall && Vector2.Distance(points[points.Count - 1], local) >= 5f)
                {
                    points.Add(local);
                    activeStroke.Changed();
                }
            }

            if (drawing && mouse.leftButton.wasReleasedThisFrame)
            {
                drawing = false;
                if (PathLength(activeStroke.points) < MinLegLength || activeStroke.points.Count < 4)
                {
                    activeStroke.points.Clear();
                    activeStroke.Changed();
                    RefreshLegUi();
                    hintText.text = "That leg is too short - make it longer!";
                    return;
                }
                legCount++;
                activeStroke = null;
                if (legCount >= maxLegs) EnterEyes(); // the last leg is the cue for the eyes, no button needed
                else RefreshLegUi();
            }
        }

        Vector2 LocalOnPaper(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(paper, screen, null, out Vector2 local);
            Rect r = paper.rect;
            local.x = Mathf.Clamp(local.x, r.xMin + 10f, r.xMax - 10f);
            local.y = Mathf.Clamp(local.y, r.yMin + 10f, r.yMax - 10f);
            return local;
        }

        void EnterEyes()
        {
            phase = Phase.Eyes;
            eyes[0].rect.anchoredPosition = BodyCentre + new Vector2(-95f, 150f);
            eyes[1].rect.anchoredPosition = BodyCentre + new Vector2(95f, 150f);
            foreach (Eye eye in eyes)
            {
                eye.rect.gameObject.SetActive(true);
                eye.lastPosition = eye.rect.anchoredPosition;
                eye.pupilPosition = Vector2.zero;
                eye.pupilVelocity = Vector2.zero;
            }

            doneButton.interactable = true;
            doneLabel.text = "Next: speed lines";
            hintText.text = "Drag the googly eyes onto your nanobot.";
        }

        void EnterMotion()
        {
            phase = Phase.Motion;
            drawing = false;
            draggingEye = -1;
            titleText.text = $"Nanobot {racers.Count + 1} of {racerCount}:  draw its speed lines";
            doneButton.interactable = false;
            doneLabel.text = racers.Count + 1 >= racerCount ? "Let go to start the race" : "Let go to add it to the race";
            hintText.text = "Draw the lines that flash behind it when it dashes! Long, straight, bold = fast.  Short and curly = slow.";
        }

        // one stroke anywhere on the paper; letting go counts, and the nanobot joins the race
        void UpdateMotion()
        {
            if (CheckCancel()) return;

            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 screen = mouse.position.ReadValue();

            if (mouse.leftButton.wasPressedThisFrame && motionStroke.points.Count == 0
                && RectTransformUtility.RectangleContainsScreenPoint(paper, screen, null))
            {
                drawing = true;
            }

            if (drawing && mouse.leftButton.isPressed)
            {
                Vector2 local = LocalOnPaper(screen);
                List<Vector2> points = motionStroke.points;
                if (points.Count == 0 || Vector2.Distance(points[points.Count - 1], local) >= 5f)
                {
                    points.Add(local);
                    motionStroke.Changed();
                }
            }

            if (drawing && mouse.leftButton.wasReleasedThisFrame)
            {
                drawing = false;
                if (PathLength(motionStroke.points) < 120f || motionStroke.points.Count < 6)
                {
                    motionStroke.points.Clear();
                    motionStroke.Changed();
                    hintText.text = "That line is too short - make it longer!";
                    return;
                }
                AddRacerFromBoard();
            }
        }

        void UpdateEyes()
        {
            if (CheckCancel()) return;

            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 screen = mouse.position.ReadValue();
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    draggingEye = -1;
                    float best = EyeRadius * EyeRadius * 2.5f;
                    for (int i = 0; i < 2; i++)
                    {
                        RectTransformUtility.ScreenPointToLocalPointInRectangle(paper, screen, null, out Vector2 local);
                        float d = (eyes[i].rect.anchoredPosition - local).sqrMagnitude;
                        if (d < best)
                        {
                            best = d;
                            draggingEye = i;
                        }
                    }
                }
                if (!mouse.leftButton.isPressed) draggingEye = -1;
                if (draggingEye >= 0) eyes[draggingEye].rect.anchoredPosition = LocalOnPaper(screen);
            }

            foreach (Eye eye in eyes) JiggleEye(eye);
        }

        // the pupil is a loose ball in the eye: it swings when the eye is moved and settles toward the bottom
        void JiggleEye(Eye eye)
        {
            float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            Vector2 position = eye.rect.anchoredPosition;
            Vector2 moved = position - eye.lastPosition;
            eye.lastPosition = position;

            eye.pupilVelocity += -moved * 40f;                            // inertia
            eye.pupilVelocity += (Vector2.down * 10f - eye.pupilPosition) * 90f * dt; // gravity-ish spring
            eye.pupilVelocity *= Mathf.Max(0f, 1f - 5f * dt);
            eye.pupilPosition += eye.pupilVelocity * dt;

            float limit = EyeRadius - PupilRadius - 2f;
            if (eye.pupilPosition.magnitude > limit)
            {
                eye.pupilPosition = eye.pupilPosition.normalized * limit;
                eye.pupilVelocity *= 0.4f;
            }
            eye.pupil.anchoredPosition = eye.pupilPosition;
        }

        bool CheckCancel()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                Cancel();
                return true;
            }
            return false;
        }

        static float PathLength(List<Vector2> points)
        {
            float length = 0f;
            for (int i = 1; i < points.Count; i++) length += Vector2.Distance(points[i - 1], points[i]);
            return length;
        }

        // ---------- turning a drawing into a nanobot ----------

        void AddRacerFromBoard()
        {
            int index = racers.Count;
            var legs = new List<List<Vector2>>();
            for (int i = 0; i < legCount; i++) legs.Add(new List<Vector2>(legStrokes[i].points));

            Racer racer = CreateRacer(index, legs, eyes[0].rect.anchoredPosition, eyes[1].rect.anchoredPosition, new List<Vector2>(motionStroke.points));
            racers.Add(racer);

            if (racers.Count >= racerCount)
            {
                CloseBoard();
                StartRace();
            }
            else
            {
                LineRoomUI.Instance.Say($"{racer.name} {racer.kind} joined the race!", 1.5f);
                StartOver();
            }
        }

        static string KindName(int legs)
        {
            switch (legs)
            {
                case 2: return "Biped";
                case 3: return "Tripod";
                case 4: return "Quadbot";
                default: return "Nanobot";
            }
        }

        Racer CreateRacer(int index, List<List<Vector2>> drawnLegs, Vector2 eyeA, Vector2 eyeB, List<Vector2> motion)
        {
            float s = metersPerPixel;
            float bodyR = BodyRadius * s;

            // resample every leg, find the lowest point of the whole drawing so the robot stands on it
            var legPoints = new List<List<Vector2>>();
            float minY = BodyCentre.y - BodyRadius;
            float totalLength = 0f;
            foreach (List<Vector2> drawn in drawnLegs)
            {
                List<Vector2> even = Resample(drawn, LegSamples);
                legPoints.Add(even);
                totalLength += PathLength(even);
                foreach (Vector2 p in even) minY = Mathf.Min(minY, p.y);
            }
            float averageLength = totalLength / Mathf.Max(1, legPoints.Count) * s;

            // alternate the legs front and back (left to right) so they don't walk through each other
            var order = new List<int>();
            for (int i = 0; i < legPoints.Count; i++) order.Add(i);
            order.Sort((a, b) => MeanX(legPoints[a]).CompareTo(MeanX(legPoints[b])));

            var racer = new Racer
            {
                seed = UnityEngine.Random.value * 100f,
                name = racerColorNames != null && index < racerColorNames.Length ? racerColorNames[index] : "Racer " + (index + 1),
                kind = KindName(legPoints.Count),
            };
            Color color = CurrentColor(index);
            racer.color = color;

            // the speed line sets the speed: long and straight is quick, short and curly is slow
            float motionLength = PathLength(motion);
            float chord = Vector2.Distance(motion[0], motion[motion.Count - 1]);
            float straightness = motionLength > 1f ? chord / motionLength : 0f;
            float lengthScore = Mathf.Clamp01(motionLength / 500f);
            racer.score = 0.55f * straightness + 0.45f * lengthScore;
            racer.speed = trackLength / averageRaceSeconds * (0.8f + 0.55f * racer.score);
            BuildStreakShape(racer, motion, lengthScore);
            racer.dashAt = new float[Mathf.Max(0, dashes)];
            for (int d = 0; d < racer.dashAt.Length; d++)
            {
                float fraction = 0.12f + 0.75f * d / Mathf.Max(1, racer.dashAt.Length) + UnityEngine.Random.Range(-0.04f, 0.04f);
                racer.dashAt[d] = trackLength * fraction;
            }

            racer.root = new GameObject("Nanobot_" + racer.name);
            racer.root.transform.SetParent(transform, false);
            racer.root.transform.rotation = Quaternion.LookRotation(trackStart.forward, Vector3.up);
            racer.root.transform.position = LanePosition(index, 0f);

            racer.body = new GameObject("Body").transform;
            racer.body.SetParent(racer.root.transform, false);
            racer.torso = new GameObject("Torso").transform;
            racer.torso.SetParent(racer.body, false);

            // the board is a front view: x is sideways, y is up, the robot faces +z (along the track)
            Vector3 bodyCentre = new Vector3(0f, (BodyCentre.y - minY) * s, 0f);
            racer.material = MakeMaterial(lineMaterial, color);
            Transform ball = MakeBall("Ball", racer.torso, bodyCentre, bodyR * 2f, lineMaterial, color);
            ball.GetComponent<Renderer>().sharedMaterial = racer.material;
            racer.torsoRest = racer.torso.localPosition;
            racer.bodyHeight = bodyCentre.y;
            racer.bodyRadius = bodyR;

            Color legColor = Color.Lerp(color, Color.black, 0.35f);
            float depth = bodyR * 0.55f;
            for (int k = 0; k < order.Count; k++)
            {
                List<Vector2> even = legPoints[order[k]];
                float side = k % 2 == 0 ? 1f : -1f;
                var leg = new Leg { phaseOffset = k % 2 == 0 ? 0f : Mathf.PI };

                leg.basePoints = new Vector3[LegSamples];
                for (int i = 0; i < LegSamples; i++)
                {
                    float t = i / (float)(LegSamples - 1);
                    float z = side * depth * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.3f)); // fans out from the hip
                    // spider legs: bend every leg so its foot lands on the table, whatever height it was drawn at
                    float footDrop = (even[LegSamples - 1].y - minY) * s;
                    leg.basePoints[i] = new Vector3((even[i].x - BodyCentre.x) * s, (even[i].y - minY) * s - footDrop * t, z);
                }

                var lineObject = new GameObject("Leg" + k);
                lineObject.transform.SetParent(racer.body, false);
                leg.line = lineObject.AddComponent<LineRenderer>();
                leg.line.useWorldSpace = false;
                leg.line.positionCount = LegSamples;
                leg.line.SetPositions(leg.basePoints);
                leg.line.widthMultiplier = 0.016f;
                leg.line.numCapVertices = 5;
                leg.line.numCornerVertices = 4;
                leg.line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                leg.line.sharedMaterial = MakeMaterial(lineMaterial, legColor);

                leg.foot = MakeBall("Foot", racer.body, leg.basePoints[LegSamples - 1] + Vector3.up * FootRadius, FootRadius * 2f, lineMaterial, legColor);
                racer.legs.Add(leg);
            }

            // eyes sit on the front of the ball
            racer.pupils = new Transform[2];
            racer.eyeWhites = new Transform[2];
            Vector2[] eyeBoard = { eyeA, eyeB };
            for (int i = 0; i < 2; i++)
            {
                Vector2 rel = (eyeBoard[i] - BodyCentre) * s;
                // keep the eye on the ball: pull it in if it was dropped beside it
                float flat = rel.magnitude;
                float maxFlat = bodyR * 0.92f;
                if (flat > maxFlat) rel *= maxFlat / flat;
                float z = Mathf.Sqrt(Mathf.Max(0f, bodyR * bodyR - rel.x * rel.x - rel.y * rel.y));
                float eyeSize = EyeRadius * 2f * s * 0.95f;
                Vector3 local = bodyCentre + new Vector3(rel.x, rel.y, z);
                Transform white = MakeBall("EyeWhite", racer.torso, local, eyeSize, eyeWhiteMaterial, Color.white);
                racer.eyeWhites[i] = white;
                racer.pupils[i] = MakeBall("Pupil", white, new Vector3(0f, -0.1f, 0.38f), 0.55f, eyePupilMaterial, new Color(0.05f, 0.05f, 0.07f));
            }
            return racer;
        }

        // lay the drawn speed line out behind the robot: its end-to-end direction runs back along the track
        void BuildStreakShape(Racer racer, List<Vector2> motion, float lengthScore)
        {
            const int Count = 16;
            List<Vector2> even = Resample(motion, Count);
            Vector2 first = even[0];
            Vector2 along = even[Count - 1] - first;
            if (along.magnitude < 1f) along = Vector2.right;
            along.Normalize();
            Vector2 across = new Vector2(-along.y, along.x);

            float maxU = 1f;
            foreach (Vector2 p in even) maxU = Mathf.Max(maxU, Mathf.Abs(Vector2.Dot(p - first, along)));
            float scale = (0.16f + 0.3f * lengthScore) / maxU;

            racer.streak = new Vector3[Count];
            for (int i = 0; i < Count; i++)
            {
                Vector2 d = even[i] - first;
                float u = Vector2.Dot(d, along) * scale;
                float v = Mathf.Clamp(Vector2.Dot(d, across) * scale, -0.15f, 0.15f);
                racer.streak[i] = new Vector3(0f, v, -u);
            }
        }

        static readonly Vector3[] StreakOffsets = { new Vector3(0f, 0f, 0f), new Vector3(-0.07f, 0.03f, -0.02f), new Vector3(0.07f, -0.025f, -0.04f) };
        static readonly float[] StreakLengths = { 1f, 0.8f, 0.65f };

        // a few copies of the speed line flash out behind the robot and shrink away, left in the world as it runs on
        void SpawnStreaks(Racer racer)
        {
            for (int k = 0; k < StreakOffsets.Length; k++)
            {
                var streak = new Streak { life = streakSeconds, width = 0.014f };
                streak.root = new GameObject("SpeedLine");
                streak.root.transform.SetParent(transform, false);
                streak.line = streak.root.AddComponent<LineRenderer>();
                streak.line.useWorldSpace = true;
                streak.line.positionCount = racer.streak.Length;
                for (int i = 0; i < racer.streak.Length; i++)
                {
                    Vector3 local = new Vector3(StreakOffsets[k].x, racer.bodyHeight + StreakOffsets[k].y, -racer.bodyRadius * 1.1f + StreakOffsets[k].z)
                                    + Vector3.Scale(racer.streak[i], new Vector3(1f, StreakLengths[k], StreakLengths[k]));
                    streak.line.SetPosition(i, racer.root.transform.TransformPoint(local));
                }
                streak.line.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.1f));
                streak.line.widthMultiplier = streak.width;
                streak.line.numCapVertices = 3;
                streak.line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                streak.line.sharedMaterial = racer.material;
                streaks.Add(streak);
            }
        }

        void UpdateStreaks()
        {
            for (int i = streaks.Count - 1; i >= 0; i--)
            {
                Streak streak = streaks[i];
                streak.age += Time.deltaTime;
                if (streak.age >= streak.life)
                {
                    Destroy(streak.root);
                    streaks.RemoveAt(i);
                    continue;
                }
                streak.line.widthMultiplier = streak.width * (1f - streak.age / streak.life);
            }
        }

        static float MeanX(List<Vector2> points)
        {
            float sum = 0f;
            foreach (Vector2 p in points) sum += p.x;
            return sum / Mathf.Max(1, points.Count);
        }

        static Material MakeMaterial(Material template, Color color)
        {
            Material material;
            if (template != null) material = new Material(template);
            else
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                material = new Material(shader);
            }
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            return material;
        }

        static Transform MakeBall(string name, Transform parent, Vector3 localPosition, float size, Material template, Color color)
        {
            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = name;
            Destroy(ball.GetComponent<Collider>());
            ball.transform.SetParent(parent, false);
            ball.transform.localPosition = localPosition;
            ball.transform.localScale = Vector3.one * size;
            var renderer = ball.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.sharedMaterial = MakeMaterial(template, color);
            return ball.transform;
        }

        // points spaced evenly along the drawn line
        static List<Vector2> Resample(List<Vector2> points, int count)
        {
            float total = PathLength(points);
            var result = new List<Vector2>(count) { points[0] };
            if (total < 0.001f)
            {
                for (int i = 1; i < count; i++) result.Add(points[0]);
                return result;
            }

            float step = total / (count - 1);
            float carried = 0f;
            int segment = 1;
            Vector2 previous = points[0];
            while (result.Count < count - 1 && segment < points.Count)
            {
                Vector2 target = points[segment];
                float length = Vector2.Distance(previous, target);
                if (carried + length >= step)
                {
                    float t = (step - carried) / length;
                    previous = Vector2.Lerp(previous, target, t);
                    result.Add(previous);
                    carried = 0f;
                }
                else
                {
                    carried += length;
                    previous = target;
                    segment++;
                }
            }
            while (result.Count < count) result.Add(points[points.Count - 1]);
            return result;
        }

        // ---------- the race ----------

        Vector3 LanePosition(int lane, float distance)
        {
            float across = (lane - (racerCount - 1) * 0.5f) * laneSpacing;
            return trackStart.position + trackStart.right * across + trackStart.forward * distance + Vector3.up * 0.02f;
        }

        void StartRace()
        {
            phase = Phase.Racing;
            raceTime = 0f;
            finishOrder.Clear();
            foreach (Racer racer in racers)
            {
                racer.finished = false;
                racer.distance = 0f;
                racer.dashIndex = 0;
                racer.dashTimer = 0f;
                racer.atRest = false;
            }
            LineRoomUI.Instance.Say("Ready... set... GO!", 1.6f);
        }

        void UpdateRace()
        {
            float dt = Time.deltaTime * raceSpeed;
            raceTime += dt;
            int running = 0;

            for (int i = 0; i < racers.Count; i++)
            {
                Racer racer = racers[i];
                if (!racer.finished)
                {
                    // each racer surges and lags a bit, so the legs only tilt the odds
                    float surge = 1f + 0.15f * (Mathf.PerlinNoise(raceTime * 0.7f, racer.seed) - 0.5f) * 2f;
                    // a little push at each step
                    float push = 1f + 0.25f * Mathf.Cos(racer.phase * 2f);

                    // a dash: the speed lines flash and the nanobot bursts forward, harder with a bolder line
                    if (racer.dashIndex < racer.dashAt.Length && racer.distance >= racer.dashAt[racer.dashIndex])
                    {
                        racer.dashIndex++;
                        racer.dashTimer = dashSeconds;
                        racer.emitTimer = 0f;
                    }
                    float dash = 1f;
                    if (racer.dashTimer > 0f)
                    {
                        racer.dashTimer -= dt;
                        dash = 1f + dashBoost * (0.4f + racer.score);
                        racer.emitTimer -= dt;
                        if (racer.emitTimer <= 0f)
                        {
                            SpawnStreaks(racer);
                            racer.emitTimer = 0.22f;
                        }
                    }

                    racer.distance += racer.speed * Mathf.Max(0.15f, surge) * push * dash * dt;
                    racer.phase += dt * stepsPerSecond * dash * Mathf.PI * 2f;

                    if (racer.distance >= trackLength)
                    {
                        racer.distance = trackLength;
                        racer.finished = true;
                        finishOrder.Add(racer);
                    }
                }
                if (!racer.finished) running++;
                Animate(racer, i);
            }

            if (running == 0) FinishRace();
        }

        void Animate(Racer racer, int lane)
        {
            racer.root.transform.position = LanePosition(lane, racer.distance);

            if (!racer.finished)
            {
                float bob = Mathf.Abs(Mathf.Sin(racer.phase)) * 0.003f;
                racer.torso.localPosition = racer.torsoRest + Vector3.up * bob;

                foreach (Leg leg in racer.legs)
                {
                    float theta = racer.phase + leg.phaseOffset;
                    float swing = Mathf.Sin(theta);              // -1 foot back .. +1 foot forward
                    float lift = Mathf.Max(0f, Mathf.Cos(theta)); // off the table while it swings forward
                    for (int i = 0; i < leg.basePoints.Length; i++)
                    {
                        float t = i / (float)(leg.basePoints.Length - 1);
                        Vector3 p = leg.basePoints[i];
                        p.z += swing * stride * t;
                        p.y += lift * stepHeight * t * t + bob * (1f - t);
                        leg.line.SetPosition(i, p);
                        if (i == leg.basePoints.Length - 1) leg.foot.localPosition = p + Vector3.up * FootRadius;
                    }
                }
                racer.atRest = false;
            }
            else if (!racer.atRest)
            {
                // stand still on the legs that were drawn
                racer.torso.localPosition = racer.torsoRest;
                foreach (Leg leg in racer.legs)
                {
                    for (int i = 0; i < leg.basePoints.Length; i++) leg.line.SetPosition(i, leg.basePoints[i]);
                    leg.foot.localPosition = leg.basePoints[leg.basePoints.Length - 1] + Vector3.up * FootRadius;
                }
                racer.atRest = true;
            }

            // pupils slosh around while it moves
            bool moving = !racer.finished;
            for (int i = 0; i < racer.pupils.Length; i++)
            {
                float k = moving ? 1f : 0.2f;
                float t = racer.phase * 0.7f + i * 1.7f;
                racer.pupils[i].localPosition = new Vector3(Mathf.Sin(t * 1.3f) * 0.2f * k, -0.1f + Mathf.Cos(t) * 0.15f * k, 0.38f);
            }
        }

        void FinishRace()
        {
            phase = Phase.Idle;
            HasRaced = true;

            Racer winner = finishOrder.Count > 0 ? finishOrder[0] : null;
            if (winner != null)
            {
                LineRoomUI.Instance.Say($"{winner.name} {winner.kind} wins!", 4f);
            }
            RaceFinished?.Invoke();
        }

        void ClearRacers()
        {
            foreach (Streak streak in streaks)
            {
                if (streak.root != null) Destroy(streak.root);
            }
            streaks.Clear();
            foreach (Racer racer in racers)
            {
                if (racer.root != null) Destroy(racer.root);
                if (racer.material != null) Destroy(racer.material);
            }
            racers.Clear();
            finishOrder.Clear();
        }
    }
}
