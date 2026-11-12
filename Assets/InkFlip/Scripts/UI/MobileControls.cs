using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace InkFlip
{
    // Touch controls for phones. Starts by itself on iOS / Android (and in the editor when switched on from
    // Tools > InkFlip > Mobile Controls In Editor, for use with the Device Simulator). Nothing in the scene needs to be set up.
    //
    // How it works: instead of changing every script, this adds a virtual keyboard, mouse and gamepad and feeds them from
    // the touch screen, so everything that already reads Keyboard.current / Mouse.current / Gamepad.current just works.
    //
    //   While playing (mouse "captured"):
    //     left thumb on the stick  - walk              drag anywhere else - look around
    //     ACT   - left mouse button (use the tool: paint, draw, carry with the glove...)
    //     USE   - F (talk, flip the switch, pick things up...)       JUMP - space
    //     ROLL  - right mouse button (the ink blaster's roller)      + / - - the mouse wheel (resize with the glove)
    //     tool buttons top right - Z X C V      < > - previous / next colour (Q / E)
    //   While something is open (a drawing board, draw mode, the sculpt studio, a conversation):
    //     touches work like the mouse pointer, and BACK is Esc.
    public class MobileControls : MonoBehaviour
    {
        public const string EditorPrefKey = "InkFlip.MobileControlsInEditor";

        // true when the touch controls are running; other scripts use it where the cursor can't really be locked
        public static bool Active { get; private set; }

        [Tooltip("Look speed multiplier on top of the mouse sensitivity.")]
        public float lookScale = 1.4f;

        // ---- virtual devices ----
        Keyboard keyboard;
        Mouse mouse;
        Gamepad pad;

        // ---- what the touches currently say ----
        Vector2 stick;
        Vector2 lookDelta;
        Vector2 pendingScroll;
        Vector2 mousePosition;
        bool fireHeld;
        bool rollHeld;
        bool mirrorHeld;
        readonly Dictionary<Key, int> heldKeys = new Dictionary<Key, int>();
        bool keysDirty;

        // what was last sent, so events are only queued when something changed
        Vector2 sentStick;
        Vector2 sentMousePosition;
        bool sentLeft, sentRight;
        bool sentDelta, sentScroll;
        bool firstMouseSend = true;

        // ---- fingers ----
        enum Owner { Stick, Button, Look, Mirror, Ui }
        class Finger
        {
            public Owner owner;
            public Control control;
        }
        readonly Dictionary<int, Finger> fingers = new Dictionary<int, Finger>();
        readonly List<int> gone = new List<int>();
        readonly HashSet<int> seen = new HashSet<int>();
        int stickFinger = -1;
        int mirrorFinger = -1;

        // ---- controls ----
        class Control
        {
            public string name;
            public RectTransform rect;
            public Image image;
            public Text label;
            public Func<bool> visible;
            public Action down, up;
            public float repeatSeconds;
            public bool held;
            public float nextRepeat;
            public bool shown = true;
            public Func<float> baseAlpha; // lets a button dim itself (e.g. a locked tool)
        }
        readonly List<Control> controls = new List<Control>();
        RectTransform safeRoot;
        RectTransform stickBase;
        RectTransform stickThumb;
        Rect lastSafeArea;
        Vector2Int lastScreenSize;
        Text[] toolLabels;

        // ---- things we look at ----
        ToolSwitcher tools;
        SelectionDrawMode drawMode;
        SculptStudio studio;
        readonly List<RaycastResult> raycastResults = new List<RaycastResult>();
        PointerEventData pointerData;

        const float StickRadius = 150f;

        // ---------- start-up ----------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Active = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            bool want = Application.isMobilePlatform;
#if UNITY_EDITOR
            want |= UnityEditor.EditorPrefs.GetBool(EditorPrefKey, false);
#endif
            if (!want) return;

            Active = true;
            var go = new GameObject("MobileControls");
            DontDestroyOnLoad(go);
            go.AddComponent<MobileControls>();
        }

        void Awake()
        {
            // a phone game: landscape, 60 fps, screen stays on
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Application.targetFrameRate = 60;

            keyboard = InputSystem.AddDevice<Keyboard>("MobileKeyboard");
            mouse = InputSystem.AddDevice<Mouse>("MobileMouse");
            pad = InputSystem.AddDevice<Gamepad>("MobileGamepad");
            EnhancedTouchSupport.Enable();
        }

        void Start()
        {
            HudFactory.EnsureEventSystem();
            BuildUI();
        }

        void OnDestroy()
        {
            Active = false;
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            if (pad != null) InputSystem.RemoveDevice(pad);
            if (EnhancedTouchSupport.enabled) EnhancedTouchSupport.Disable();
        }

        // ---------- every frame ----------

        void Update()
        {
            if (safeRoot == null) return;

            FindThings();
            UpdateSafeArea();
            ProcessTouches();
            UpdateControls();
            Flush();
        }

        void FindThings()
        {
            if (tools == null) tools = FindAnyObjectByType<ToolSwitcher>();
            if (drawMode == null) drawMode = FindAnyObjectByType<SelectionDrawMode>();
            if (studio == null) studio = FindAnyObjectByType<SculptStudio>();
        }

        static bool Captured => FirstPersonController.InputCaptured;

        // ---------- touches ----------

        void ProcessTouches()
        {
            seen.Clear();
            foreach (Touch touch in Touch.activeTouches)
            {
                int id = touch.finger.index;
                seen.Add(id);
                Vector2 position = touch.screenPosition;
                bool ending = touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;

                if (!fingers.TryGetValue(id, out Finger finger))
                {
                    finger = Assign(position);
                    fingers[id] = finger;
                    Begin(id, finger, position);
                }
                else
                {
                    Move(finger, position, touch.delta);
                }

                if (ending) End(id, finger);
            }

            // fingers that disappeared without a clean end
            gone.Clear();
            foreach (var pair in fingers)
            {
                if (!seen.Contains(pair.Key)) gone.Add(pair.Key);
            }
            foreach (int id in gone)
            {
                End(id, fingers[id]);
            }
        }

        Finger Assign(Vector2 position)
        {
            // 1. an on-screen button
            for (int i = controls.Count - 1; i >= 0; i--)
            {
                Control c = controls[i];
                if (!c.shown) continue;
                if (Vector2.Distance(position, c.rect.position) <= c.rect.rect.width * 0.5f * c.rect.lossyScale.x * 1.1f)
                    return new Finger { owner = Owner.Button, control = c };
            }

            // 2. the walking stick
            if (Captured && stickFinger < 0 && Vector2.Distance(position, stickBase.position) <= StickRadius * stickBase.lossyScale.x * 1.7f)
                return new Finger { owner = Owner.Stick };

            // 3. something clickable in the game's own UI (buttons, the mixing pool...)
            if (OverClickableUi(position)) return new Finger { owner = Owner.Ui };

            // 4. the rest of the screen: look around while playing, otherwise act as the mouse pointer
            if (Captured) return new Finger { owner = Owner.Look };
            if (mirrorFinger < 0 && !(studio != null && studio.IsActive)) return new Finger { owner = Owner.Mirror }; // the sculpt studio reads touches itself
            return new Finger { owner = Owner.Ui };
        }

        void Begin(int id, Finger finger, Vector2 position)
        {
            switch (finger.owner)
            {
                case Owner.Button:
                    finger.control.held = true;
                    finger.control.nextRepeat = Time.unscaledTime + 0.35f;
                    finger.control.down?.Invoke();
                    break;
                case Owner.Stick:
                    stickFinger = id;
                    Move(finger, position, Vector2.zero);
                    break;
                case Owner.Mirror:
                    mirrorFinger = id;
                    mirrorHeld = true;
                    mousePosition = position;
                    break;
            }
        }

        void Move(Finger finger, Vector2 position, Vector2 delta)
        {
            switch (finger.owner)
            {
                case Owner.Stick:
                    stick = Vector2.ClampMagnitude((position - (Vector2)stickBase.position) / (StickRadius * stickBase.lossyScale.x), 1f);
                    break;
                case Owner.Look:
                    lookDelta += delta * lookScale;
                    break;
                case Owner.Mirror:
                    mousePosition = position;
                    break;
                case Owner.Button:
                    Control c = finger.control;
                    if (c.repeatSeconds > 0f && Time.unscaledTime >= c.nextRepeat)
                    {
                        c.nextRepeat = Time.unscaledTime + c.repeatSeconds;
                        c.down?.Invoke();
                    }
                    break;
            }
        }

        void End(int id, Finger finger)
        {
            switch (finger.owner)
            {
                case Owner.Button:
                    finger.control.held = false;
                    finger.control.up?.Invoke();
                    break;
                case Owner.Stick:
                    stickFinger = -1;
                    stick = Vector2.zero;
                    break;
                case Owner.Mirror:
                    mirrorFinger = -1;
                    mirrorHeld = false;
                    break;
            }
            fingers.Remove(id);
        }

        // true when the topmost thing under the finger would react to a press (a button, a drag area)
        bool OverClickableUi(Vector2 position)
        {
            EventSystem system = EventSystem.current;
            if (system == null) return false;

            pointerData ??= new PointerEventData(system);
            pointerData.position = position;
            raycastResults.Clear();
            system.RaycastAll(pointerData, raycastResults);
            if (raycastResults.Count == 0) return false;

            GameObject top = raycastResults[0].gameObject;
            return ExecuteEvents.GetEventHandler<IPointerDownHandler>(top) != null
                   || ExecuteEvents.GetEventHandler<IPointerClickHandler>(top) != null
                   || ExecuteEvents.GetEventHandler<IDragHandler>(top) != null;
        }

        // ---------- the controls on screen ----------

        void UpdateControls()
        {
            bool captured = Captured;

            // the stick
            stickBase.gameObject.SetActive(captured);
            if (captured)
            {
                stickThumb.anchoredPosition = stick * StickRadius * 0.55f;
            }

            foreach (Control c in controls)
            {
                bool visible = c.visible == null || c.visible();
                if (visible != c.shown)
                {
                    c.shown = visible;
                    c.image.gameObject.SetActive(visible);
                }
                if (!c.shown) continue;

                float alpha = c.baseAlpha != null ? c.baseAlpha() : 0.26f;
                c.image.color = new Color(1f, 1f, 1f, c.held ? alpha + 0.3f : alpha);
            }

            // tool buttons show the tool's own name
            if (tools != null && tools.tools != null && toolLabels != null)
            {
                for (int i = 0; i < toolLabels.Length && i < tools.tools.Length; i++)
                {
                    string shortName = tools.tools[i].name;
                    int space = shortName.IndexOf(' ');
                    if (space > 0) shortName = shortName.Substring(0, space);
                    if (toolLabels[i].text != shortName) toolLabels[i].text = shortName;
                }
            }
        }

        bool GloveActive => tools != null && tools.tools != null && tools.ActiveIndex < tools.tools.Length && tools.Active.requires == Ability.Glove;
        bool InkBlasterActive => tools != null && tools.tools != null && tools.ActiveIndex < tools.tools.Length && tools.Active.requires == Ability.InkBlaster;
        bool DrawModeActive => drawMode != null && drawMode.IsActive;

        void BuildUI()
        {
            RectTransform canvas = HudFactory.CreateCanvas("MobileControlsCanvas", transform, 70);
            safeRoot = HudFactory.CreateGroup("SafeArea", canvas);

            // the walking stick, bottom left
            Image back = HudFactory.CreateImage("StickBase", safeRoot, HudFactory.Circle, new Color(1f, 1f, 1f, 0.16f), StickRadius * 2f);
            Place(back.rectTransform, new Vector2(0f, 0f), new Vector2(280f, 280f), new Vector2(StickRadius * 2f, StickRadius * 2f));
            stickBase = back.rectTransform;
            Image thumb = HudFactory.CreateImage("StickThumb", back.transform, HudFactory.Circle, new Color(1f, 1f, 1f, 0.38f), 130f);
            thumb.rectTransform.anchorMin = thumb.rectTransform.anchorMax = thumb.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            thumb.rectTransform.anchoredPosition = Vector2.zero;
            stickThumb = thumb.rectTransform;

            // right-hand buttons
            Func<bool> playing = () => Captured;
            AddButton("Act", "ACT", new Vector2(1f, 0f), new Vector2(-240f, 240f), 230f, playing, () => fireHeld = true, () => fireHeld = false);
            AddButton("Use", "USE", new Vector2(1f, 0f), new Vector2(-250f, 520f), 150f, playing, () => TapKey(Key.F), null);
            AddButton("Jump", "JUMP", new Vector2(1f, 0f), new Vector2(-520f, 190f), 140f, playing, () => TapKey(Key.Space), null);
            AddButton("Roll", "ROLL", new Vector2(1f, 0f), new Vector2(-520f, 420f), 120f, () => Captured && InkBlasterActive, () => rollHeld = true, () => rollHeld = false);

            // resize (the mouse wheel): carrying with the glove, or the brush size while drawing
            Func<bool> resizeVisible = () => (Captured && GloveActive) || DrawModeActive;
            AddButton("Bigger", "+", new Vector2(1f, 0.5f), new Vector2(-90f, 130f), 110f, resizeVisible, () => pendingScroll.y += 1f, null, 0.12f);
            AddButton("Smaller", "-", new Vector2(1f, 0.5f), new Vector2(-90f, -10f), 110f, resizeVisible, () => pendingScroll.y -= 1f, null, 0.12f);

            // tools, top right
            Key[] toolKeys = { Key.Z, Key.X, Key.C, Key.V };
            toolLabels = new Text[toolKeys.Length];
            for (int i = 0; i < toolKeys.Length; i++)
            {
                int index = i;
                Control tool = AddButton("Tool" + i, "", new Vector2(1f, 1f), new Vector2(-95f - (toolKeys.Length - 1 - i) * 135f, -95f), 118f,
                                         playing, () => TapKey(DirectKey(index, toolKeys[index])), null);
                tool.baseAlpha = () => tools == null || !tools.IsAllowed(index) ? 0.08f : (tools.ActiveIndex == index ? 0.5f : 0.24f);
                toolLabels[i] = tool.label;
                tool.label.fontSize = 22;
            }

            // colours: previous / next
            AddButton("ColorPrev", "<", new Vector2(1f, 1f), new Vector2(-230f, -235f), 100f, playing, () => TapKey(Key.Q), null);
            AddButton("ColorNext", ">", new Vector2(1f, 1f), new Vector2(-95f, -235f), 100f, playing, () => TapKey(Key.E), null);

            // back out of whatever is open
            AddButton("Back", "BACK", new Vector2(0f, 1f), new Vector2(110f, -95f), 130f, () => !Captured, () => TapKey(Key.Escape), null);
        }

        Key DirectKey(int index, Key fallback)
        {
            if (tools != null && tools.directKeys != null && index < tools.directKeys.Length && tools.directKeys[index] != Key.None) return tools.directKeys[index];
            return fallback;
        }

        // position is where the middle of the button sits, measured from the given corner or edge of the safe area in canvas pixels (negative = inwards for the right / top)
        static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        Control AddButton(string name, string text, Vector2 anchor, Vector2 position, float size, Func<bool> visible, Action down, Action up, float repeat = 0f)
        {
            Image image = HudFactory.CreateImage(name, safeRoot, HudFactory.Circle, new Color(1f, 1f, 1f, 0.26f), size);
            Place(image.rectTransform, anchor, position, new Vector2(size, size));

            Text label = HudFactory.CreateText("Label", image.transform, text, size > 200f ? 40 : 28, TextAnchor.MiddleCenter);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;

            var control = new Control
            {
                name = name,
                rect = image.rectTransform,
                image = image,
                label = label,
                visible = visible,
                down = down,
                up = up,
                repeatSeconds = repeat,
            };
            controls.Add(control);
            return control;
        }

        // keep everything inside the notch / home bar
        void UpdateSafeArea()
        {
            var size = new Vector2Int(Screen.width, Screen.height);
            if (Screen.safeArea == lastSafeArea && size == lastScreenSize) return;
            lastSafeArea = Screen.safeArea;
            lastScreenSize = size;

            Rect safe = Screen.safeArea;
            safeRoot.anchorMin = new Vector2(safe.xMin / size.x, safe.yMin / size.y);
            safeRoot.anchorMax = new Vector2(safe.xMax / size.x, safe.yMax / size.y);
            safeRoot.offsetMin = safeRoot.offsetMax = Vector2.zero;
        }

        // ---------- sending to the virtual devices ----------

        // a key is held down for one whole frame, so the game sees "pressed this frame"
        void TapKey(Key key)
        {
            heldKeys[key] = Time.frameCount + 1;
            keysDirty = true;
        }

        readonly List<Key> releaseKeys = new List<Key>();

        void Flush()
        {
            // keys: release the ones that have been down long enough
            releaseKeys.Clear();
            foreach (var pair in heldKeys)
            {
                if (Time.frameCount >= pair.Value) releaseKeys.Add(pair.Key);
            }
            foreach (Key key in releaseKeys)
            {
                heldKeys.Remove(key);
                keysDirty = true;
            }
            if (keysDirty)
            {
                var state = new KeyboardState();
                foreach (Key key in heldKeys.Keys) state.Set(key, true);
                InputSystem.QueueStateEvent(keyboard, state);
                keysDirty = false;
            }

            // walking
            if (stick != sentStick)
            {
                InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = stick });
                sentStick = stick;
            }

            // the pointer: aimed at the middle of the screen while playing, under the finger otherwise
            bool captured = Captured;
            Vector2 position = captured ? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) : mousePosition;
            bool left = fireHeld || (!captured && mirrorHeld);
            bool right = rollHeld;
            bool hasDelta = lookDelta != Vector2.zero;
            bool hasScroll = pendingScroll != Vector2.zero;

            bool changed = firstMouseSend || position != sentMousePosition || left != sentLeft || right != sentRight
                           || hasDelta || hasScroll || sentDelta || sentScroll;
            if (changed)
            {
                var state = new MouseState { position = position, delta = lookDelta, scroll = pendingScroll };
                if (left) state = state.WithButton(MouseButton.Left);
                if (right) state = state.WithButton(MouseButton.Right);
                InputSystem.QueueStateEvent(mouse, state);

                sentMousePosition = position;
                sentLeft = left;
                sentRight = right;
                sentDelta = hasDelta;
                sentScroll = hasScroll;
                firstMouseSend = false;
            }
            lookDelta = Vector2.zero;
            pendingScroll = Vector2.zero;
        }
    }
}
