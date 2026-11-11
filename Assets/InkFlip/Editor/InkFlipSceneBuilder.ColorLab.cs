using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace InkFlip.EditorTools
{
    // The contents of the Colour Room (the south-east room): a science bench with five paint beakers and a mixing flask,
    // a shelf of four sample beakers to match, and a colour wheel on the wall. The scripts are in Scripts/ColorLab.
    public static partial class InkFlipSceneBuilder
    {
        // the same five paints as the original colour mixer (red, yellow, blue, white, black)
        static readonly string[] LabPaintNames = { "Red", "Yellow", "Blue", "White", "Black" };
        static readonly Color[] LabPaintColors =
        {
            new Color(0.85f, 0.12f, 0.15f), new Color(0.98f, 0.85f, 0.10f), new Color(0.10f, 0.30f, 0.80f),
            new Color(0.97f, 0.97f, 0.97f), new Color(0.08f, 0.08f, 0.08f),
        };

        // what each sample is made from: parts of red, yellow, blue, white, black (so every sample can be matched)
        static readonly (string name, int[] parts)[] LabSamples =
        {
            ("Sunny Orange", new[] { 1, 1, 0, 0, 0 }),
            ("Leaf Green", new[] { 0, 2, 1, 0, 0 }),
            ("Bubblegum Pink", new[] { 1, 0, 0, 2, 0 }),
            ("Deep Teal", new[] { 0, 1, 3, 1, 0 }),
        };

        class LabMaterials
        {
            public Material glass, rim, mark, highlight, cork, light, drop;
        }

        static LabMaterials CreateLabMaterials(Shader lit, Shader unlit)
        {
            var m = new LabMaterials
            {
                glass = CreateMaterial("LabGlass", lit, "#CFE8F52E", 0.95f),
                rim = CreateMaterial("LabRim", lit, "#EAF6FF", 0.9f),
                mark = CreateMaterial("LabMark", unlit, "#7C93AD", 0f),
                highlight = CreateMaterial("LabHighlight", unlit, "#FFF2A0", 0f),
                cork = CreateMaterial("LabCork", lit, "#A8794A", 0.1f),
                light = CreateMaterial("LabSampleLight", unlit, "#404050", 0f),
                drop = CreateMaterial("LabDrop", unlit, "#FFFFFF", 0f),
            };
            MakeTransparent(m.glass);
            return m;
        }

        static void MakeTransparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f); // alpha
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetShaderPassEnabled("ShadowCaster", false);
            EditorUtility.SetDirty(m);
        }

        static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        // a built-in cylinder (2 units tall, 1 across) sized in metres, without its collider
        static GameObject LabCylinder(Transform parent, string name, Vector3 localPosition, float diameter, float height, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        static GameObject LabCube(Transform parent, string name, Vector3 localPosition, Vector3 size, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = size;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        // A straight-sided lab beaker standing with its bottom centre at `basePosition`.
        static LabBeaker MakeBeaker(Transform parent, string name, Vector3 basePosition, LabBeaker.Kind kind, string label, Color color,
                                    float fill, Material liquidMaterial, LabMaterials m, bool corked)
        {
            const float Radius = 0.09f;
            const float Height = 0.3f;

            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = basePosition;

            LabCylinder(root.transform, "Glass", new Vector3(0f, Height * 0.5f, 0f), Radius * 2f, Height, m.glass);
            GameObject liquid = LabCylinder(root.transform, "Liquid", new Vector3(0f, 0.1f, 0f), Radius * 2f * 0.88f, 0.1f, liquidMaterial);
            LabCylinder(root.transform, "Rim", new Vector3(0f, Height, 0f), Radius * 2f * 1.1f, 0.012f, m.rim);
            foreach (float level in new[] { 0.25f, 0.5f, 0.75f })
            {
                LabCube(root.transform, "Mark", new Vector3(0f, Height * level, Radius), new Vector3(0.05f, 0.004f, 0.004f), m.mark);
            }
            GameObject ring = LabCylinder(root.transform, "Highlight", new Vector3(0f, 0.003f, 0f), Radius * 2f * 1.9f, 0.004f, m.highlight);
            if (corked) LabCylinder(root.transform, "Cork", new Vector3(0f, Height + 0.02f, 0f), Radius * 2f * 0.7f, 0.05f, m.cork);

            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, Height * 0.5f, 0f);
            box.size = new Vector3(Radius * 2.4f, Height, Radius * 2.4f);

            var beaker = root.AddComponent<LabBeaker>();
            beaker.kind = kind;
            beaker.label = label;
            beaker.color = color;
            beaker.fill = fill;
            beaker.liquid = liquid.transform;
            beaker.liquidRenderer = liquid.GetComponent<MeshRenderer>();
            beaker.fullHeight = Height * 0.86f;
            beaker.baseY = 0.012f;
            beaker.highlight = ring.GetComponent<MeshRenderer>();
            beaker.rimHeight = Height + 0.06f;
            return beaker;
        }

        // The mixing flask: a wide base, a shoulder and a thin neck (three stacked cylinders - low poly on purpose).
        static LabBeaker MakeFlask(Transform parent, Vector3 basePosition, Material liquidMaterial, LabMaterials m)
        {
            var root = new GameObject("MixingFlask");
            root.transform.SetParent(parent, false);
            root.transform.position = basePosition;

            LabCylinder(root.transform, "GlassBase", new Vector3(0f, 0.085f, 0f), 0.30f, 0.17f, m.glass);
            LabCylinder(root.transform, "GlassShoulder", new Vector3(0f, 0.21f, 0f), 0.20f, 0.08f, m.glass);
            LabCylinder(root.transform, "GlassNeck", new Vector3(0f, 0.325f, 0f), 0.09f, 0.15f, m.glass);
            LabCylinder(root.transform, "Rim", new Vector3(0f, 0.40f, 0f), 0.115f, 0.012f, m.rim);
            GameObject liquid = LabCylinder(root.transform, "Liquid", new Vector3(0f, 0.1f, 0f), 0.26f, 0.1f, liquidMaterial);
            GameObject ring = LabCylinder(root.transform, "Highlight", new Vector3(0f, 0.003f, 0f), 0.5f, 0.004f, m.highlight);

            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.2f, 0f);
            box.size = new Vector3(0.32f, 0.4f, 0.32f);

            var beaker = root.AddComponent<LabBeaker>();
            beaker.kind = LabBeaker.Kind.Flask;
            beaker.label = "";
            beaker.color = Color.white;
            beaker.fill = 0f;
            beaker.liquid = liquid.transform;
            beaker.liquidRenderer = liquid.GetComponent<MeshRenderer>();
            beaker.fullHeight = 0.15f;
            beaker.baseY = 0.01f;
            beaker.highlight = ring.GetComponent<MeshRenderer>();
            beaker.rimHeight = 0.44f;
            return beaker;
        }

        // hue around the circle, paler towards the middle; transparent outside (used with a cut-out material)
        static Texture2D LoadOrCreateColorWheelTexture()
        {
            string path = TextureFolder + "/ColorWheel.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            const int Size = 256;
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    Vector2 p = new Vector2((x + 0.5f) / Size - 0.5f, (y + 0.5f) / Size - 0.5f);
                    float r = p.magnitude * 2f;
                    Color c = new Color(0f, 0f, 0f, 0f);
                    if (r <= 1f)
                    {
                        float hue = (Mathf.Atan2(p.y, p.x) / (2f * Mathf.PI) + 1f) % 1f;
                        c = Color.HSVToRGB(hue, Mathf.Clamp01(r * 1.15f), 1f);
                        c.a = 1f;
                    }
                    texture.SetPixel(x, y, c);
                }
            }
            texture.Apply();
            return SaveTexture(path, texture, TextureWrapMode.Clamp, true);
        }

        static void BuildColorLabContents(Transform group, RoomZone zone, Material floor, Material wall, Material furniture)
        {
            EnsureFolder(TextureFolder);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            Shader paintable = Shader.Find("InkFlip/PaintableLit");

            // ---- materials ----
            LabMaterials m = CreateLabMaterials(lit, unlit);
            Material benchBody = CreateMaterial("LabBenchBody", paintable, "#2C3E66", 0.3f);
            Material benchTop = CreateMaterial("LabBenchTop", paintable, "#DDE3EA", 0.55f);
            Material navy = CreateMaterial("LabNavy", lit, "#1D2B4F", 0.35f);
            Material flaskLiquid = CreateMaterial("Liquid_Flask", unlit, "#FFFFFF", 0f);

            // ---- the bench: paints on a raised rack at the back, the flask in front ----
            const float TopY = 0.92f;
            Vector3 benchCentre = new Vector3(3.1f, 0f, -9.35f);
            Box("LabBench_Body", group, new Vector3(3.8f, 0.86f, 1.0f), new Vector3(benchCentre.x, 0.43f, benchCentre.z), benchBody);
            Box("LabBench_Top", group, new Vector3(3.95f, 0.06f, 1.12f), new Vector3(benchCentre.x, 0.89f, benchCentre.z), benchTop);
            Box("PaintRack", group, new Vector3(3.6f, 0.22f, 0.4f), new Vector3(benchCentre.x, TopY + 0.11f, -9.7f), benchTop);
            float rackTop = TopY + 0.22f;

            var paints = new LabBeaker[LabPaintNames.Length];
            for (int i = 0; i < paints.Length; i++)
            {
                Material liquidMaterial = CreateMaterial("Liquid_" + LabPaintNames[i], unlit, Hex(LabPaintColors[i]), 0f);
                float x = benchCentre.x + (i - (paints.Length - 1) * 0.5f) * 0.7f;
                paints[i] = MakeBeaker(group, "Paint_" + LabPaintNames[i], new Vector3(x, rackTop, -9.7f), LabBeaker.Kind.Pigment,
                                       LabPaintNames[i], LabPaintColors[i], 0.9f, liquidMaterial, m, corked: false);
            }

            LabBeaker flask = MakeFlask(group, new Vector3(benchCentre.x, TopY, -9.05f), flaskLiquid, m);

            // ---- the sample shelf on the back wall ----
            Box("SampleShelf", group, new Vector3(5.2f, 0.06f, 0.45f), new Vector3(3.1f, 1.3f, -11.85f), furniture);
            Box("ShelfBracket_W", group, new Vector3(0.08f, 0.3f, 0.4f), new Vector3(0.75f, 1.12f, -11.87f), furniture);
            Box("ShelfBracket_E", group, new Vector3(0.08f, 0.3f, 0.4f), new Vector3(5.45f, 1.12f, -11.87f), furniture);

            var sampleBeakers = new LabBeaker[LabSamples.Length];
            var recipes = new ColorLabGame.Sample[LabSamples.Length];
            var paintColors = new List<Color>(LabPaintColors);
            for (int i = 0; i < LabSamples.Length; i++)
            {
                Color sampleColor = ColorLabGame.MixColor(paintColors, LabSamples[i].parts);
                Material liquidMaterial = CreateMaterial("Liquid_Sample" + i, unlit, Hex(sampleColor), 0f);
                float x = 3.1f + (i - (LabSamples.Length - 1) * 0.5f) * 1.2f;
                sampleBeakers[i] = MakeBeaker(group, "Sample_" + LabSamples[i].name.Replace(' ', '_'), new Vector3(x, 1.33f, -11.85f),
                                              LabBeaker.Kind.Sample, LabSamples[i].name, sampleColor, 0.75f, liquidMaterial, m, corked: true);

                // a little light on the wall that turns green once the sample is matched
                GameObject light = LabCube(group, "SampleLight_" + i, new Vector3(x, 2.05f, -12.07f), new Vector3(0.16f, 0.16f, 0.04f), m.light);
                sampleBeakers[i].statusLight = light.GetComponent<MeshRenderer>();

                recipes[i] = new ColorLabGame.Sample { name = LabSamples[i].name, parts = (int[])LabSamples[i].parts.Clone() };
            }

            // ---- a colour wheel on the east wall ----
            Material posterMaterial = CreateMaterial("ColorWheelPoster", lit, "#FFFFFF", 0.1f);
            posterMaterial.SetFloat("_AlphaClip", 1f);
            posterMaterial.SetFloat("_Cutoff", 0.5f);
            posterMaterial.SetFloat("_Cull", 0f);
            posterMaterial.EnableKeyword("_ALPHATEST_ON");
            posterMaterial.SetOverrideTag("RenderType", "TransparentCutout");
            posterMaterial.renderQueue = (int)RenderQueue.AlphaTest;
            Texture2D wheel = LoadOrCreateColorWheelTexture();
            posterMaterial.SetTexture("_BaseMap", wheel);
            posterMaterial.mainTexture = wheel;
            EditorUtility.SetDirty(posterMaterial);
            Box("WheelFrame", group, new Vector3(0.05f, 1.8f, 1.8f), new Vector3(5.975f, 1.8f, -9.6f), navy);
            WallBanner(group, "ColorWheel", new Vector3(5.95f, 1.8f, -9.6f), Vector3.left, new Vector2(1.5f, 1.5f), posterMaterial);

            // ---- Pippa explains the lab ----
            PlaceTalker(group, "Pippa", "Assets/Prototype/Messy Image/buddy_03.png", new Vector3(5.3f, 0f, -7.5f), 1.1f);

            // ---- the game and the goal ----
            var gameObject = new GameObject("ColorLab");
            gameObject.transform.SetParent(group, false);
            var game = gameObject.AddComponent<ColorLabGame>();
            game.zone = zone;
            game.paintBeakers = paints;
            game.samples = recipes;
            game.sampleBeakers = sampleBeakers;
            game.flask = flask;
            game.dropMaterial = m.drop;

            var objective = zone.gameObject.AddComponent<ColorLabObjective>();
            objective.title = "Match the colours";
            objective.description = "Mix paints in the flask until you match every sample on the shelf.";
            objective.lab = game;
            objective.zone = zone;
            objective.testKey = UnityEngine.InputSystem.Key.K;
            zone.objective = objective;
        }
    }
}
