using UnityEditor;
using UnityEngine;

namespace InkFlip.EditorTools
{
    // The contents of the Glove Room (the north-west room): a big blank glove on a pedestal to decorate with the pencil, and,
    // for after you've picked it up, a levitation puzzle - three crates and three switches, one of them on a ledge and one
    // in a pocket behind a low wall, so things have to be lifted. The scripts are GloveRoomGame, LevitationSwitch, GloveRoomObjective.
    public static partial class InkFlipSceneBuilder
    {
        // A big glove, standing up with its fingers pointing along +Y and the back of the hand facing -Z.
        // The origin is the middle of the palm. It is a few boxes merged into one mesh so the pencil can draw on all of it.
        static Mesh BuildGloveMesh()
        {
            // size, centre, tilt (degrees about Z)
            var parts = new (Vector3 size, Vector3 centre, float tilt)[]
            {
                (new Vector3(0.52f, 0.52f, 0.16f), new Vector3(0f, 0f, 0f), 0f),          // palm
                (new Vector3(0.46f, 0.12f, 0.18f), new Vector3(0f, -0.32f, 0f), 0f),      // wrist
                (new Vector3(0.62f, 0.26f, 0.24f), new Vector3(0f, -0.45f, 0f), 0f),      // cuff
                (new Vector3(0.115f, 0.40f, 0.14f), new Vector3(-0.195f, 0.46f, 0f), 0f), // index finger
                (new Vector3(0.115f, 0.46f, 0.14f), new Vector3(-0.065f, 0.49f, 0f), 0f), // middle
                (new Vector3(0.115f, 0.43f, 0.14f), new Vector3(0.065f, 0.475f, 0f), 0f), // ring
                (new Vector3(0.115f, 0.34f, 0.14f), new Vector3(0.195f, 0.43f, 0f), 0f),  // little finger
                (new Vector3(0.14f, 0.34f, 0.14f), new Vector3(-0.38f, 0.1f, 0f), 32f),   // thumb
            };

            var instances = new CombineInstance[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                instances[i] = new CombineInstance
                {
                    mesh = BuildBoxMesh(parts[i].size),
                    transform = Matrix4x4.TRS(parts[i].centre, Quaternion.Euler(0f, 0f, parts[i].tilt), Vector3.one),
                };
            }

            var combined = new Mesh();
            combined.CombineMeshes(instances, true, true);
            combined.RecalculateBounds();
            combined.RecalculateTangents();
            foreach (CombineInstance instance in instances) Object.DestroyImmediate(instance.mesh);
            return combined;
        }

        static LevitationSwitch MakeLevitationSwitch(Transform group, string name, Vector3 surfacePoint, Material plateMaterial, Material beaconMaterial)
        {
            var root = new GameObject(name).transform;
            root.SetParent(group, false);
            root.position = surfacePoint;

            Box("Plate", root, new Vector3(1.0f, 0.04f, 1.0f), surfacePoint + new Vector3(0f, 0.02f, 0f), plateMaterial);

            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Beacon";
            Object.DestroyImmediate(pole.GetComponent<Collider>());
            pole.transform.SetParent(root, false);
            pole.transform.position = surfacePoint + new Vector3(0.62f, 0.8f, 0f);
            pole.transform.localScale = new Vector3(0.07f, 0.8f, 0.07f);
            var poleRenderer = pole.GetComponent<MeshRenderer>();
            poleRenderer.sharedMaterial = beaconMaterial;
            poleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var sw = root.gameObject.AddComponent<LevitationSwitch>();
            sw.plateRenderer = root.Find("Plate").GetComponent<MeshRenderer>();
            sw.beaconRenderer = poleRenderer;
            sw.goalCenter = new Vector3(0f, 0.44f, 0f);
            sw.goalSize = new Vector3(0.9f, 0.8f, 0.9f);
            return sw;
        }

        static void BuildGloveRoomContents(Transform group, RoomZone zone, Material wall, Material furniture, Material crate)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            Shader paintable = Shader.Find("InkFlip/PaintableLit");

            Material gloveMaterial = CreateMaterial("BlankGlove", paintable, "#F4F1EA", 0.25f);
            Material navy = CreateMaterial("LabNavyPaintable", paintable, "#1D2B4F", 0.35f);
            Material plateMaterial = CreateMaterial("SwitchPlate", paintable, "#8E98A6", 0.5f);
            Material beaconMaterial = CreateMaterial("SwitchBeacon", unlit, "#6B707F", 0f);

