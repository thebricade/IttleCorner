using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace InkFlip
{
    // Sculpt a sculpture out of playdough, then drop it into the world where it can be painted like anything else.
    //
    // Add this to one object in the scene. Press B (or tap the Sculpt button) to open the studio.
    //
    // Step 1 - the pottery wheel: a spinning lump of clay. Pick a starting profile (cylinder, vase, bowl...)
    //   and press the clay with your finger to push the wall in or pull it out. "Just a ball" skips the wheel.
    // Step 2 - the bench: a sculpture is a handful of shapes (sphere, cube, cylinder, cone, pyramid, and the
    //   thrown base) pressed together.
    //     Build / Press / Smooth / Pull sculpt the surface of the shape you touch.
    //     Move / Size / Spin arrange the shape you touch. Move slides over the other shapes and snaps to them.
    //     Rotate shows three rings around the selected shape (like a 3D app's transform gizmo): drag a ring to
    //     turn the shape around that axis, with soft snapping to 15 degree steps.
    //     "Add shape" drops a new one onto the clay.
    //
    // Getting around: pinch / wheel zooms toward your finger, double-tap zooms to the shape you tapped (or back
    // out to everything), "Zoom all" and "Zoom shape" do the same from the buttons.
    //   Mouse: left-drag to use the tool, left-drag empty space / right-drag to turn everything,
    //          middle-drag to pan, wheel to zoom
    //   Touch: one finger uses the tool (or turns everything if you start on empty space),
    //          two fingers turn and pinch-zoom
    //   Esc: cancel    Done: place the sculpture in front of you
    public class SculptStudio : MonoBehaviour
    {
        [Tooltip("InkFlip/PaintableLit - assigned so the shader is included in builds.")]
        public Shader paintableShader;
        [Tooltip("Unlit shader for the snap marker and guide line. Falls back to Universal Render Pipeline/Unlit.")]
        public Shader markerShader;
        public FirstPersonController player;
        public ToolSwitcher tools;
        public Camera worldCamera;

        [Tooltip("Not used while the studio is switched off.")]
        public Key openKey = Key.None;

        [Header("Clay")]
        [Tooltip("Quads along each side of a shape. More = finer detail but slower sculpting.")]
        public int resolution = 24;
        public float clayRadius = 1f;
        public Color[] clayColors =
        {
            new Color(0.93f, 0.42f, 0.45f), // strawberry
            new Color(0.98f, 0.69f, 0.28f), // apricot
            new Color(0.97f, 0.86f, 0.35f), // butter
            new Color(0.52f, 0.78f, 0.45f), // pistachio
            new Color(0.42f, 0.65f, 0.92f), // blueberry
            new Color(0.78f, 0.58f, 0.9f),  // lavender
        };

        [Header("Pottery wheel")]
        public float wheelSpinSpeed = 90f;
        [Tooltip("How quickly the wall follows your finger (higher = snappier).")]
        public float wheelResponse = 10f;
        public float wheelHeightStep = 0.1f;

        [Header("Shapes")]
        public int maxShapes = 8;
        [Tooltip("Size of a newly added shape compared to the first one.")]
        public float newShapeScale = 0.55f;
        public float minShapeScale = 0.15f;
        public float maxShapeScale = 2.5f;
        [Tooltip("Screen pixels of vertical drag that double / halve a shape with the Size tool.")]
        public float resizePixelsPerDoubling = 220f;

        [Header("Snapping")]
        [Tooltip("While moving, a shape sticks to the surface of any other shape under your finger.")]
        public bool snapToSurfaces = true;
        [Tooltip("How far a shape sinks into what it's attached to, as a fraction of its own size, so it reads as joined.")]
        public float snapEmbed = 0.12f;
        [Tooltip("Letting go this close to another shape (in clay units) pulls it in until it touches.")]
        public float releaseSnapDistance = 0.3f;
        [Tooltip("A shape this close to the middle plane snaps onto it, and a guide line shows.")]
        public float centreSnap = 0.06f;

        [Header("Rotate rings")]
        [Tooltip("Rotation snaps to multiples of this many degrees when you're close to one.")]
        public float rotateSnapDegrees = 15f;
        [Tooltip("How close (in degrees) to a snap step counts as on it.")]
        public float rotateSnapTolerance = 5f;
        [Tooltip("How near (in screen pixels at 1080p) a touch must be to a ring to grab it.")]
        public float ringPickPixels = 45f;

        [Header("Brush")]
        public float brushRadius = 0.3f;
        public float minBrushRadius = 0.1f;
        public float maxBrushRadius = 0.7f;
        [Tooltip("How far (in clay radii per second) Build / Press move the surface at the middle of the brush.")]
        public float pushSpeed = 0.9f;
        public float smoothSpeed = 8f;
        public int undoSteps = 20;

        [Header("Placing")]
        [Tooltip("Longest side of the finished sculpture in the world, in metres.")]
        public float placedSize = 0.8f;
        public float placeReach = 6f;
        [Tooltip("Paint texels per metre of surface. Small objects need a lot to take crisp ink.")]
        public float paintTexelsPerMeter = 700f;

        [Header("Camera")]
        public float cameraDistance = 3.4f;
        public float minCameraDistance = 0.7f;
        public float maxCameraDistance = 6f;
        public float turnSpeed = 0.4f;
        [Tooltip("How quickly the camera glides to a zoom-to-shape (higher = snappier).")]
        public float frameSpeed = 10f;
        public float doubleTapSeconds = 0.3f;
        public float doubleTapPixels = 60f;

        public bool IsActive { get; private set; }
        public bool OnWheel { get; private set; }
        public ClayTool Tool { get; private set; } = ClayTool.Add;
        public bool Mirror { get; private set; }
        public bool AngleSnap { get; private set; } = true;

        // fired with the new object once a sculpture is placed (hook quests / inventory here)
        public event Action<GameObject> SculptureCreated;

        // the stage lives far from the playable world so the clay never clips into the scene
        static readonly Vector3 StagePosition = new Vector3(0f, 1000f, 0f);

        const float MinWheelRadius = 0.15f;
        const float MaxWheelRadius = 1.4f;
        const float MinWheelHalfHeight = 0.3f;
        const float MaxWheelHalfHeight = 1.6f;

        // everything needed to put one shape back the way it was (for Undo)
        struct PartState
        {
            public bool alive;
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
            public Color color;
            public Vector3[] points;
        }

        GameObject stage;
        Transform clayRoot;
        Material materialTemplate;
        Camera studioCamera;
        SculptStudioUI ui;
        SelectionDrawMode drawMode;

        // wheel step
        GameObject wheelDisc;
        float[] profile;
        float wheelHalfHeight = 0.9f;
        bool wheelPressing;

        // rotate rings
        const int RingPoints = 64;
        static readonly Color[] RingColors =
        {
            new Color(0.95f, 0.3f, 0.3f),  // X
            new Color(0.4f, 0.85f, 0.35f), // Y
            new Color(0.35f, 0.55f, 1f),   // Z
        };
        static readonly Color RingActiveColor = new Color(1f, 0.9f, 0.3f);
        GameObject gizmoRoot;
        LineRenderer[] gizmoRings;
        Material[] gizmoMaterials;
        readonly Vector3[] ringBuffer = new Vector3[RingPoints + 1];
        bool gizmoDragging;
        int gizmoAxisIndex;
        Vector3 gizmoAxis;
        Vector3 gizmoCentre;
        Vector3 gizmoStartOffset;
        Quaternion gizmoStartRotation;
        float gizmoAngle;

        // snap feedback
        GameObject snapMarker;
        GameObject centreGuide;
        ClayPart snappedTo;

        // every shape made this session (removed ones stay, hidden, so Undo can bring them back)
        readonly List<ClayPart> parts = new List<ClayPart>();
        ClayPart active;
        int colorIndex;
        readonly List<List<PartState>> undoStack = new List<List<PartState>>();

        // camera
        Vector3 cameraFocus;
        bool framing;
        Vector3 frameFocus;
        float frameDistance;

        // pointer
        bool stroking;
        bool rotating;
        bool ignoreUntilRelease;
        bool worldCameraWasEnabled;
        ClayPart strokePart;
        Plane dragPlane;
        Vector3 dragStartWorld;
        Vector3 dragGrabOffset;
        Vector2 dragStartScreen;
        float dragStartScale;
        float lastTapTime = -10f;
        Vector2 lastTapPosition;
        bool lastTapPushedUndo;

        void Awake()
        {
            ui = new SculptStudioUI(transform, clayColors)
            {
                OpenClicked = Open,
                ToolSelected = SelectTool,
                ShapeAdded = AddShape,
                ColorSelected = SelectColor,
                WheelPresetSelected = LoadWheelPreset,
                WheelTaller = () => ChangeWheelHeight(wheelHeightStep),
                WheelShorter = () => ChangeWheelHeight(-wheelHeightStep),
                WheelNext = FinishWheel,
                WheelSkip = SkipWheel,
                SizeSmaller = () => ChangeBrushSize(1f / 1.25f),
                SizeBigger = () => ChangeBrushSize(1.25f),
                MirrorToggled = ToggleMirror,
                SnapToggled = ToggleAngleSnap,
                Undo = UndoLast,
                Reset = EnterWheel,
                DeletePart = DeleteActive,
                FitAll = FrameAll,
                FocusShape = FocusOnActive,
                Done = () => Close(true),
                Cancel = () => Close(false),
            };
        }

        void OnDestroy()
        {
            DestroyAllParts();
            if (materialTemplate != null) Destroy(materialTemplate);
            if (gizmoMaterials != null) foreach (Material m in gizmoMaterials) if (m != null) Destroy(m);
            if (stage != null) Destroy(stage);
        }

        // ---------- opening / closing ----------

        void Resolve()
        {
            if (player == null) player = FindAnyObjectByType<FirstPersonController>();
            if (tools == null) tools = FindAnyObjectByType<ToolSwitcher>();
            if (worldCamera == null) worldCamera = Camera.main;
            if (drawMode == null) drawMode = FindAnyObjectByType<SelectionDrawMode>();
        }

        bool CanOpen()
        {
            Resolve();
            return !IsActive && worldCamera != null && Abilities.Can(Ability.Sculpt) && !(drawMode != null && drawMode.IsActive);
        }

        public void Open()
        {
            if (!CanOpen()) return;

            BuildStage();
            colorIndex = 0;
            Tool = ClayTool.Add;
            Mirror = false;
            stroking = rotating = wheelPressing = false;
            ignoreUntilRelease = true; // the click on the Sculpt button shouldn't dig into the clay
            cameraFocus = Vector3.zero;
            cameraDistance = Mathf.Clamp(cameraDistance, minCameraDistance, maxCameraDistance);

            IsActive = true;
            EnterWheel();

            if (player != null) player.enabled = false;
            if (tools != null) tools.enabled = false;
            worldCameraWasEnabled = worldCamera.enabled;
            worldCamera.enabled = false;
            studioCamera.enabled = true;

            FirstPersonController.SetCursorCaptured(false);
            ui.SetStudioVisible(true);
            ui.SetStep(OnWheel);
            RefreshUI();
        }

        // place = true keeps the sculpture; false throws it away
        public void Close(bool place)
        {
            if (!IsActive) return;

            // never place a broken mesh: repair what can be repaired, and stop here if something can't be
            if (place && !OnWheel && !CheckHealth()) return;
            if (place && OnWheel) FinishWheel();

            GameObject sculpture = place ? CreateSculpture() : null;

            IsActive = false;
            stroking = rotating = wheelPressing = gizmoDragging = false;
            HideSnapFeedback();
            if (gizmoRoot != null) gizmoRoot.SetActive(false);
            studioCamera.enabled = false;
            worldCamera.enabled = worldCameraWasEnabled;
            if (player != null) player.enabled = true;
            if (tools != null) tools.enabled = true;

            FirstPersonController.SetCursorCaptured(true);
            ui.SetStudioVisible(false);
            DestroyAllParts();

            if (sculpture != null) SculptureCreated?.Invoke(sculpture);
        }

        bool CheckHealth()
        {
            foreach (ClayPart part in parts)
            {
                if (!part.Alive) continue;
                if (!part.Clay.EnsureHealthy(out string problem))
                {
                    active = part;
                    ui.ShowMessage($"The {part.Shape} {problem} - press Undo, or delete that shape.");
                    RefreshUI();
                    return false;
                }
            }
            return true;
        }

        void BuildStage()
        {
            if (stage != null) return;

            stage = new GameObject("SculptStage");
            stage.transform.position = StagePosition;

            var cameraObject = new GameObject("SculptCamera");
            cameraObject.transform.SetParent(stage.transform, false);
            studioCamera = cameraObject.AddComponent<Camera>();
            studioCamera.clearFlags = CameraClearFlags.SolidColor;
            studioCamera.backgroundColor = new Color(0.78f, 0.74f, 0.7f);
            studioCamera.fieldOfView = 40f;
            studioCamera.nearClipPlane = 0.1f;
            studioCamera.farClipPlane = 50f;
            studioCamera.depth = 100f;
            studioCamera.enabled = false;

            // a little extra light so the form reads even if the scene's sun is low
            var lightObject = new GameObject("SculptLight");
            lightObject.transform.SetParent(stage.transform, false);
            lightObject.transform.localPosition = new Vector3(2f, 3f, -4f);
            var sculptLight = lightObject.AddComponent<Light>();
            sculptLight.type = LightType.Point;
            sculptLight.range = 20f;
            sculptLight.intensity = 6f;
            sculptLight.shadows = LightShadows.None;

            var clayObject = new GameObject("Clay");
            clayObject.transform.SetParent(stage.transform, false);
            clayRoot = clayObject.transform;

            materialTemplate = CreateClayMaterial();

            // the wheel the clay sits on (only visible during the wheel step)
            wheelDisc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wheelDisc.name = "WheelDisc";
            Destroy(wheelDisc.GetComponent<Collider>());
            wheelDisc.transform.SetParent(clayRoot, false);
            wheelDisc.transform.localScale = new Vector3(3f, 0.04f, 3f);
            var discMaterial = new Material(materialTemplate) { name = "WheelDisc" };
            discMaterial.SetColor("_BaseColor", new Color(0.25f, 0.25f, 0.29f));
            wheelDisc.GetComponent<MeshRenderer>().sharedMaterial = discMaterial;
            wheelDisc.SetActive(false);

            // snap feedback: a dot where the shape touches, a line down the middle plane
            snapMarker = CreateGuide(PrimitiveType.Sphere, "SnapMarker", stage.transform, new Color(1f, 0.85f, 0.25f));
            snapMarker.transform.localScale = Vector3.one * 0.09f;
            centreGuide = CreateGuide(PrimitiveType.Cylinder, "CentreGuide", clayRoot, new Color(0.3f, 0.9f, 0.95f));
            centreGuide.transform.localScale = new Vector3(0.012f, 2.2f, 0.012f);

            BuildRotateRings();
        }

        // three circles that follow the selected shape (drawn as lines, so no extra meshes or colliders)
        void BuildRotateRings()
        {
            gizmoRoot = new GameObject("RotateRings");
            gizmoRoot.transform.SetParent(stage.transform, false);
            gizmoRings = new LineRenderer[3];
            gizmoMaterials = new Material[3];

            Shader shader = markerShader != null ? markerShader : Shader.Find("Universal Render Pipeline/Unlit");
            for (int i = 0; i < 3; i++)
            {
                var ringObject = new GameObject("Ring_" + "XYZ"[i]);
                ringObject.transform.SetParent(gizmoRoot.transform, false);
                var line = ringObject.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.loop = false;
                line.positionCount = RingPoints + 1;
                line.numCapVertices = 4;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;

                gizmoMaterials[i] = shader != null ? new Material(shader) : new Material(materialTemplate);
                gizmoMaterials[i].SetColor("_BaseColor", RingColors[i]);
                line.sharedMaterial = gizmoMaterials[i];
                gizmoRings[i] = line;
            }
            gizmoRoot.SetActive(false);
        }

        GameObject CreateGuide(PrimitiveType type, string name, Transform parent, Color color)
        {
            GameObject guide = GameObject.CreatePrimitive(type);
            guide.name = name;
            Destroy(guide.GetComponent<Collider>());
            guide.transform.SetParent(parent, false);

            Shader shader = markerShader != null ? markerShader : Shader.Find("Universal Render Pipeline/Unlit");
            Material material = shader != null ? new Material(shader) : new Material(materialTemplate);
            material.SetColor("_BaseColor", color);
            guide.GetComponent<MeshRenderer>().sharedMaterial = material;
            guide.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            guide.SetActive(false);
            return guide;
        }

        Material CreateClayMaterial()
        {
            Shader shader = paintableShader != null ? paintableShader : Shader.Find("InkFlip/PaintableLit");
            if (shader == null)
            {
                Debug.LogError("SculptStudio: InkFlip/PaintableLit shader not found - sculptures won't take paint.", this);
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }
            var material = new Material(shader) { name = "Clay" };
            material.SetFloat("_Smoothness", 0.12f);
            return material;
        }

        // ---------- pottery wheel ----------

        // (re)starts from the wheel with a fresh lump of clay
        void EnterWheel()
        {
            OnWheel = true;
            DestroyAllParts();
            HideSnapFeedback();
            clayRoot.rotation = Quaternion.identity;

            active = CreatePart(ClayShape.Vase, Vector3.zero, 1f);
            wheelDisc.SetActive(true);
            LoadWheelPreset(0);

            cameraFocus = Vector3.zero;
            framing = false;
            cameraDistance = 3.4f;
            PlaceCamera();
            ui.SetStep(true);
            RefreshUI();
        }

        void LoadWheelPreset(int preset)
        {
            if (!OnWheel || active == null) return;

            int samples = resolution + 1;
            profile = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)(samples - 1);
                profile[i] = Mathf.Clamp(PresetRadius(preset, t), MinWheelRadius, MaxWheelRadius);
            }
            wheelHalfHeight = PresetHalfHeight(preset);
            ApplyWheel();
        }

        // 0 = cylinder, 1 = vase, 2 = bowl, 3 = ball, 4 = cone
        static float PresetRadius(int preset, float t)
        {
            switch (preset)
            {
                case 1: return 0.5f + 0.35f * Mathf.Sin(Mathf.PI * t) - 0.18f * t; // belly, narrower neck
                case 2: return 0.25f + 0.8f * Mathf.Pow(t, 0.7f);                  // wide open bowl
                case 3: { float u = 2f * t - 1f; return 0.95f * Mathf.Sqrt(Mathf.Max(0f, 1f - u * u)); }
                case 4: return 0.85f * (1f - t) + 0.12f;
                default: return 0.65f;
            }
        }

        static float PresetHalfHeight(int preset)
        {
            switch (preset)
            {
                case 1: return 1.05f;
                case 2: return 0.55f;
                case 3: return 0.95f;
                case 4: return 1f;
                default: return 0.9f;
            }
        }

        void ChangeWheelHeight(float delta)
        {
            if (!OnWheel || active == null) return;
            wheelHalfHeight = Mathf.Clamp(wheelHalfHeight + delta, MinWheelHalfHeight, MaxWheelHalfHeight);
            ApplyWheel();
        }

        void ApplyWheel()
        {
            active.Clay.SetProfile(profile, wheelHalfHeight);
            // the wheel sits just under the clay
            wheelDisc.transform.localPosition = new Vector3(0f, -wheelHalfHeight * clayRadius - 0.03f, 0f);
        }

        // press the clay wall: it follows your finger, so pull out to widen and push in to narrow
        void ShapeOnWheel(Vector2 screen)
        {
            Ray ray = studioCamera.ScreenPointToRay(screen);
            var plane = new Plane(-studioCamera.transform.forward, clayRoot.position);
            if (!plane.Raycast(ray, out float distance)) return;

            Vector3 point = clayRoot.InverseTransformPoint(ray.GetPoint(distance));
            float height = point.y / clayRadius;
            if (Mathf.Abs(height) > wheelHalfHeight + 0.2f) return;

            float target = Mathf.Clamp(Mathf.Abs(point.x) / clayRadius, MinWheelRadius, MaxWheelRadius);
            float sigma = Mathf.Max(0.12f, brushRadius * 0.8f);
            float follow = 1f - Mathf.Exp(-wheelResponse * Time.deltaTime);

            int last = profile.Length - 1;
            for (int i = 0; i <= last; i++)
            {
                float y = Mathf.Lerp(-wheelHalfHeight, wheelHalfHeight, i / (float)last);
                float falloff = Mathf.Exp(-Mathf.Pow((y - height) / sigma, 2f));
                profile[i] = Mathf.Lerp(profile[i], target, follow * falloff);
            }

            // a light smoothing pass keeps the wall flowing instead of getting spiky
            for (int i = 1; i < last; i++)
            {
                float smoothed = (profile[i - 1] + 2f * profile[i] + profile[i + 1]) * 0.25f;
                profile[i] = Mathf.Lerp(profile[i], smoothed, 0.15f);
            }
            ApplyWheel();
        }

        // wheel -> bench, keeping the thrown shape
        void FinishWheel()
        {
            if (!OnWheel) return;
            OnWheel = false;
            wheelPressing = false;
            wheelDisc.SetActive(false);
            active.Transform.localRotation = Quaternion.identity;
            undoStack.Clear();
            ui.SetStep(false);
            FrameAll();
            RefreshUI();
        }

        // wheel -> bench with a plain ball instead
        void SkipWheel()
        {
            if (!OnWheel) return;
            DestroyAllParts();
            active = CreatePart(ClayShape.Sphere, Vector3.zero, 1f);
            FinishWheel();
        }

        // ---------- shapes ----------

        int AliveCount
        {
            get
            {
                int count = 0;
                foreach (ClayPart part in parts) if (part.Alive) count++;
                return count;
            }
        }

        ClayPart CreatePart(ClayShape shape, Vector3 position, float scale)
        {
            var part = new ClayPart(shape, resolution, clayRadius, clayRoot, materialTemplate, clayColors[colorIndex]);
            part.Transform.localPosition = position;
            part.Transform.localRotation = Quaternion.identity;
            part.Transform.localScale = Vector3.one * scale;
            parts.Add(part);
            return part;
        }

        void AddShape(ClayShape shape)
        {
            if (!IsActive || OnWheel) return;
            if (AliveCount >= maxShapes)
            {
                ui.ShowMessage($"That's {maxShapes} shapes - delete one to add another.");
                return;
            }

            PushUndo();

            // land on the clay in the middle of the view, so the new shape starts out attached
            Vector3 position = new Vector3(0f, clayRadius * 0.9f, 0f);
            Ray centre = studioCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (TryHitParts(centre, out _, out _, out Vector3 worldPoint, out _)) position = clayRoot.InverseTransformPoint(worldPoint);

            active = CreatePart(shape, position, newShapeScale);
            RefreshUI();
        }

        void DeleteActive()
        {
            if (!IsActive || OnWheel || active == null) return;
            if (AliveCount <= 1)
            {
                ui.ShowMessage("Keep at least one shape.");
                return;
            }

            PushUndo();
            active.SetAlive(false);
            active = FirstAlive();
            RefreshUI();
        }

        ClayPart FirstAlive()
        {
            foreach (ClayPart part in parts) if (part.Alive) return part;
            return null;
        }

        void DestroyAllParts()
        {
            foreach (ClayPart part in parts) part.Destroy();
            parts.Clear();
            active = null;
            undoStack.Clear();
        }

        // ---------- tool state ----------

        void SelectTool(ClayTool tool)
        {
            Tool = tool;
            RefreshUI();
        }

        void SelectColor(int index)
        {
            if (index < 0 || index >= clayColors.Length) return;
            colorIndex = index;
            if (active != null)
            {
                if (!OnWheel) PushUndo();
                active.SetColor(clayColors[index]);
            }
        }

        void ChangeBrushSize(float factor)
        {
            brushRadius = Mathf.Clamp(brushRadius * factor, minBrushRadius, maxBrushRadius);
            RefreshUI();
        }

        void ToggleMirror()
        {
            Mirror = !Mirror;
            RefreshUI();
        }

        void ToggleAngleSnap()
        {
            AngleSnap = !AngleSnap;
            RefreshUI();
        }

        void RefreshUI()
        {
            string info = active != null ? $"{active.Shape}   ({AliveCount}/{maxShapes} shapes)" : "";
            if (OnWheel) info = "Pottery wheel";
            ui.Refresh(Tool, Mirror, brushRadius, info, AngleSnap, OnWheel);
        }

        // ---------- undo ----------

        List<PartState> Capture()
        {
            var state = new List<PartState>(parts.Count);
            foreach (ClayPart part in parts)
            {
                state.Add(new PartState
                {
                    alive = part.Alive,
                    position = part.Transform.localPosition,
                    rotation = part.Transform.localRotation,
                    scale = part.Transform.localScale,
                    color = part.Color,
                    points = part.Clay.Snapshot(),
                });
            }
            return state;
        }

        void PushUndo()
        {
            undoStack.Add(Capture());
            if (undoStack.Count > undoSteps) undoStack.RemoveAt(0);
        }

        void UndoLast()
        {
            if (OnWheel || undoStack.Count == 0) return;
            List<PartState> state = undoStack[undoStack.Count - 1];
            undoStack.RemoveAt(undoStack.Count - 1);

            for (int i = 0; i < parts.Count; i++)
            {
                ClayPart part = parts[i];
                if (i >= state.Count)
                {
                    part.SetAlive(false); // made after this snapshot
                    continue;
                }

                PartState s = state[i];
                part.SetAlive(s.alive);
                part.Transform.localPosition = s.position;
                part.Transform.localRotation = s.rotation;
                part.Transform.localScale = s.scale;
                part.SetColor(s.color);
                part.Clay.Restore(s.points);
            }

            if (active == null || !active.Alive) active = FirstAlive();
            RefreshUI();
        }

        // ---------- camera ----------

        void PlaceCamera()
        {
            studioCamera.transform.localPosition = cameraFocus + new Vector3(0f, 0f, -cameraDistance);
            studioCamera.transform.localRotation = Quaternion.identity;
        }

        // zoom by a factor (amount is the log of the change), keeping whatever is under 'screen' where it is,
        // so zooming goes toward your finger or the mouse instead of the middle of the screen
        void Zoom(float amount, Vector2 screen)
        {
            framing = false;
            float oldDistance = cameraDistance;
            float newDistance = Mathf.Clamp(oldDistance * Mathf.Exp(amount), minCameraDistance, maxCameraDistance);
            if (Mathf.Approximately(oldDistance, newDistance)) return;

            Ray ray = studioCamera.ScreenPointToRay(screen);
            var focusPlane = new Plane(-studioCamera.transform.forward, stage.transform.TransformPoint(cameraFocus));
            if (focusPlane.Raycast(ray, out float distance))
            {
                Vector3 under = stage.transform.InverseTransformPoint(ray.GetPoint(distance));
                Vector3 focus = under - (under - cameraFocus) * (newDistance / oldDistance);
                focus.z = cameraFocus.z;
                cameraFocus = Vector3.ClampMagnitude(focus, 3f);
            }

            cameraDistance = newDistance;
            PlaceCamera();
        }

        void Pan(Vector2 delta)
        {
            framing = false;
            float worldPerPixel = 2f * cameraDistance * Mathf.Tan(studioCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) / Screen.height;
            cameraFocus -= new Vector3(delta.x, delta.y, 0f) * worldPerPixel;
            cameraFocus = Vector3.ClampMagnitude(cameraFocus, 3f);
            PlaceCamera();
        }

        // glide the camera so 'bounds' (in stage space) fills the view
        void FrameBounds(Bounds bounds)
        {
            float radius = Mathf.Max(0.1f, bounds.extents.magnitude);
            frameFocus = bounds.center;
            frameDistance = Mathf.Clamp(radius / Mathf.Tan(studioCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.15f, minCameraDistance, maxCameraDistance);
            framing = true;
        }

        void FrameAll()
        {
            if (!IsActive) return;
            Bounds bounds = PartsBounds(null);
            if (bounds.size.sqrMagnitude > 0f) FrameBounds(bounds);
        }

        void FocusOnActive()
        {
            if (!IsActive || active == null) return;
            FrameBounds(PartsBounds(active));
        }

        // bounds in stage space of one shape, or of every live shape when 'only' is null
        Bounds PartsBounds(ClayPart only)
        {
            bool any = false;
            Bounds bounds = new Bounds();
            foreach (ClayPart part in parts)
            {
                if (!part.Alive || (only != null && part != only)) continue;
                foreach (Vector3 vertex in part.Clay.Mesh.vertices)
                {
                    Vector3 p = stage.transform.InverseTransformPoint(part.Transform.TransformPoint(vertex));
                    if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; }
                    else bounds.Encapsulate(p);
                }
            }
            return bounds;
        }

        void UpdateFraming()
        {
            if (!framing) return;
            float follow = 1f - Mathf.Exp(-frameSpeed * Time.unscaledDeltaTime);
            cameraFocus = Vector3.Lerp(cameraFocus, frameFocus, follow);
            cameraDistance = Mathf.Lerp(cameraDistance, frameDistance, follow);
            if ((cameraFocus - frameFocus).sqrMagnitude < 1e-6f && Mathf.Abs(cameraDistance - frameDistance) < 1e-3f)
            {
                cameraFocus = frameFocus;
                cameraDistance = frameDistance;
                framing = false;
            }
            PlaceCamera();
        }

        // ---------- input ----------

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            ui.Tick(Time.unscaledDeltaTime);

            if (!IsActive)
            {
                // the on-screen button is for touch (or whenever the mouse is free to click it)
                // the sculpt studio is switched off for now: no key and no button open it
                ui.SetOpenButtonVisible(false);
                return;
            }

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                FirstPersonController.ConsumeEscape();
                Close(false);
                return;
            }

            if (OnWheel && active != null) active.Transform.Rotate(Vector3.up, wheelSpinSpeed * Time.deltaTime, Space.Self);

            HandleInput();
            UpdateFraming();
            UpdateRotateRings();
        }

        void HandleInput()
        {
            Mouse mouse = Mouse.current;
            Touchscreen touchscreen = Touchscreen.current;

            // gather fingers
            TouchControl first = null, second = null;
            int touchCount = 0;
            if (touchscreen != null)
            {
                foreach (TouchControl touch in touchscreen.touches)
                {
                    if (!touch.press.isPressed) continue;
                    touchCount++;
                    if (first == null) first = touch;
                    else if (second == null) second = touch;
                }
            }

            // two fingers: turn everything and pinch to zoom toward the fingers, never sculpt
            if (touchCount >= 2)
            {
                EndStroke();
                ignoreUntilRelease = true;
                Vector2 p0 = first.position.ReadValue(), p1 = second.position.ReadValue();
                Vector2 d0 = first.delta.ReadValue(), d1 = second.delta.ReadValue();
                if (!OnWheel) Turn((d0 + d1) * 0.5f);
                float now = Vector2.Distance(p0, p1);
                float before = Vector2.Distance(p0 - d0, p1 - d1);
                if (before > 1f) Zoom(-Mathf.Log(Mathf.Max(now, 1f) / before), (p0 + p1) * 0.5f);
                return;
            }

            // mouse extras
            if (mouse != null && touchCount == 0)
            {
                if (mouse.rightButton.isPressed && !OnWheel) Turn(mouse.delta.ReadValue());
                if (mouse.middleButton.isPressed) Pan(mouse.delta.ReadValue());
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f) Zoom(-Mathf.Sign(scroll) * 0.15f, mouse.position.ReadValue());
            }

            // the one pointer that works the tools: a finger, or the left mouse button
            bool pressed, held, released;
            Vector2 position, delta;
            int pointerId;
            if (touchCount == 1)
            {
                pressed = first.press.wasPressedThisFrame;
                held = true;
                released = false;
                position = first.position.ReadValue();
                delta = first.delta.ReadValue();
                pointerId = first.touchId.ReadValue();
            }
            else if (mouse != null)
            {
                pressed = mouse.leftButton.wasPressedThisFrame;
                held = mouse.leftButton.isPressed;
                released = mouse.leftButton.wasReleasedThisFrame;
                position = mouse.position.ReadValue();
                delta = mouse.delta.ReadValue();
                pointerId = -1;
            }
            else
            {
                EndStroke();
                ignoreUntilRelease = false;
                return;
            }

            if (!held || released)
            {
                EndStroke();
                if (!held) ignoreUntilRelease = false;
                return;
            }
            if (ignoreUntilRelease && !pressed) return;

            if (pressed)
            {
                ignoreUntilRelease = false;
                // the mouse uses the no-argument check (like draw mode); fingers are looked up by their touch id
                bool overUI = EventSystem.current != null &&
                              (pointerId < 0 ? EventSystem.current.IsPointerOverGameObject() : EventSystem.current.IsPointerOverGameObject(pointerId));
                if (overUI)
                {
                    ignoreUntilRelease = true;
                    return;
                }

                if (OnWheel)
                {
                    wheelPressing = true;
                    ShapeOnWheel(position);
                    return;
                }

                // a ring under the finger wins over anything behind it
                if (TryBeginRingDrag(position)) return;

                Ray ray = studioCamera.ScreenPointToRay(position);
                bool hit = TryHitParts(ray, out ClayPart part, out Vector3 localPoint, out Vector3 worldPoint, out _);

                // double-tap: zoom to the shape (or back out to everything)
                if (Time.unscaledTime - lastTapTime < doubleTapSeconds && Vector2.Distance(position, lastTapPosition) < doubleTapPixels)
                {
                    lastTapTime = -10f;
                    if (lastTapPushedUndo) UndoLast(); // the first tap may have nudged the clay - take that back
                    lastTapPushedUndo = false;
                    ignoreUntilRelease = true;
                    ZoomToTap(hit ? part : null);
                    return;
                }
                lastTapTime = Time.unscaledTime;
                lastTapPosition = position;

                // Rotate tool: tapping a shape only selects it (the rings do the work); dragging turns the view
                if (Tool == ClayTool.Rotate)
                {
                    if (hit)
                    {
                        active = part;
                        RefreshUI();
                    }
                    rotating = true;
                    lastTapPushedUndo = false;
                    return;
                }

                if (hit) BeginStroke(part, localPoint, worldPoint, position);
                else rotating = true;
                lastTapPushedUndo = hit;
                return;
            }

            if (wheelPressing) ShapeOnWheel(position);
            else if (gizmoDragging) DragRing(delta);
            else if (stroking) ContinueStroke(position, delta);
            else if (rotating) Turn(delta);
        }

        void ZoomToTap(ClayPart part)
        {
            if (part == null)
            {
                FrameAll();
                return;
            }

            // tapping the shape you're already looking at zooms back out
            active = part;
            RefreshUI();
            Bounds bounds = PartsBounds(part);
            bool alreadyFocused = (cameraFocus - bounds.center).sqrMagnitude < 0.01f && cameraDistance < maxCameraDistance * 0.6f;
            if (alreadyFocused) FrameAll();
            else FrameBounds(bounds);
        }

        void Turn(Vector2 delta)
        {
            clayRoot.Rotate(Vector3.up, -delta.x * turnSpeed, Space.World);
            clayRoot.Rotate(studioCamera.transform.right, delta.y * turnSpeed, Space.World);
        }

        // ---------- picking ----------

        // nearest shape under the ray. localPoint is in that shape's own space, worldPoint and worldNormal in the world.
        bool TryHitParts(Ray ray, out ClayPart hitPart, out Vector3 localPoint, out Vector3 worldPoint, out Vector3 worldNormal, ClayPart exclude = null)
        {
            hitPart = null;
            localPoint = worldPoint = worldNormal = Vector3.zero;
            float best = float.MaxValue;

            foreach (ClayPart part in parts)
            {
                if (!part.Alive || part == exclude) continue;
                if (!TryHitPart(part, ray, out Vector3 local, out Vector3 world, out Vector3 normal)) continue;

                float distance = Vector3.Distance(ray.origin, world);
                if (distance < best)
                {
                    best = distance;
                    hitPart = part;
                    localPoint = local;
                    worldPoint = world;
                    worldNormal = normal;
                }
            }
            return hitPart != null;
        }

        bool TryHitPart(ClayPart part, Ray ray, out Vector3 localPoint, out Vector3 worldPoint, out Vector3 worldNormal)
        {
            Transform t = part.Transform;
            // InverseTransformVector keeps the shape's scale in the direction, so distances stay in world units
            var localRay = new Ray(t.InverseTransformPoint(ray.origin), t.InverseTransformVector(ray.direction));
            if (part.Clay.Raycast(localRay, out localPoint, out Vector3 localNormal))
            {
                worldPoint = t.TransformPoint(localPoint);
                worldNormal = t.TransformDirection(localNormal).normalized;
                return true;
            }
            worldPoint = worldNormal = Vector3.zero;
            return false;
        }

        // brush size is measured in the clay's world units; shapes can be scaled, so convert for each one
        float LocalRadius(ClayPart part)
        {
            return brushRadius / Mathf.Max(0.01f, part.Transform.localScale.x);
        }

        // a world-space point or offset reflected across the clay's middle plane
        Vector3 MirrorPoint(Vector3 world)
        {
            Vector3 local = clayRoot.InverseTransformPoint(world);
            return clayRoot.TransformPoint(new Vector3(-local.x, local.y, local.z));
        }

        Vector3 MirrorVector(Vector3 world)
        {
            Vector3 local = clayRoot.InverseTransformVector(world);
            return clayRoot.TransformVector(new Vector3(-local.x, local.y, local.z));
        }

        bool NearMirrorPlane(Vector3 world)
        {
            return Mathf.Abs(clayRoot.InverseTransformPoint(world).x) < brushRadius * 0.15f;
        }

        // ---------- using the tools ----------

        void BeginStroke(ClayPart part, Vector3 localPoint, Vector3 worldPoint, Vector2 screen)
        {
            PushUndo();
            stroking = true;
            strokePart = part;
            active = part;
            RefreshUI();

            dragStartScreen = screen;
            dragStartWorld = worldPoint;
            dragPlane = new Plane(-studioCamera.transform.forward, worldPoint);

            switch (Tool)
            {
                case ClayTool.Grab:
                    part.Clay.ClearGrab();
                    part.Clay.BeginGrab(localPoint, LocalRadius(part), false);
                    if (Mirror && !NearMirrorPlane(worldPoint))
                    {
                        part.Clay.BeginGrab(part.Transform.InverseTransformPoint(MirrorPoint(worldPoint)), LocalRadius(part), true);
                    }
                    break;

                case ClayTool.Move:
                    // keep the spot you grabbed under your finger
                    dragGrabOffset = part.Transform.position - worldPoint;
                    snappedTo = null;
                    break;

                case ClayTool.Resize:
                    dragStartScale = part.Transform.localScale.x;
                    break;

                case ClayTool.Add:
                case ClayTool.Dent:
                case ClayTool.Smooth:
                    ApplyBrush(part, localPoint, worldPoint);
                    break;
            }
        }

        void ContinueStroke(Vector2 screen, Vector2 delta)
        {
            ClayPart part = strokePart;
            if (part == null || !part.Alive) return;

            Ray ray = studioCamera.ScreenPointToRay(screen);
            switch (Tool)
            {
                case ClayTool.Grab:
                {
                    if (!dragPlane.Raycast(ray, out float distance)) return;
                    Vector3 offsetWorld = ray.GetPoint(distance) - dragStartWorld;
                    part.Clay.DragGrab(part.Transform.InverseTransformVector(offsetWorld),
                                       part.Transform.InverseTransformVector(MirrorVector(offsetWorld)));
                    part.Clay.Refresh();
                    break;
                }

                case ClayTool.Move:
                    MoveShape(part, ray);
                    break;

                case ClayTool.Resize:
                {
                    // dragging up grows, down shrinks
                    float factor = Mathf.Pow(2f, (screen.y - dragStartScreen.y) / Mathf.Max(1f, resizePixelsPerDoubling));
                    float scale = Mathf.Clamp(dragStartScale * factor, minShapeScale, maxShapeScale);
                    part.Transform.localScale = Vector3.one * scale;
                    break;
                }

                case ClayTool.Spin:
                    part.Transform.Rotate(Vector3.up, -delta.x * turnSpeed, Space.World);
                    part.Transform.Rotate(studioCamera.transform.right, delta.y * turnSpeed, Space.World);
                    break;

                default:
                    // keep sculpting the shape this stroke started on, even if the finger drifts over another
                    if (TryHitPart(part, ray, out Vector3 localPoint, out Vector3 worldPoint, out _)) ApplyBrush(part, localPoint, worldPoint);
                    break;
            }
        }

        void ApplyBrush(ClayPart part, Vector3 localPoint, Vector3 worldPoint)
        {
            float dt = Time.deltaTime;
            float scale = Mathf.Max(0.01f, part.Transform.localScale.x);
            float amount = Tool == ClayTool.Smooth ? smoothSpeed * dt : pushSpeed * clayRadius * dt / scale;
            float radius = LocalRadius(part);

            part.Clay.Brush(Tool, localPoint, radius, amount);
            if (Mirror && !NearMirrorPlane(worldPoint))
            {
                part.Clay.Brush(Tool, part.Transform.InverseTransformPoint(MirrorPoint(worldPoint)), radius, amount);
            }
            part.Clay.Refresh();
        }

        // ---------- moving and snapping ----------

        // how far a shape reaches along a world direction from its middle
        float Reach(ClayPart part, Vector3 worldDirection)
        {
            Vector3 local = part.Transform.InverseTransformDirection(worldDirection);
            return part.Clay.Support(local.normalized) * part.Transform.localScale.x;
        }

        // Follows your finger. Over another shape it slides across that shape's surface and rests against it
        // (sunk in a little so it reads as joined); over empty space it moves freely in the view plane.
        void MoveShape(ClayPart part, Ray ray)
        {
            Vector3 centre;
            snappedTo = null;
            bool onSurface = false;

            if (snapToSurfaces && TryHitParts(ray, out ClayPart other, out _, out Vector3 hitPoint, out Vector3 hitNormal, part))
            {
                float reach = Reach(part, -hitNormal);
                centre = hitPoint + hitNormal * (reach * (1f - snapEmbed));
                snappedTo = other;
                onSurface = true;
                snapMarker.transform.position = hitPoint;
            }
            else
            {
                if (!dragPlane.Raycast(ray, out float distance)) return;
                centre = ray.GetPoint(distance) + dragGrabOffset;
            }

            // magnet toward the middle plane, with a guide line, for building symmetric things
            Vector3 local = clayRoot.InverseTransformPoint(centre);
            bool centred = Mathf.Abs(local.x) < centreSnap;
            if (centred) local.x = 0f;
            local = Vector3.ClampMagnitude(local, clayRadius * 2.5f);
            part.Transform.position = clayRoot.TransformPoint(local);

            snapMarker.SetActive(onSurface);
            centreGuide.SetActive(centred);
            if (centred) centreGuide.transform.localPosition = new Vector3(0f, 0f, local.z); // the line runs through the shape
        }

        // Let go close to another shape and it slides in until the two touch.
        void SettleOnRelease(ClayPart part)
        {
            Vector3 centre = part.Transform.position;
            float bestGap = float.MaxValue;
            Vector3 bestMove = Vector3.zero;

            foreach (ClayPart other in parts)
            {
                if (other == part || !other.Alive) continue;

                Vector3 toward = other.Transform.position - centre;
                if (toward.sqrMagnitude < 1e-6f) continue;
                Vector3 direction = toward.normalized;

                if (!TryHitPart(other, new Ray(centre, direction), out _, out Vector3 hitPoint, out _)) continue;

                float reach = Reach(part, direction);
                float embed = snapEmbed * reach;
                float gap = Vector3.Distance(centre, hitPoint) - reach;
                if (gap > releaseSnapDistance || gap < -embed || gap >= bestGap) continue;

                bestGap = gap;
                bestMove = direction * (gap + embed);
            }

            if (bestGap < float.MaxValue) part.Transform.position = centre + bestMove;
        }

        void HideSnapFeedback()
        {
            if (snapMarker != null) snapMarker.SetActive(false);
            if (centreGuide != null) centreGuide.SetActive(false);
            snappedTo = null;
        }

        void EndStroke()
        {
            if (stroking && Tool == ClayTool.Move && strokePart != null && strokePart.Alive && snappedTo == null)
            {
                SettleOnRelease(strokePart);
            }

            HideSnapFeedback();
            stroking = false;
            rotating = false;
            wheelPressing = false;
            gizmoDragging = false;
            strokePart = null;
        }

        // ---------- rotate rings ----------

        // the sculpture's own axes, so the rings line up with the clay however you've turned the view
        Vector3 RingAxis(int index)
        {
            return index == 0 ? clayRoot.right : index == 1 ? clayRoot.up : clayRoot.forward;
        }

        static void RingBasis(Vector3 axis, out Vector3 u, out Vector3 v)
        {
            Vector3 reference = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up;
            u = Vector3.Cross(axis, reference).normalized;
            v = Vector3.Cross(axis, u);
        }

        Vector3 RingPoint(Vector3 centre, Vector3 u, Vector3 v, float radius, float angle)
        {
            return centre + (u * Mathf.Cos(angle) + v * Mathf.Sin(angle)) * radius;
        }

        // just outside the shape, so the rings stay clear of the clay
        float RingRadius(ClayPart part)
        {
            return part.Clay.MaxRadius() * part.Transform.localScale.x * 1.15f + 0.05f;
        }

        bool RingsVisible => IsActive && !OnWheel && Tool == ClayTool.Rotate && active != null && active.Alive;

        void UpdateRotateRings()
        {
            bool show = RingsVisible;
            if (gizmoRoot.activeSelf != show) gizmoRoot.SetActive(show);
            if (!show) return;

            Vector3 centre = gizmoDragging ? gizmoCentre : active.Transform.position;
            float radius = RingRadius(active);
            float width = cameraDistance * 0.014f; // roughly constant thickness on screen

            for (int i = 0; i < 3; i++)
            {
                RingBasis(RingAxis(i), out Vector3 u, out Vector3 v);
                for (int k = 0; k <= RingPoints; k++)
                {
                    ringBuffer[k] = RingPoint(centre, u, v, radius, k / (float)RingPoints * Mathf.PI * 2f);
                }
                gizmoRings[i].SetPositions(ringBuffer);
                gizmoRings[i].widthMultiplier = width * (gizmoDragging && i == gizmoAxisIndex ? 1.5f : 1f);
                gizmoMaterials[i].SetColor("_BaseColor", gizmoDragging && i == gizmoAxisIndex ? RingActiveColor : RingColors[i]);
            }
        }

        // grabs the ring nearest the finger, if one is close enough
        bool TryBeginRingDrag(Vector2 screen)
        {
            if (!RingsVisible) return false;

            Vector3 centre = active.Transform.position;
            float radius = RingRadius(active);
            float bestDistance = ringPickPixels * (Screen.height / 1080f);
            int bestAxis = -1;
            Vector3 bestPoint = Vector3.zero;

            for (int i = 0; i < 3; i++)
            {
                RingBasis(RingAxis(i), out Vector3 u, out Vector3 v);
                for (int k = 0; k < RingPoints; k++)
                {
                    Vector3 point = RingPoint(centre, u, v, radius, k / (float)RingPoints * Mathf.PI * 2f);
                    Vector3 onScreen = studioCamera.WorldToScreenPoint(point);
                    if (onScreen.z <= 0f) continue;

                    float distance = Vector2.Distance(screen, new Vector2(onScreen.x, onScreen.y));
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestAxis = i;
                        bestPoint = point;
                    }
                }
            }

            if (bestAxis < 0) return false;

            PushUndo();
            gizmoDragging = true;
            gizmoAxisIndex = bestAxis;
            gizmoAxis = RingAxis(bestAxis);
            gizmoCentre = centre;
            gizmoStartOffset = bestPoint - centre;
            gizmoStartRotation = active.Transform.rotation;
            gizmoAngle = 0f;
            strokePart = active;
            return true;
        }

        // Turns the shape by however far your finger travels along the ring at the grabbed point. Measuring along
        // the ring on screen works from any view angle, mouse or touch.
        void DragRing(Vector2 screenDelta)
        {
            ClayPart part = strokePart;
            if (part == null || !part.Alive) return;

            float shown = SnapAngle(gizmoAngle);
            Vector2 here = ScreenXY(gizmoCentre + Quaternion.AngleAxis(shown, gizmoAxis) * gizmoStartOffset);
            Vector2 next = ScreenXY(gizmoCentre + Quaternion.AngleAxis(shown + 1f, gizmoAxis) * gizmoStartOffset);
            Vector2 perDegree = next - here;
            float lengthSq = perDegree.sqrMagnitude;
            if (lengthSq < 0.0004f) return; // ring is edge-on at this point - turn the view to get a better grip

            gizmoAngle += Vector2.Dot(screenDelta, perDegree) / lengthSq;
            shown = SnapAngle(gizmoAngle);
            part.Transform.rotation = Quaternion.AngleAxis(shown, gizmoAxis) * gizmoStartRotation;
            ui.ShowMessage($"{Mathf.Round(shown)} degrees");
        }

        Vector2 ScreenXY(Vector3 world)
        {
            Vector3 p = studioCamera.WorldToScreenPoint(world);
            return new Vector2(p.x, p.y);
        }

        // soft snap: near a multiple of the step it sticks to it, in between it moves freely
        float SnapAngle(float angle)
        {
            if (!AngleSnap || rotateSnapDegrees <= 0f) return angle;
            float nearest = Mathf.Round(angle / rotateSnapDegrees) * rotateSnapDegrees;
            return Mathf.Abs(angle - nearest) < rotateSnapTolerance ? nearest : angle;
        }

        // ---------- placing ----------

        GameObject CreateSculpture()
        {
            // bounds of everything, in the clay's own space
            Vector3 min = Vector3.one * float.MaxValue;
            Vector3 max = Vector3.one * float.MinValue;
            foreach (ClayPart part in parts)
            {
                if (!part.Alive) continue;
                Matrix4x4 toClay = Matrix4x4.TRS(part.Transform.localPosition, part.Transform.localRotation, part.Transform.localScale);
                foreach (Vector3 vertex in part.Clay.Mesh.vertices)
                {
                    Vector3 p = toClay.MultiplyPoint3x4(vertex);
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                }
            }

            Vector3 centre = (min + max) * 0.5f;
            Vector3 size = max - min;
            float scale = placedSize / Mathf.Max(size.x, size.y, size.z, 0.01f);

            // one object per sculpture, one child per shape (each shape keeps its own paint)
            var sculpture = new GameObject("Sculpture");
            sculpture.transform.localScale = Vector3.one * scale;
            foreach (ClayPart part in parts)
            {
                if (!part.Alive) continue;

                var child = new GameObject(part.Shape.ToString());
                child.transform.SetParent(sculpture.transform, false);
                child.transform.localPosition = part.Transform.localPosition - centre;
                child.transform.localRotation = part.Transform.localRotation;
                child.transform.localScale = part.Transform.localScale;

                Mesh mesh = part.Clay.BakeCopy();
                child.AddComponent<MeshFilter>().sharedMesh = mesh;
                child.AddComponent<MeshRenderer>().sharedMaterial = new Material(part.Material) { name = "Sculpture_" + part.Shape };
                child.AddComponent<MeshCollider>().sharedMesh = mesh;

                var paintable = child.AddComponent<Paintable>();
                paintable.texelsPerMeter = paintTexelsPerMeter;
                paintable.minTextureSize = 256;
                paintable.maxTextureSize = 1024;
            }

            // the glove can pick it up and resize it, like the furniture
            sculpture.AddComponent<Grabbable>();

            // set it down on whatever is in front of the player, facing them
            Transform view = worldCamera.transform;
            Ray ray = new Ray(view.position, view.forward);
            Vector3 position;
            Vector3 up = Vector3.up;
            if (Physics.Raycast(ray, out RaycastHit hit, placeReach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                position = hit.point + hit.normal * (size.y * scale * 0.5f);
                up = hit.normal;
            }
            else
            {
                position = ray.GetPoint(Mathf.Min(3f, placeReach));
            }

            Vector3 facing = Vector3.ProjectOnPlane(-view.forward, up);
            if (facing.sqrMagnitude < 1e-4f) facing = Vector3.back;
            sculpture.transform.SetPositionAndRotation(position, Quaternion.LookRotation(facing, up));
            return sculpture;
        }
    }
}
