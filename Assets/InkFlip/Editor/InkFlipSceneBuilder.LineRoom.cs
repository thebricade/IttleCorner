using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace InkFlip.EditorTools
{
    // The contents of the Lines room (the south-west room): a race table with a character, five colour pictures
    // on the walls, a light switch with a far-off light, and the scripts that run the three games.
    public static partial class InkFlipSceneBuilder
    {
        const string TextureFolder = Root + "/Generated/Textures";

        static void BuildLineRoomContents(Transform group, RoomZone zone, Material floor, Material wall, Material furniture)
        {
            EnsureFolder(TextureFolder);
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Shader paintable = Shader.Find("InkFlip/PaintableLit");

            // ---- materials ----
            Material racerLine = CreateMaterial("RacerLine", unlit, "#FFFFFF", 0f);
            Material eyeWhite = CreateMaterial("RacerEyeWhite", unlit, "#FFFFFF", 0f);
            Material eyePupil = CreateMaterial("RacerEyePupil", unlit, "#0D0D14", 0f);
            Material neonCore = CreateMaterial("NeonCore", unlit, "#66FFD9", 0f);
            Material neonGlow = CreateMaterial("NeonGlow", unlit, "#FFFFFF", 0f);
            neonGlow.SetFloat("_Surface", 1f);
            neonGlow.SetFloat("_Blend", 2f); // additive
            neonGlow.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            neonGlow.SetFloat("_DstBlend", (float)BlendMode.One);
            neonGlow.SetFloat("_ZWrite", 0f);
            neonGlow.SetOverrideTag("RenderType", "Transparent");
            neonGlow.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            neonGlow.renderQueue = (int)RenderQueue.Transparent;
            neonGlow.SetShaderPassEnabled("ShadowCaster", false);
            Texture2D glowTexture = LoadOrCreateGlowTexture();
            if (glowTexture != null)
            {
                neonGlow.SetTexture("_BaseMap", glowTexture);
                neonGlow.mainTexture = glowTexture;
            }

            Material ghostBody = CreateMaterial("GhostBody", unlit, "#C7E6FF", 0f);
            Material beaconMaterial = CreateMaterial("FarLight", unlit, "#FFE6A0", 0f);
            Material stripeMaterial = CreateMaterial("TrackStripe", paintable, "#2A2630", 0.2f);
            Material leverMaterial = CreateMaterial("SwitchLever", lit, "#E04848", 0.3f);

            // ---- ceiling: hides the sky, so the room can go properly dark (it casts no shadow, the sun still reaches in) ----
            Box("Ceiling_DrawingRoom", group, new Vector3(6.4f, 0.2f, 6.4f), new Vector3(-3f, 3.3f, -9.1f), wall);
            Transform ceiling = group.Find("Ceiling_DrawingRoom");
            ceiling.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            // ---- race table along the east wall ----
            Vector3 tableCentre = new Vector3(-0.65f, 0f, -9.1f);
            Box("RaceTable_Top", group, new Vector3(1.2f, 0.08f, 4.4f), new Vector3(tableCentre.x, 0.76f, tableCentre.z), furniture);
            foreach (Vector2 leg in new[] { new Vector2(-0.52f, -2.05f), new Vector2(0.52f, -2.05f), new Vector2(-0.52f, 2.05f), new Vector2(0.52f, 2.05f) })
            {
                Box("RaceTable_Leg", group, new Vector3(0.1f, 0.72f, 0.1f), new Vector3(tableCentre.x + leg.x, 0.36f, tableCentre.z + leg.y), furniture);
            }
            Box("RaceStartLine", group, new Vector3(1.2f, 0.012f, 0.06f), new Vector3(tableCentre.x, 0.806f, -7.25f), stripeMaterial);
            Box("RaceFinishLine", group, new Vector3(1.2f, 0.012f, 0.06f), new Vector3(tableCentre.x, 0.806f, -11.05f), stripeMaterial);

            var trackStart = new GameObject("RaceTrackStart").transform;
            trackStart.SetParent(group, false);
            trackStart.position = new Vector3(tableCentre.x, 0.8f, -7.25f);
            trackStart.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up); // the racers run south

            // ---- the character who asks for the race ----
            PlaceTalker(group, "Squiggle", "Assets/Prototype/Messy Image/buddy_02.png", new Vector3(-1.9f, 0f, -7.6f), 1.1f);

            // ---- the power panel: a line-tracing practice on the west wall (trace straight lines with the brush to power the room) ----
            Material flowFrame = CreateMaterial("FlowFrame", lit, "#2B2F3A", 0.3f);
            Material flowScreen = CreateMaterial("FlowScreen", unlit, "#FFFFFF", 0f);
            Material lampMaterial = CreateMaterial("PowerLamp", unlit, "#FFFFFF", 0f);
            LineTracePuzzle flow = BuildFlowBoard(group, new Vector3(-6f, 1.75f, -9.1f), Vector3.right, flowFrame, flowScreen, lampMaterial);

            // ceiling lamps over the race table that come on with the power
            var lamps = new System.Collections.Generic.List<Renderer>(flow.powerLamps);
            foreach (float lampZ in new[] { -10.3f, -7.9f })
            {
                Box("PowerLamp_Ceiling", group, new Vector3(0.9f, 0.06f, 0.3f), new Vector3(tableCentre.x, 3.17f, lampZ), lampMaterial);
                Transform lampTransform = group.GetChild(group.childCount - 1);
                Object.DestroyImmediate(lampTransform.GetComponent<Collider>());
                lampTransform.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                lamps.Add(lampTransform.GetComponent<MeshRenderer>());
            }
            flow.powerLamps = lamps.ToArray();
            flow.Rebuild();

            // ---- the light switch (on the wall beside the door) and the far light ----
            GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = "LightSwitch";
            Object.DestroyImmediate(plate.GetComponent<Collider>());
            plate.transform.SetParent(group, false);
            plate.transform.position = new Vector3(-5f, 1.3f, -6.22f);
            plate.transform.localScale = new Vector3(0.16f, 0.26f, 0.04f);
            plate.GetComponent<MeshRenderer>().sharedMaterial = furniture;

            // the lever hangs off the group (not the plate), so the plate's squashed scale doesn't shear it when it flips
            var lever = new GameObject("Lever").transform;
            lever.SetParent(group, false);
            lever.position = plate.transform.position + new Vector3(0f, 0f, -0.035f);
            GameObject leverBar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leverBar.name = "Bar";
            Object.DestroyImmediate(leverBar.GetComponent<Collider>());
            leverBar.transform.SetParent(lever, false);
            leverBar.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            leverBar.transform.localScale = new Vector3(0.055f, 0.1f, 0.025f);
            leverBar.GetComponent<MeshRenderer>().sharedMaterial = leverMaterial;

            var hit = new GameObject("SwitchHit");
            hit.transform.SetParent(group, false);
            hit.transform.position = new Vector3(-5f, 1.3f, -6.3f);
            var hitBox = hit.AddComponent<BoxCollider>();
            hitBox.size = new Vector3(0.5f, 0.6f, 0.3f);

            var beacon = new GameObject("FarLight").transform;
            beacon.SetParent(group, false);
            beacon.position = new Vector3(-3f, 2.3f, -11.7f);
            GameObject bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bulb.name = "Bulb";
            Object.DestroyImmediate(bulb.GetComponent<Collider>());
            bulb.transform.SetParent(beacon, false);
            bulb.transform.localScale = Vector3.one * 0.35f;
            bulb.GetComponent<MeshRenderer>().sharedMaterial = beaconMaterial;
            bulb.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var beaconLight = beacon.gameObject.AddComponent<Light>();
            beaconLight.type = LightType.Point;
            beaconLight.color = new Color(1f, 0.86f, 0.55f);
            beaconLight.range = 9f;
            beaconLight.intensity = 3.5f;
            beaconLight.shadows = LightShadows.None;

            // ---- the three games ----
            var games = new GameObject("LineGames").transform;
            games.SetParent(group, false);

            var race = games.gameObject.AddComponent<LineRaceGame>();
            race.trackStart = trackStart;
            race.lineMaterial = racerLine;
            race.eyeWhiteMaterial = eyeWhite;
            race.eyePupilMaterial = eyePupil;
            race.power = flow;

            var neon = games.gameObject.AddComponent<NeonLightsGame>();
            neon.zone = zone;
            neon.switchCollider = hitBox;
            neon.switchLever = lever;
            neon.beacon = beacon;
            neon.coreMaterial = neonCore;
            neon.glowMaterial = neonGlow;
            neon.ghostMaterial = ghostBody;

            var objective = zone.gameObject.AddComponent<LineRoomObjective>();
            objective.title = "Line Room";
            objective.description = "Power the room on the wall panel, then race your nanobots.";
            objective.race = race;
            objective.power = flow;
            // the neon lights game isn't developed yet: left out of the objective and switched off
            neon.enabled = false;
            objective.zone = zone;
            zone.objective = objective;
        }

        // a framed quad on a wall. 'intoRoom' is the direction pointing away from the wall, into the room.
        static ColoringPanel MakePicturePanel(Transform parent, string name, Vector3 wallPoint, Vector3 intoRoom, int pictureIndex, Material frameMaterial, Material pictureMaterial)
        {
            const float Size = 1.3f;
            bool alongX = Mathf.Abs(intoRoom.x) > 0.5f;

            // frame
            Vector3 frameSize = alongX ? new Vector3(0.03f, Size + 0.15f, Size + 0.15f) : new Vector3(Size + 0.15f, Size + 0.15f, 0.03f);
            Box(name + "_Frame", parent, frameSize, wallPoint + intoRoom * 0.015f, frameMaterial);

            // the picture; a Unity quad shows its front to anything looking along its +Z, so it looks into the wall
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Object.DestroyImmediate(quad.GetComponent<MeshCollider>());
            quad.transform.SetParent(parent, false);
            quad.transform.position = wallPoint + intoRoom * 0.035f;
            quad.transform.rotation = Quaternion.LookRotation(-intoRoom, Vector3.up);
            quad.transform.localScale = new Vector3(Size, Size, 1f);
            var box = quad.AddComponent<BoxCollider>();
            box.size = new Vector3(1f, 1f, 0.05f);

            var panel = quad.AddComponent<ColoringPanel>();
            panel.pictureIndex = pictureIndex;
            panel.panelRenderer = quad.GetComponent<MeshRenderer>();
            panel.panelRenderer.sharedMaterial = pictureMaterial;
            return panel;
        }

        // The wall picture as a real texture + material asset, so it shows in the editor and never depends on runtime set-up.
        static Material BakePictureMaterial(int index, Shader lit)
        {
            const int Size = 512;
            ColoringPictures.Picture picture = ColoringPictures.Generate(index, Size);
            string pictureName = ColoringPictures.Names[index % ColoringPictures.Count];
            string path = $"{TextureFolder}/Picture_{pictureName}.png";

            var pixels = new Color32[Size * Size];
            for (int i = 0; i < pixels.Length; i++)
            {
                byte id = picture.region[i];
                pixels[i] = id == 0 ? new Color32(250, 247, 235, 255) : picture.palette[id];
            }
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = true;
                importer.anisoLevel = 4;
                importer.SaveAndReimport();
            }

            var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Material material = CreateMaterial("Picture_" + pictureName, lit, "#FFFFFF", 0.05f);
            material.SetTexture("_BaseMap", loaded);
            material.mainTexture = loaded;
            EditorUtility.SetDirty(material);
            return material;
        }

        // a soft stripe: opaque in the middle, fading to nothing at both edges (the glow around the neon line)
        static Texture2D LoadOrCreateGlowTexture()
        {
            string path = TextureFolder + "/NeonGlow.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            const int Width = 8, Height = 64;
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            for (int y = 0; y < Height; y++)
            {
                float across = Mathf.Abs((y + 0.5f) / Height * 2f - 1f);
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - across), 2f);
                for (int x = 0; x < Width; x++) texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