            // ---- the pedestal with the blank glove ----
            Vector3 centre = new Vector3(-3f, 0f, 9.1f);
            Transform pedestal = Group("GlovePedestal", group);
            Box("Pedestal_Base", pedestal, new Vector3(1.4f, 0.12f, 1.4f), new Vector3(centre.x, 0.06f, centre.z), navy);
            Box("Pedestal_Column", pedestal, new Vector3(1.0f, 0.78f, 1.0f), new Vector3(centre.x, 0.51f, centre.z), furniture);
            Box("Pedestal_Top", pedestal, new Vector3(1.2f, 0.06f, 1.2f), new Vector3(centre.x, 0.93f, centre.z), navy);

            Mesh gloveMesh = LoadOrCreateMesh("BigGlove_v1", BuildGloveMesh);
            CreatePaintable("BigGlove", pedestal, gloveMesh, new Vector3(centre.x, 0.96f + 0.58f, centre.z), Quaternion.identity, gloveMaterial);
            Paintable glovePaintable = pedestal.Find("BigGlove").GetComponent<Paintable>();
            glovePaintable.textureSize = 1024;
            glovePaintable.gameObject.AddComponent<DrawTurntable>(); // turn it while drawing to reach the other side

            // ---- the puzzle: a ledge, and a pocket behind a low wall ----
            Box("Ledge", group, new Vector3(1.8f, 0.9f, 2.0f), new Vector3(-1.0f, 0.45f, 10.9f), furniture);
            Box("PocketWall_S", group, new Vector3(2.6f, 1.2f, 0.2f), new Vector3(-4.7f, 0.6f, 10.9f), wall);
            Box("PocketWall_E", group, new Vector3(0.2f, 1.2f, 1.3f), new Vector3(-3.4f, 0.6f, 11.45f), wall);

            var switches = new[]
            {
                MakeLevitationSwitch(group, "Switch_Floor", new Vector3(-1.0f, 0f, 7.0f), plateMaterial, beaconMaterial),
                MakeLevitationSwitch(group, "Switch_Ledge", new Vector3(-1.0f, 0.9f, 10.9f), plateMaterial, beaconMaterial),
                MakeLevitationSwitch(group, "Switch_Pocket", new Vector3(-4.75f, 0f, 11.55f), plateMaterial, beaconMaterial),
            };

            // three crates to move (they can be resized too)
            Box("PuzzleCrate_A", group, Vector3.one * 0.7f, new Vector3(-5.5f, 0.35f, 7.0f), crate, Quaternion.Euler(0f, 12f, 0f));
            Box("PuzzleCrate_B", group, Vector3.one * 0.8f, new Vector3(-4.7f, 0.4f, 7.0f), crate, Quaternion.Euler(0f, -8f, 0f));
            Box("PuzzleCrate_C", group, Vector3.one * 0.6f, new Vector3(-5.5f, 0.3f, 7.9f), crate, Quaternion.Euler(0f, 25f, 0f));
            foreach (string crateName in new[] { "PuzzleCrate_A", "PuzzleCrate_B", "PuzzleCrate_C" })
            {
                group.Find(crateName).gameObject.AddComponent<Grabbable>();
            }

            // ---- Mitten explains the room (lines come from Dialogue.csv) ----
            PlaceTalker(group, "Mitten", "Assets/Prototype/Messy Image/buddy_01.png", new Vector3(-1.3f, 0f, 8.6f), 1.1f);

            // ---- the game and the goal ----
            var gameObject = new GameObject("GloveRoomGame");
            gameObject.transform.SetParent(group, false);
            var game = gameObject.AddComponent<GloveRoomGame>();
            game.zone = zone;
            game.glove = glovePaintable;
            game.pedestal = pedestal;
            game.switches = switches;

            var objective = zone.gameObject.AddComponent<GloveRoomObjective>();
            objective.title = "Earn the glove";
            objective.description = "Decorate the glove, pick it up, then put something on every switch.";
            objective.game = game;
            objective.zone = zone;
            objective.testKey = UnityEngine.InputSystem.Key.K;
            zone.objective = objective;
        }
    }
}
