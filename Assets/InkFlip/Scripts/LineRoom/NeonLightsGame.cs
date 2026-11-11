using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace InkFlip
{
    // Mini-game 3 of the Lines room: lights out.
    // Flip the switch and the room goes dark except for one far-off light. Your steps leave a glowing neon line on the
    // floor. Draw one long line and walk it to the far light - but the line may never touch itself, and ghosts are
    // drifting around in the dark. Touch your own line or a ghost and it fizzles out so you can start again.
    // Turn the lights back on and the neon vanishes, like a blacklight.
    public class NeonLightsGame : MonoBehaviour
    {
        public RoomZone zone;
        public FirstPersonController player;

        [Header("Switch and far light")]
        [Tooltip("The switch plate. Look at it and press the key.")]
        public Collider switchCollider;
        [Tooltip("The little lever that flips when you use the switch (optional).")]
        public Transform switchLever;
        public Key switchKey = Key.F;
        public float reach = 2.8f;
        [Tooltip("The far-away light you walk to. Only lit while the room is dark.")]
        public Transform beacon;
        public float beaconReachDistance = 1.0f;

        [Header("The game")]
        [Tooltip("How long the neon line has to get (metres) before the far light finishes it.")]
        public float targetLength = 14f;
        public int ghostCount = 3;
        public float ghostSpeed = 1.1f;
        public float lineWidth = 0.12f;
        public float glowWidth = 0.5f;

        [Header("Look")]
        public Material coreMaterial;
        [Tooltip("Soft additive material for the glow around the line. If empty there is no glow strip.")]
        public Material glowMaterial;
        public Material ghostMaterial;
        public Color[] neonColors =
        {
            new Color(0.1f, 1f, 0.75f), new Color(1f, 0.2f, 0.85f), new Color(0.6f, 1f, 0.15f), new Color(1f, 0.55f, 0.1f),
        };

        public bool LightsOn { get; private set; } = true;
        public bool Solved { get; private set; }
        public event Action SolvedEvent;

        // the room's real lighting, saved so it can be put back
        float sunIntensity;
        Color skyAmbient, equatorAmbient, groundAmbient;
        float reflectionIntensity;
        Light sun;
        bool lightingSaved;
        float darkness;

        // the neon line
        readonly List<Vector3> points = new List<Vector3>();
        readonly List<float> arc = new List<float>();
        float length;
        LineRenderer core;
        LineRenderer glow;
        Light headLight;
        Material coreInstance;
        Material glowInstance;
        int run;
        Color neon;
        float flashTimer;
        float safeTimer;
        float trailScale = 1f;
        float winTimer = -1f;

        class Ghost
        {
            public Transform root;
            public Vector3 target;
            public float seed;
        }

        readonly List<Ghost> ghosts = new List<Ghost>();
        Vector3 boundsMin, boundsMax;

        SelectionDrawMode drawMode;
        SculptStudio studio;
        Camera lookCamera;
        Light beaconLight;

        void Start()
        {
            if (player == null) player = FindAnyObjectByType<FirstPersonController>();

            if (zone != null)
            {
                Vector3 centre = zone.transform.TransformPoint(zone.center);
                Vector3 half = zone.size * 0.5f;
                boundsMin = new Vector3(centre.x - half.x + 0.6f, 0f, centre.z - half.z + 0.6f);
                boundsMax = new Vector3(centre.x + half.x - 0.6f, 0f, centre.z + half.z - 0.6f);
            }

            BuildTrail();
            BuildGhosts();
            if (beacon != null)
            {
                beaconLight = beacon.GetComponentInChildren<Light>(true);
                beacon.gameObject.SetActive(false);
            }
            SetLever(true);
        }

        void OnDisable() => RestoreLighting();

        void OnDestroy()
        {
            RestoreLighting();
            if (coreInstance != null) Destroy(coreInstance);
            if (glowInstance != null) Destroy(glowInstance);
        }

        // ---------- building ----------

        void BuildTrail()
        {
            var holder = new GameObject("NeonTrail");
            holder.transform.SetParent(transform, false);
            // lines in 'TransformZ' mode lie flat against the holder's Z axis, so point that straight up
            holder.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

            core = holder.AddComponent<LineRenderer>();
            ConfigureLine(core, lineWidth);
            coreInstance = MakeMaterial(coreMaterial);
            core.sharedMaterial = coreInstance;

            if (glowMaterial != null)
            {
                var glowHolder = new GameObject("NeonGlow");
                glowHolder.transform.SetParent(transform, false);
                glowHolder.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
                glow = glowHolder.AddComponent<LineRenderer>();
                ConfigureLine(glow, glowWidth);
                glowInstance = new Material(glowMaterial);
                glow.sharedMaterial = glowInstance;
            }

            var lightObject = new GameObject("NeonHeadLight");
            lightObject.transform.SetParent(transform, false);
            headLight = lightObject.AddComponent<Light>();
            headLight.type = LightType.Point;
            headLight.range = 4f;
            headLight.intensity = 2f;
            headLight.shadows = LightShadows.None;

            SetTrailVisible(false);
        }

        static void ConfigureLine(LineRenderer line, float width)
        {
            line.useWorldSpace = true;
            line.alignment = LineAlignment.TransformZ;
            line.widthMultiplier = width;
            line.numCapVertices = 6;
            line.numCornerVertices = 5;
            line.textureMode = LineTextureMode.Stretch;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.positionCount = 0;
        }

        static Material MakeMaterial(Material template)
        {
            if (template != null) return new Material(template);
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            return new Material(shader);
        }

        void BuildGhosts()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            for (int i = 0; i < ghostCount; i++)
            {
                var root = new GameObject("Ghost_" + i).transform;
                root.SetParent(transform, false);

                Material body = ghostMaterial != null ? new Material(ghostMaterial) : new Material(shader);
                if (ghostMaterial == null)
                {
                    body.color = new Color(0.78f, 0.9f, 1f);
                    if (body.HasProperty("_BaseColor")) body.SetColor("_BaseColor", body.color);
                }
                Material dark = new Material(shader);
                dark.color = new Color(0.05f, 0.05f, 0.1f);
                if (dark.HasProperty("_BaseColor")) dark.SetColor("_BaseColor", dark.color);

                Part(PrimitiveType.Capsule, "Body", root, new Vector3(0f, 0f, 0f), new Vector3(0.55f, 0.42f, 0.55f), body);
                Part(PrimitiveType.Sphere, "Skirt", root, new Vector3(0f, -0.3f, 0f), new Vector3(0.58f, 0.2f, 0.58f), body);
                Part(PrimitiveType.Sphere, "EyeL", root, new Vector3(-0.1f, 0.12f, 0.24f), Vector3.one * 0.1f, dark);
                Part(PrimitiveType.Sphere, "EyeR", root, new Vector3(0.1f, 0.12f, 0.24f), Vector3.one * 0.1f, dark);
                Part(PrimitiveType.Sphere, "Mouth", root, new Vector3(0f, -0.04f, 0.26f), new Vector3(0.09f, 0.12f, 0.06f), dark);

                root.gameObject.SetActive(false);
                ghosts.Add(new Ghost { root = root, seed = UnityEngine.Random.value * 50f });
            }
        }

        static void Part(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            var renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        // ---------- every frame ----------

        void Update()
        {
            HandleSwitch();

            // leaving the room puts the lights back on
            if (!LightsOn && zone != null && !zone.PlayerInside) SetLights(true, quiet: true);

            float target = LightsOn ? 0f : 1f;
            if (!Mathf.Approximately(darkness, target))
            {
                darkness = Mathf.MoveTowards(darkness, target, Time.deltaTime * 2.5f);
                ApplyDarkness();
            }

            // the neon line shrinks away when the lights come back on
            if (LightsOn && trailScale > 0f)
            {
                trailScale = Mathf.MoveTowards(trailScale, 0f, Time.deltaTime * 3f);
                ApplyTrailScale();
                if (trailScale <= 0f) SetTrailVisible(false);
            }

            if (!LightsOn) UpdateRun();
        }

        void HandleSwitch()
        {
            LineRoomUI ui = LineRoomUI.Instance;
            if (switchCollider == null || zone == null || !zone.PlayerInside || LineRoomUI.Busy || !CanInteract())
            {
                ui.SetPrompt(this, "");
                return;
            }

            if (lookCamera == null) lookCamera = Camera.main;
            if (lookCamera == null) return;

            Ray ray = lookCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (!switchCollider.Raycast(ray, out RaycastHit hit, reach))
            {
                ui.SetPrompt(this, "");
                return;
            }

            ui.SetPrompt(this, LightsOn ? $"Press {switchKey}: lights OFF" : $"Press {switchKey}: lights ON");
            Keyboard keyboard = Keyboard.current;
            Gamepad pad = Gamepad.current;
            if ((keyboard != null && keyboard[switchKey].wasPressedThisFrame) || (pad != null && pad.buttonWest.wasPressedThisFrame))
            {
                SetLights(!LightsOn, quiet: false);
            }
        }

        bool CanInteract()
        {
            if (drawMode == null) drawMode = FindAnyObjectByType<SelectionDrawMode>();
            if (studio == null) studio = FindAnyObjectByType<SculptStudio>();
            if ((drawMode != null && drawMode.IsActive) || (studio != null && studio.IsActive)) return false;
            if (DialogueRunner.Instance != null && DialogueRunner.Instance.IsActive) return false;
            return FirstPersonController.InputCaptured;
        }

        // ---------- lights ----------

        public void SetLights(bool on, bool quiet)
        {
            if (on == LightsOn) return;
            LightsOn = on;
            SetLever(on);
            winTimer = -1f;

            LineRoomUI ui = LineRoomUI.Instance;
            if (!on)
            {
                SaveLighting();
                neon = neonColors[run % neonColors.Length];
                run++;
                SetNeonColor(neon);
                trailScale = 1f;
                ApplyTrailScale();
                SetTrailVisible(true);
                StartRun(PlayerFeet());
                if (beacon != null) beacon.gameObject.SetActive(true);
                SpawnGhosts();
                ui.Say("Lights out!\nDraw ONE line to the far light.\nDon't cross it - and watch for ghosts!", 4f);
            }
            else
            {
                if (beacon != null) beacon.gameObject.SetActive(false);
                foreach (Ghost ghost in ghosts) ghost.root.gameObject.SetActive(false);
                ui.SetStatus("");
                ui.SetBar(-1f, neon);
                if (!quiet && !Solved) ui.Say("The neon only shows in the dark...", 2.5f);
            }
        }

        void SetLever(bool on)
        {
            if (switchLever != null) switchLever.localRotation = Quaternion.Euler(on ? -25f : 25f, 0f, 0f);
        }

        void SaveLighting()
        {
            if (lightingSaved) return;
            lightingSaved = true;
            sun = RenderSettings.sun;
            if (sun == null)
            {
                foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                {
                    if (light.type == LightType.Directional)
                    {
                        sun = light;
                        break;
                    }
                }
            }
            sunIntensity = sun != null ? sun.intensity : 0f;
            skyAmbient = RenderSettings.ambientSkyColor;
            equatorAmbient = RenderSettings.ambientEquatorColor;
            groundAmbient = RenderSettings.ambientGroundColor;
            reflectionIntensity = RenderSettings.reflectionIntensity;
        }

        void ApplyDarkness()
        {
            if (!lightingSaved) return;
            Color night = new Color(0.01f, 0.01f, 0.025f);
            if (sun != null) sun.intensity = Mathf.Lerp(sunIntensity, 0f, darkness);
            RenderSettings.ambientSkyColor = Color.Lerp(skyAmbient, night, darkness);
            RenderSettings.ambientEquatorColor = Color.Lerp(equatorAmbient, night, darkness);
            RenderSettings.ambientGroundColor = Color.Lerp(groundAmbient, night, darkness);
            RenderSettings.reflectionIntensity = Mathf.Lerp(reflectionIntensity, 0f, darkness);
        }

        void RestoreLighting()
        {
            if (!lightingSaved) return;
            darkness = 0f;
            ApplyDarkness();
        }

        // ---------- the neon line ----------

        Vector3 PlayerFeet()
        {
            Vector3 p = player != null ? player.transform.position : Vector3.zero;
            p.y = 0.015f;
            return p;
        }

        void StartRun(Vector3 from)
        {
            points.Clear();
            arc.Clear();
            length = 0f;
            points.Add(from);
            arc.Add(0f);
            UpdateLine();
        }

        void UpdateRun()
        {
            if (player == null) return;
            LineRoomUI ui = LineRoomUI.Instance;
            float dt = Time.deltaTime;

            if (flashTimer > 0f)
            {
                flashTimer -= dt;
                if (flashTimer <= 0f) SetNeonColor(neon);
            }
            if (safeTimer > 0f) safeTimer -= dt;

            UpdateGhosts(dt);

            Vector3 feet = PlayerFeet();
            headLight.transform.position = feet + Vector3.up * 0.45f;

            if (winTimer >= 0f)
            {
                winTimer -= dt;
                if (winTimer < 0f) SetLights(true, quiet: true);
                return;
            }

            // lay down more line as the player walks
            Vector3 last = points[points.Count - 1];
            float step = Vector3.Distance(feet, last);
            if (step >= 0.1f && safeTimer <= 0f)
            {
                if (TouchesOwnLine(feet))
                {
                    Fail("Your line touched itself!");
                    return;
                }
                points.Add(feet);
                length += step;
                arc.Add(length);
                UpdateLine();
            }

            // ghosts
            if (safeTimer <= 0f)
            {
                foreach (Ghost ghost in ghosts)
                {
                    Vector3 g = ghost.root.position;
                    g.y = 0f;
                    Vector3 f = feet;
                    f.y = 0f;
                    if (Vector3.Distance(g, f) < 0.62f)
                    {
                        Fail("A ghost got you!");
                        return;
                    }
                }
            }

            bool full = length >= targetLength;
            ui.SetStatus(full ? "Now reach the far light!" : $"Lights out:  draw a glowing line  ({length:0.0} / {targetLength:0} m)");
            ui.SetBar(Mathf.Clamp01(length / targetLength), neon);

            if (full && beacon != null)
            {
                Vector3 b = beacon.position;
                b.y = 0f;
                Vector3 f = feet;
                f.y = 0f;
                if (Vector3.Distance(b, f) <= beaconReachDistance) Win();
            }
        }

        // The new position may not come within a body-width of any older part of the line.
        // The most recent bit (where the player has just been walking) is ignored.
        bool TouchesOwnLine(Vector3 head)
        {
            const float Clearance = 0.2f;
            const float IgnoreRecent = 0.8f;
            for (int i = 0; i < points.Count - 1; i++)
            {
                if (arc[i + 1] > length - IgnoreRecent) break;
                if (DistanceToSegment(head, points[i], points[i + 1]) < Clearance) return true;
            }
            return false;
        }

        static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            ab.y = 0f;
            Vector3 ap = p - a;
            ap.y = 0f;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(ap, ab) / ab.sqrMagnitude) : 0f;
            Vector3 closest = a + ab * t;
            closest.y = p.y;
            return Vector3.Distance(p, closest);
        }

        void Fail(string reason)
        {
            LineRoomUI.Instance.Say(reason + "\nTry again!", 1.8f);
            SetNeonColor(new Color(1f, 0.15f, 0.15f));
            flashTimer = 0.5f;
            safeTimer = 1.2f;
            StartRun(PlayerFeet());
        }

        void Win()
        {
            Solved = true;
            winTimer = 2.5f;
            LineRoomUI.Instance.Say("You drew it!\nSwitch on the lights to see the neon vanish...", 3f);
            SolvedEvent?.Invoke();
        }

        void UpdateLine()
        {
            Vector3[] array = points.ToArray();
            core.positionCount = array.Length;
            core.SetPositions(array);
            if (glow != null)
            {
                glow.positionCount = array.Length;
                glow.SetPositions(array);
            }
        }

        void SetNeonColor(Color color)
        {
            if (coreInstance != null)
            {
                Color bright = Color.Lerp(color, Color.white, 0.35f);
                coreInstance.color = bright;
                if (coreInstance.HasProperty("_BaseColor")) coreInstance.SetColor("_BaseColor", bright);
            }
            if (glowInstance != null)
            {
                var soft = new Color(color.r, color.g, color.b, 0.55f);
                glowInstance.color = soft;
                if (glowInstance.HasProperty("_BaseColor")) glowInstance.SetColor("_BaseColor", soft);
            }
            if (headLight != null) headLight.color = color;
        }

        void SetTrailVisible(bool visible)
        {
            if (core != null) core.enabled = visible;
            if (glow != null) glow.enabled = visible;
            if (headLight != null) headLight.enabled = visible;
        }

        void ApplyTrailScale()
        {
            if (core != null) core.widthMultiplier = lineWidth * trailScale;
            if (glow != null) glow.widthMultiplier = glowWidth * trailScale;
            if (headLight != null) headLight.intensity = 2f * trailScale;
        }

        // ---------- ghosts ----------

        void SpawnGhosts()
        {
            Vector3 feet = PlayerFeet();
            foreach (Ghost ghost in ghosts)
            {
                Vector3 spot = feet;
                for (int tries = 0; tries < 12; tries++)
                {
                    spot = RandomSpot();
                    if (Vector3.Distance(spot, feet) > 3f) break;
                }
                ghost.root.position = new Vector3(spot.x, 0.85f, spot.z);
                ghost.target = RandomSpot();
                ghost.root.gameObject.SetActive(true);
            }
        }

        Vector3 RandomSpot()
        {
            return new Vector3(UnityEngine.Random.Range(boundsMin.x, boundsMax.x), 0f, UnityEngine.Random.Range(boundsMin.z, boundsMax.z));
        }

        void UpdateGhosts(float dt)
        {
            Vector3 feet = PlayerFeet();
            for (int i = 0; i < ghosts.Count; i++)
            {
                Ghost ghost = ghosts[i];
                Vector3 position = ghost.root.position;
                Vector3 flatTarget = ghost.target;
                flatTarget.y = position.y;

                Vector3 toward = flatTarget - position;
                if (toward.magnitude < 0.35f)
                {
                    // sometimes drift toward where the player is now
                    ghost.target = UnityEngine.Random.value < 0.4f ? new Vector3(
                        Mathf.Clamp(feet.x, boundsMin.x, boundsMax.x), 0f, Mathf.Clamp(feet.z, boundsMin.z, boundsMax.z)) : RandomSpot();
                    continue;
                }

                Vector3 move = toward.normalized * ((ghostSpeed + 0.1f * i) * dt);
                position += move;
                position.y = 0.85f + Mathf.Sin(Time.time * 2.2f + ghost.seed) * 0.1f;
                ghost.root.position = position;
                ghost.root.rotation = Quaternion.Slerp(ghost.root.rotation, Quaternion.LookRotation(new Vector3(toward.x, 0f, toward.z)), 6f * dt);
            }
        }
    }
}
