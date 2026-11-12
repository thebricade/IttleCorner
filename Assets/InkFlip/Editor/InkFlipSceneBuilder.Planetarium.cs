using System.IO;
using UnityEditor;
using UnityEngine;

namespace InkFlip.EditorTools
{
    // The final area behind the elevator: a planetarium. The walls and ceiling are a starfield, a projector stands in the
    // middle, and nine blank planets float around it. The goal is to colour them all, each one different.
    // Scripts: PlanetariumGame, PlanetariumObjective.
    public static partial class InkFlipSceneBuilder
    {
        // x from 12.2 to 26.2, z from -6.1 to 6.1 (the same width as the entry hall, to the east of the elevator)
        const float PlanetariumCentreX = 19.2f;
        const float PlanetariumHeight = 6f;

        static void BuildPlanetarium(Transform room)
        {
            Transform group = Group("Planetarium", room);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            Shader paintable = Shader.Find("InkFlip/PaintableLit");
            EnsureFolder(TextureFolder);

            Texture2D stars = LoadOrCreateStarTexture();
            Material sky = CreateMaterial("PlanetariumSky", unlit, "#FFFFFF", 0f);
            sky.SetTexture("_BaseMap", stars);
            sky.mainTexture = stars;
            sky.SetTextureScale("_BaseMap", new Vector2(0.22f, 0.22f));
            sky.mainTextureScale = new Vector2(0.22f, 0.22f);
            Material floor = CreateMaterial("PlanetariumFloor", lit, "#1B2038", 0.55f);
            Material blank = CreateMaterial("BlankPlanet", paintable, "#F2F2F6", 0.3f);
            Material trim = CreateMaterial("PlanetariumTrim", paintable, "#3A4570", 0.4f);
            Material beaconMaterial = CreateMaterial("PlanetBeacon", unlit, "#FFFFFF", 0f);
            Material starDot = CreateMaterial("StarDot", unlit, "#FFFFFF", 0f);

            float length = 14f, depth = 12.2f;
            Box("Floor_Planetarium", group, new Vector3(length, 0.2f, depth), new Vector3(PlanetariumCentreX, -0.1f, 0f), floor);
            Box("Wall_Planetarium_N", group, new Vector3(length + 0.2f, PlanetariumHeight, 0.2f), new Vector3(PlanetariumCentreX, PlanetariumHeight * 0.5f, 6.1f), sky);
            Box("Wall_Planetarium_S", group, new Vector3(length + 0.2f, PlanetariumHeight, 0.2f), new Vector3(PlanetariumCentreX, PlanetariumHeight * 0.5f, -6.1f), sky);
            Box("Wall_Planetarium_E", group, new Vector3(0.2f, PlanetariumHeight, depth + 0.2f), new Vector3(PlanetariumCentreX + length * 0.5f + 0.1f, PlanetariumHeight * 0.5f, 0f), sky);
            Box("Ceiling_Planetarium", group, new Vector3(length + 0.4f, 0.2f, depth + 0.4f), new Vector3(PlanetariumCentreX, PlanetariumHeight + 0.1f, 0f), sky);
            // a low rim round the floor, like a stage
            Box("Rim_N", group, new Vector3(length, 0.12f, 0.3f), new Vector3(PlanetariumCentreX, 0.06f, 5.85f), trim);
            Box("Rim_S", group, new Vector3(length, 0.12f, 0.3f), new Vector3(PlanetariumCentreX, 0.06f, -5.85f), trim);
            Box("Rim_E", group, new Vector3(0.3f, 0.12f, depth - 0.6f), new Vector3(PlanetariumCentreX + length * 0.5f - 0.15f, 0.06f, 0f), trim);

            // the projector in the middle
            Box("Projector_Base", group, new Vector3(1.4f, 0.25f, 1.4f), new Vector3(PlanetariumCentreX, 0.125f, 0f), trim);
            Primitive("Projector_Column", group, "Cylinder.fbx", new Vector3(0.35f, 0.9f, 0.35f), new Vector3(PlanetariumCentreX, 0.7f, 0f), trim);
            Primitive("Projector_Head", group, "Sphere.fbx", new Vector3(0.8f, 0.8f, 0.8f), new Vector3(PlanetariumCentreX, 1.45f, 0f), trim);

            // tiny stars floating in the air, so the room feels full
            var random = new System.Random(7);
            for (int i = 0; i < 40; i++)
            {
                Vector3 at = new Vector3(PlanetariumCentreX + (float)(random.NextDouble() * 12.5 - 6.25), 0.6f + (float)random.NextDouble() * 4.8f, (float)(random.NextDouble() * 10.6 - 5.3));
                float size = 0.05f + (float)random.NextDouble() * 0.07f;
                GameObject star = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                star.name = "Star";
                Object.DestroyImmediate(star.GetComponent<Collider>());
                star.transform.SetParent(group, false);
                star.transform.position = at;
                star.transform.localScale = Vector3.one * size;
                var starRenderer = star.GetComponent<MeshRenderer>();
                starRenderer.sharedMaterial = starDot;
                starRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // ---- the planets: all blank, all waiting for colour (x and z are from the middle of the room) ----
            var layout = new (float x, float y, float z, float size)[]
            {
                (-4.2f, 1.6f, -3.0f, 1.4f), (-3.0f, 2.7f, 2.8f, 1.0f), (-0.6f, 1.5f, -4.3f, 0.9f),
                (0.5f, 2.3f, 4.0f, 1.7f), (2.6f, 1.6f, -2.6f, 1.2f), (3.8f, 3.0f, 1.2f, 0.8f),
                (5.0f, 1.6f, -4.0f, 1.5f), (-1.8f, 2.9f, -0.3f, 1.1f), (5.4f, 1.9f, 3.8f, 1.0f),
            };
            var planets = new Paintable[layout.Length];
            var beacons = new Renderer[layout.Length];
            for (int i = 0; i < layout.Length; i++)
            {
                var p = layout[i];
                Vector3 at = new Vector3(PlanetariumCentreX + p.x, p.y, p.z);
                Primitive("Planet_" + (i + 1), group, "Sphere.fbx", Vector3.one * p.size, at, blank);
                planets[i] = group.Find("Planet_" + (i + 1)).GetComponent<Paintable>();
                // with the pencil, turn the planet to colour its other sides
                planets[i].gameObject.AddComponent<DrawTurntable>().maxTilt = 90f;

                GameObject beacon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                beacon.name = "Beacon_" + (i + 1);
                Object.DestroyImmediate(beacon.GetComponent<Collider>());
                beacon.transform.SetParent(group, false);
                beacon.transform.position = at + Vector3.up * (p.size * 0.5f + 0.3f);
                beacon.transform.localScale = Vector3.one * 0.16f;
                beacons[i] = beacon.GetComponent<MeshRenderer>();
                beacons[i].sharedMaterial = beaconMaterial;
                beacons[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            RoomZone zone = MakeZone(room, "final_area", "Planetarium", "Give every planet its own colour",
                                     new Color(0.55f, 0.5f, 1f), new Vector3(PlanetariumCentreX, 0f, 0f), Ability.All, countsForFinal: false);
            zone.size = new Vector3(14f, 7f, 12f);
            zone.center = new Vector3(0f, 3.5f, 0f);

            var gameObject = new GameObject("PlanetariumGame");
            gameObject.transform.SetParent(group, false);
            var game = gameObject.AddComponent<PlanetariumGame>();
            game.zone = zone;
            game.planets = planets;
            game.beacons = beacons;

            var objective = zone.gameObject.AddComponent<PlanetariumObjective>();
            objective.title = "Colour the planets";
            objective.description = "Every planet is blank. Paint each one a different colour.";
            objective.game = game;
            objective.zone = zone;
            objective.testKey = UnityEngine.InputSystem.Key.K;
            zone.objective = objective;
        }

        // a black sky with scattered stars, tileable
        static Texture2D LoadOrCreateStarTexture()
        {
            string path = TextureFolder + "/PlanetariumStars.png";
            const int Size = 512;
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float gradient = 0.5f + 0.5f * Mathf.PerlinNoise(x * 0.012f, y * 0.012f);
                    pixels[y * Size + x] = new Color32((byte)(8 + 14 * gradient), (byte)(10 + 12 * gradient), (byte)(28 + 30 * gradient), 255);
                }
            }
            var random = new System.Random(11);
            for (int i = 0; i < 260; i++)
            {
                int sx = random.Next(Size), sy = random.Next(Size);
                float bright = 0.5f + (float)random.NextDouble() * 0.5f;
                int radius = random.NextDouble() < 0.12 ? 2 : 1;
                for (int dy = -radius; dy <= radius; dy++)
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        if (dx * dx + dy * dy > radius * radius) continue;
                        int px = (sx + dx + Size) % Size, py = (sy + dy + Size) % Size;
                        byte v = (byte)(255 * bright);
                        pixels[py * Size + px] = new Color32(v, v, (byte)Mathf.Min(255, v + 25), 255);
                    }
            }
            texture.SetPixels32(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
