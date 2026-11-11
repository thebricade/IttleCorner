using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace InkFlip
{
    // Mini-game 2 of the Lines room: a reverse colouring book.
    // The wall pictures are blobs of colour with the outlines missing. Look at one and press F to open it, then draw
    // the outlines yourself. When the lines match the shapes, press "Pop it out!" and the picture steps off the wall
    // as a cut-out you can pick up with the glove and use to decorate the room.
    public class ColoringBook : MonoBehaviour
    {
        public FirstPersonController player;
        public ToolSwitcher tools;
        public RoomZone zone;
        public ColoringPanel[] panels;

        [Header("Look")]
        [Tooltip("Lit material used for the wall pictures.")]
        public Material panelMaterial;
        [Tooltip("Lit, alpha-clipped, two-sided material used for the cut-outs.")]
        public Material cutoutMaterial;
        public Key interactKey = Key.F;
        [Tooltip("How far away you can start a picture from.")]
        public float lookDistance = 4.5f;
        [Tooltip("How directly you have to face it (1 = dead centre, 0.8 = within about 35 degrees).")]
        [Range(0.5f, 1f)] public float facingNeeded = 0.8f;

        [Header("Drawing")]
        public int textureSize = 512;
        [Tooltip("Pencil radius in texture pixels.")]
        public float brushRadius = 4f;
        [Tooltip("How far (texture pixels) a line may stray from an edge and still count.")]
        public int tolerance = 10;
        [Range(0.2f, 1f)] public float coverageNeeded = 0.6f;
        [Range(0.1f, 1f)] public float precisionNeeded = 0.45f;
        [Tooltip("Size of a popped-out cut-out, in metres (longest side).")]
        public float propSize = 1f;

        [Header("Goal")]
        [Tooltip("How many pictures the room needs.")]
        public int picturesNeeded = 3;

        public int DoneCount { get; private set; }
        public bool IsOpen => current != null;
        public event Action<ColoringPanel> PictureCompleted;

        static readonly Color Paper = new Color(0.98f, 0.97f, 0.92f);
        static readonly Color32 InkColor = new Color32(30, 24, 52, 255);

        // the open page
        ColoringPanel current;
        ColoringPictures.Picture picture;
        byte[] ink;
        Color32[] pixels;
        bool[] edge;
        bool[] edgeNear;
        int edgeCount;
        Texture2D display;
        bool drawing;
        Vector2 lastPoint;
        float coverage;
        float precision;
        bool passes;

        // ui
        RectTransform boardRoot;
        RectTransform pageRect;
        RawImage pageImage;
        Text titleText;
        Text hintText;
        Text meterText;
        Image meterFill;
        Button popButton;
        Text popLabel;

        SelectionDrawMode drawMode;
        SculptStudio studio;
        Camera lookCamera;
        readonly Dictionary<ColoringPanel, Material> panelMaterials = new Dictionary<ColoringPanel, Material>();

        void Start()
        {
            if (panels == null) return;
            foreach (ColoringPanel panel in panels)
            {
                if (panel == null || panel.panelRenderer == null) continue;
                try
                {
                ColoringPictures.Picture generated = ColoringPictures.Generate(panel.pictureIndex, textureSize);
                Color32[] colours = new Color32[generated.region.Length];
                PaintBase(generated, colours);
                Texture2D texture = MakeTexture(colours, textureSize);
                SetPanelTexture(panel, texture);
                }
                catch (System.Exception problem)
                {
                    // the builder bakes the same picture into the material, so the wall still shows it
                    Debug.LogError($"ColoringBook: couldn't set up {panel.name}: {problem}", panel);
                }
            }
            Debug.Log($"ColoringBook: {panels.Length} wall pictures ready. Face one (within {lookDistance} m) and press {interactKey}.", this);
        }

        ColoringPanel highlighted;

        // the picture you are facing glows a little, so it is clear which one F will open
        void SetHighlight(ColoringPanel panel)
        {
            if (panel == highlighted) return;
            if (highlighted != null && panelMaterials.TryGetValue(highlighted, out Material old) && old != null)
            {
                old.SetColor("_EmissionColor", Color.black);
            }
            highlighted = panel;
            if (panel != null && panelMaterials.TryGetValue(panel, out Material now) && now != null)
            {
                now.SetColor("_EmissionColor", new Color(0.38f, 0.38f, 0.34f));
            }
        }

        void SetPanelTexture(ColoringPanel panel, Texture2D texture)
        {
            if (!panelMaterials.TryGetValue(panel, out Material material) || material == null)
            {
                Material source = panel.panelRenderer.sharedMaterial != null ? panel.panelRenderer.sharedMaterial : panelMaterial;
                material = source != null ? new Material(source) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                panelMaterials[panel] = material;
                panel.panelRenderer.sharedMaterial = material;
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
            }
            if (material.HasProperty("_EmissionMap")) material.SetTexture("_EmissionMap", texture);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            material.mainTexture = texture;
        }

        static Texture2D MakeTexture(Color32[] colours, int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 4,
            };
            texture.SetPixels32(colours);
            texture.Apply(true);
            return texture;
        }

        static void PaintBase(ColoringPictures.Picture pic, Color32[] target)
        {
            Color32 paper = Paper;
            for (int i = 0; i < target.Length; i++)
            {
                byte id = pic.region[i];
                target[i] = id == 0 ? paper : pic.palette[id];
            }
        }

        void OnDestroy()
        {
            if (current != null) LineRoomUI.Busy = false;
        }

        // ---------- looking at the wall ----------

        void Update()
        {
            if (current != null)
            {
                UpdateBoard();
                return;
            }

            LineRoomUI ui = LineRoomUI.Instance;
            ColoringPanel looked = FindLookedPanel();
            SetHighlight(looked);
            if (looked == null)
            {
                ui.SetPrompt(this, "");
                Keyboard k = Keyboard.current;
                if (k != null && k[interactKey].wasPressedThisFrame) Debug.Log("ColoringBook (F pressed, no picture found): " + Diagnose());
                return;
            }

            if (looked.IsDone)
            {
                ui.SetPrompt(this, $"The {looked.Title} is out in the room - pick it up with the glove!");
                return;
            }

            ui.SetPrompt(this, $"Press {interactKey} to draw the lines of the {looked.Title}");
            Keyboard keyboard = Keyboard.current;
            Gamepad pad = Gamepad.current;
            if ((keyboard != null && keyboard[interactKey].wasPressedThisFrame) || (pad != null && pad.buttonWest.wasPressedThisFrame))
            {
                Open(looked);
            }
        }

        // says why no picture can be opened right now (printed to the Console when F is pressed and nothing happens)
        string Diagnose()
        {
            if (panels == null || panels.Length == 0) return "no pictures are wired into this ColoringBook.";
            if (zone == null) return "the ColoringBook has no Zone set.";
            if (!zone.PlayerInside) return $"you are not inside zone '{zone.roomId}' (player at {(zone.player != null ? zone.player.position.ToString() : "unknown")}).";
            if (LineRoomUI.Busy) return "another board is open (LineRoomUI.Busy is stuck on).";
            if (!FirstPersonController.InputCaptured) return "the mouse isn't captured (click the Game view).";
            if (lookCamera == null) return "no main camera.";

            var text = new System.Text.StringBuilder("closest pictures: ");
            Vector3 eye = lookCamera.transform.position;
            foreach (ColoringPanel panel in panels)
            {
                if (panel == null) continue;
                Vector3 to = panel.transform.position - eye;
                float d = to.magnitude;
                Vector3 dir = to / Mathf.Max(d, 0.01f);
                string blocker = "clear";
                if (Physics.Raycast(eye, dir, out RaycastHit hit, d, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    && hit.collider.GetComponentInParent<ColoringPanel>() != panel) blocker = "blocked by " + hit.collider.name;
                text.Append($"[{panel.name}: {d:0.0}m, facing {Vector3.Dot(lookCamera.transform.forward, dir):0.00} (need {facingNeeded}), front {Vector3.Dot(panel.transform.forward, dir):0.00} (need 0.15), {blocker}] ");
            }
            return text.ToString();
        }

        // The picture you are facing, within a forgiving cone (no need to aim the crosshair exactly), as long as nothing is in the way.
        ColoringPanel FindLookedPanel()
        {
            if (panels == null || zone == null || !zone.PlayerInside || LineRoomUI.Busy) return null;

            if (lookCamera == null) lookCamera = Camera.main;
            if (drawMode == null) drawMode = FindAnyObjectByType<SelectionDrawMode>();
            if (studio == null) studio = FindAnyObjectByType<SculptStudio>();
            if (lookCamera == null) return null;
            if ((drawMode != null && drawMode.IsActive) || (studio != null && studio.IsActive)) return null;
            if (DialogueRunner.Instance != null && DialogueRunner.Instance.IsActive) return null;
            if (!FirstPersonController.InputCaptured) return null;

            Vector3 eye = lookCamera.transform.position;
            Vector3 forward = lookCamera.transform.forward;
            ColoringPanel best = null;
            float bestScore = float.MinValue;

            foreach (ColoringPanel panel in panels)
            {
                if (panel == null) continue;
                Vector3 to = panel.transform.position - eye;
                float distance = to.magnitude;
                if (distance > lookDistance || distance < 0.01f) continue;

                Vector3 direction = to / distance;
                float facing = Vector3.Dot(forward, direction);
                if (facing < facingNeeded) continue;
                if (Vector3.Dot(panel.transform.forward, direction) < 0.15f) continue; // you are behind or beside it

                // a wall, table or person in between blocks it
                if (Physics.Raycast(eye, direction, out RaycastHit hit, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    && hit.collider.GetComponentInParent<ColoringPanel>() != panel
                    && hit.distance < distance - 0.25f)
                {
                    continue;
                }

                float score = facing - distance * 0.03f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = panel;
                }
            }
            return best;
        }

        // ---------- the page ----------

        void Open(ColoringPanel panel)
        {
            BuildBoard();

            current = panel;
            picture = ColoringPictures.Generate(panel.pictureIndex, textureSize);
            int n = picture.region.Length;
            ink = new byte[n];
            pixels = new Color32[n];
            PaintBase(picture, pixels);
            ComputeEdges();

            if (display == null || display.width != textureSize)
            {
                display = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            }
            display.SetPixels32(pixels);
            display.Apply();
            pageImage.texture = display;

            coverage = precision = 0f;
            passes = false;
            drawing = false;
            titleText.text = $"Draw the lines of the {panel.Title}";
            hintText.text = "The colours have no outlines. Trace around every shape with the pencil!";
            RefreshMeter();

            LineRoomUI.Busy = true;
            LineRoomUI.Instance.SetPrompt(this, "");
            if (player == null) player = FindAnyObjectByType<FirstPersonController>();
            if (tools == null) tools = FindAnyObjectByType<ToolSwitcher>();
            if (player != null) player.enabled = false;
            if (tools != null) tools.enabled = false;
            FirstPersonController.SetCursorCaptured(false);
            boardRoot.gameObject.SetActive(true);
        }

        void CloseBoard()
        {
            if (boardRoot != null) boardRoot.gameObject.SetActive(false);
            current = null;
            drawing = false;
            LineRoomUI.Busy = false;
            if (player != null) player.enabled = true;
            if (tools != null) tools.enabled = true;
            FirstPersonController.SetCursorCaptured(true);
        }

        void ClearPage()
        {
            Array.Clear(ink, 0, ink.Length);
            PaintBase(picture, pixels);
            display.SetPixels32(pixels);
            display.Apply();
            coverage = precision = 0f;
            passes = false;
            RefreshMeter();
        }

        void BuildBoard()
        {
            if (boardRoot != null) return;

            HudFactory.EnsureEventSystem();
            boardRoot = HudFactory.CreateCanvas("ColoringBoard", transform, 60);

            Image dim = HudFactory.CreateImage("Dim", boardRoot, null, new Color(0.05f, 0.04f, 0.12f, 0.9f), 0f);
            dim.rectTransform.anchorMin = Vector2.zero;
            dim.rectTransform.anchorMax = Vector2.one;
            dim.rectTransform.offsetMin = dim.rectTransform.offsetMax = Vector2.zero;

            titleText = HudFactory.CreateText("Title", boardRoot, "", 46, TextAnchor.MiddleCenter);
            Place(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -55f), new Vector2(1500f, 80f));
            hintText = HudFactory.CreateText("Hint", boardRoot, "", 28, TextAnchor.MiddleCenter);
            Place(hintText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(1500f, 50f));
            hintText.color = new Color(1f, 1f, 1f, 0.75f);

            var pageObject = new GameObject("Page", typeof(RectTransform), typeof(RawImage));
            pageObject.transform.SetParent(boardRoot, false);
            pageImage = pageObject.GetComponent<RawImage>();
            pageImage.raycastTarget = false;
            pageRect = pageImage.rectTransform;
            Place(pageRect, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(700f, 700f));

            meterText = HudFactory.CreateText("Meter", boardRoot, "", 30, TextAnchor.MiddleLeft);
            Place(meterText.rectTransform, new Vector2(0f, 0.5f), new Vector2(110f, 120f), new Vector2(480f, 50f));
            Image meterBack = HudFactory.CreateImage("MeterBack", boardRoot, null, new Color(0f, 0f, 0f, 0.6f), 0f);
            Place(meterBack.rectTransform, new Vector2(0f, 0.5f), new Vector2(110f, 70f), new Vector2(440f, 26f));
            meterFill = HudFactory.CreateImage("MeterFill", meterBack.rectTransform, null, new Color(0.4f, 0.9f, 0.5f), 0f);
            meterFill.rectTransform.anchorMin = new Vector2(0f, 0f);
            meterFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            meterFill.rectTransform.pivot = new Vector2(0f, 0.5f);
            meterFill.rectTransform.anchoredPosition = new Vector2(3f, 0f);

            Button clear = HudFactory.CreateButton("StartOver", boardRoot, "Start over", new Vector2(280f, 80f), ClearPage, out _, out Text clearLabel);
            clearLabel.fontSize = 30;
            Place((RectTransform)clear.transform, new Vector2(1f, 0.5f), new Vector2(-250f, 40f), new Vector2(280f, 80f));

            popButton = HudFactory.CreateButton("Pop", boardRoot, "", new Vector2(280f, 80f), OnPop, out _, out popLabel);
            popLabel.fontSize = 30;
            Place((RectTransform)popButton.transform, new Vector2(1f, 0.5f), new Vector2(-250f, -60f), new Vector2(280f, 80f));

            Text cancelHint = HudFactory.CreateText("Cancel", boardRoot, "Esc: leave this page", 24, TextAnchor.MiddleCenter);
            Place(cancelHint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(600f, 40f));
            cancelHint.color = new Color(1f, 1f, 1f, 0.55f);

            boardRoot.gameObject.SetActive(false);
        }

        static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        void RefreshMeter()
        {
            meterText.text = $"Outline:  {Mathf.RoundToInt(coverage * 100f)}%   (need {Mathf.RoundToInt(coverageNeeded * 100f)}%)";
            float width = 434f * Mathf.Clamp01(coverage / Mathf.Max(0.01f, coverageNeeded));
            meterFill.rectTransform.sizeDelta = new Vector2(width, -6f);
            meterFill.color = passes ? new Color(0.4f, 0.95f, 0.5f) : new Color(1f, 0.75f, 0.3f);
            popButton.interactable = passes;
            popLabel.text = passes ? "Pop it out!" : "Keep drawing...";
        }

        // ---------- drawing ----------

        void UpdateBoard()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                CloseBoard();
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 screen = mouse.position.ReadValue();

            bool over = RectTransformUtility.RectangleContainsScreenPoint(pageRect, screen, null);
            if (mouse.leftButton.wasPressedThisFrame && over)
            {
                drawing = true;
                lastPoint = ToPixel(screen);
                Stamp(lastPoint, lastPoint);
            }
            else if (drawing && mouse.leftButton.isPressed)
            {
                Vector2 point = ToPixel(screen);
                if ((point - lastPoint).sqrMagnitude >= 1f)
                {
                    Stamp(lastPoint, point);
                    lastPoint = point;
                }
            }

            if (drawing && mouse.leftButton.wasReleasedThisFrame)
            {
                drawing = false;
                Score();
            }
        }

        Vector2 ToPixel(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(pageRect, screen, null, out Vector2 local);
            Rect r = pageRect.rect;
            float u = Mathf.Clamp01((local.x - r.xMin) / r.width);
            float v = Mathf.Clamp01((local.y - r.yMin) / r.height);
            return new Vector2(u * (textureSize - 1), v * (textureSize - 1));
        }

        void Stamp(Vector2 from, Vector2 to)
        {
            float distance = Vector2.Distance(from, to);
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / 1.5f));
            int size = textureSize;
            int r = Mathf.CeilToInt(brushRadius);
            float r2 = brushRadius * brushRadius;

            for (int s = 0; s <= steps; s++)
            {
                Vector2 p = Vector2.Lerp(from, to, s / (float)steps);
                int cx = Mathf.RoundToInt(p.x), cy = Mathf.RoundToInt(p.y);
                for (int y = Mathf.Max(0, cy - r); y <= Mathf.Min(size - 1, cy + r); y++)
                {
                    for (int x = Mathf.Max(0, cx - r); x <= Mathf.Min(size - 1, cx + r); x++)
                    {
                        float dx = x - p.x, dy = y - p.y;
                        if (dx * dx + dy * dy > r2) continue;
                        int i = y * size + x;
                        ink[i] = 1;
                        pixels[i] = InkColor;
                    }
                }
            }

            display.SetPixels32(pixels);
            display.Apply();
        }

        // ---------- scoring ----------

        // pixels at a boundary between two colours (or a colour and the paper)
        void ComputeEdges()
        {
            int size = textureSize;
            int n = size * size;
            edge = new bool[n];
            const int Reach = 3;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    byte me = picture.region[y * size + x];
                    bool boundary =
                        Region(x - Reach, y) != me || Region(x + Reach, y) != me ||
                        Region(x, y - Reach) != me || Region(x, y + Reach) != me;
                    edge[y * size + x] = boundary;
                }
            }

            edgeCount = 0;
            foreach (bool e in edge) if (e) edgeCount++;
            edgeNear = Dilate(edge, tolerance + 4);
        }

        byte Region(int x, int y)
        {
            int size = textureSize;
            if (x < 0 || y < 0 || x >= size || y >= size) return 0;
            return picture.region[y * size + x];
        }

        void Score()
        {
            int n = ink.Length;
            var inkMask = new bool[n];
            int inkCount = 0;
            for (int i = 0; i < n; i++)
            {
                if (ink[i] != 0)
                {
                    inkMask[i] = true;
                    inkCount++;
                }
            }

            bool[] inkNear = Dilate(inkMask, tolerance);

            int covered = 0;
            for (int i = 0; i < n; i++) if (edge[i] && inkNear[i]) covered++;
            coverage = edgeCount > 0 ? covered / (float)edgeCount : 0f;

            int good = 0;
            for (int i = 0; i < n; i++) if (inkMask[i] && edgeNear[i]) good++;
            precision = inkCount > 0 ? good / (float)inkCount : 0f;

            passes = coverage >= coverageNeeded && precision >= precisionNeeded;
            if (coverage >= coverageNeeded && precision < precisionNeeded) hintText.text = "Stay close to the edges of the shapes!";
            else if (passes) hintText.text = "That looks great! Pop it out - or keep tracing for a neater picture.";
            else hintText.text = "Trace around every shape with the pencil!";
            RefreshMeter();
        }

        // grow a mask by 'radius' pixels in every direction (a square)
        bool[] Dilate(bool[] mask, int radius)
        {
            int size = textureSize;
            var horizontal = new bool[mask.Length];
            for (int y = 0; y < size; y++)
            {
                int row = y * size;
                int last = -100000;
                for (int x = 0; x < size; x++)
                {
                    if (mask[row + x]) last = x;
                    if (x - last <= radius) horizontal[row + x] = true;
                }
                last = 100000;
                for (int x = size - 1; x >= 0; x--)
                {
                    if (mask[row + x]) last = x;
                    if (last - x <= radius) horizontal[row + x] = true;
                }
            }

            var result = new bool[mask.Length];
            for (int x = 0; x < size; x++)
            {
                int last = -100000;
                for (int y = 0; y < size; y++)
                {
                    if (horizontal[y * size + x]) last = y;
                    if (y - last <= radius) result[y * size + x] = true;
                }
                last = 100000;
                for (int y = size - 1; y >= 0; y--)
                {
                    if (horizontal[y * size + x]) last = y;
                    if (last - y <= radius) result[y * size + x] = true;
                }
            }
            return result;
        }

        // ---------- popping it out ----------

        void OnPop()
        {
            if (current == null || !passes) return;

            ColoringPanel panel = current;
            GameObject prop = MakeCutout(panel);

            // the wall keeps the finished picture, lines and all
            SetPanelTexture(panel, MakeTexture((Color32[])pixels.Clone(), textureSize));
            panel.IsDone = true;
            DoneCount++;

            CloseBoard();
            PictureCompleted?.Invoke(panel);

            string left = DoneCount >= picturesNeeded ? "" : $"  ({DoneCount} / {picturesNeeded})";
            LineRoomUI.Instance.Say($"The {panel.Title} popped out!{left}", 2.5f);
            if (prop != null) Debug.Log("ColoringBook: spawned " + prop.name);
        }

        GameObject MakeCutout(ColoringPanel panel)
        {
            int size = textureSize;
            int n = size * size;

            // outline everything with a thin dark rim, so lines that stop short still give a clean edge
            var silhouette = new bool[n];
            for (int i = 0; i < n; i++) silhouette[i] = picture.region[i] != 0 || ink[i] != 0;
            bool[] rim = Dilate(silhouette, 3);

            var colours = new Color32[n];
            int minX = size, minY = size, maxX = -1, maxY = -1;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int i = y * size + x;
                    Color32 c;
                    if (ink[i] != 0) c = InkColor;
                    else if (picture.region[i] != 0) c = picture.palette[picture.region[i]];
                    else if (rim[i]) c = InkColor;
                    else c = new Color32(InkColor.r, InkColor.g, InkColor.b, 0);
                    colours[i] = c;

                    if (c.a != 0)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }
            if (maxX < minX) return null;

            int w = maxX - minX + 1, h = maxY - minY + 1;
            var cropped = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++) cropped[y * w + x] = colours[(y + minY) * size + (x + minX)];
            }
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, true) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(cropped);
            texture.Apply(true);

            float longest = Mathf.Max(w, h);
            float worldW = propSize * w / longest;
            float worldH = propSize * h / longest;

            // stand it on the floor in front of the picture, facing the room
            Transform wall = panel.transform;
            Vector3 intoRoom = -wall.forward;
            intoRoom.y = 0f;
            intoRoom.Normalize();
            Vector3 spot = new Vector3(wall.position.x, 0f, wall.position.z) + intoRoom * 1.1f;

            var root = new GameObject("Cutout_" + panel.Title);
            root.transform.position = spot;
            root.transform.rotation = Quaternion.LookRotation(-intoRoom, Vector3.up); // the quad's front looks along -forward

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Quad);
            visual.name = "Visual";
            Destroy(visual.GetComponent<MeshCollider>());
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(0f, worldH * 0.5f, 0f);
            visual.transform.localScale = new Vector3(worldW, worldH, 1f);

            var box = visual.AddComponent<BoxCollider>();
            box.size = new Vector3(1f, 1f, 0.08f);

            Material material = cutoutMaterial != null ? new Material(cutoutMaterial) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            material.mainTexture = texture;
            var renderer = visual.GetComponent<Renderer>();
            renderer.sharedMaterial = material;

            root.AddComponent<Grabbable>();
            visual.AddComponent<PopIn>();
            return root;
        }

        // small bounce when a cut-out appears
        class PopIn : MonoBehaviour
        {
            Vector3 target;
            float time;

            void Start()
            {
                target = transform.localScale;
                transform.localScale = target * 0.05f;
            }

            void Update()
            {
                time += Time.deltaTime;
                float t = Mathf.Clamp01(time / 0.5f);
                float overshoot = 1f + 0.18f * Mathf.Sin(t * Mathf.PI) * (1f - t);
                transform.localScale = target * Mathf.Lerp(0.05f, 1f, t) * overshoot;
                if (t >= 1f)
                {
                    transform.localScale = target;
                    Destroy(this);
                }
            }
        }
    }
}
