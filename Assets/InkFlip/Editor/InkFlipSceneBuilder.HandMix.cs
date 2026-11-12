using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace InkFlip.EditorTools
{
    // The Hand Mix room, dressed as a candy factory (south of the entry hall's east half): paint hoppers, a conveyor belt of
    // candies in the sample colours, a sample shelf where you click a sample to mix it by hand, and a palette machine for
    // making the ink colours. Mixing three colours earns the Ink Blaster. It does not count towards opening the elevator.
    public static partial class InkFlipSceneBuilder
    {
        // x from 6.1 to 12.2, z from -12.2 to -6.1. Its west wall is the Colour Lab's east wall; its door is in the hall's south wall (x = 10.1).
        static void BuildHandMixRoom(Transform room, Material floor, Material wall, Material furniture)
        {
            Transform group = Group("HandMixRoom", room);
            Box("Floor_HandMix", group, new Vector3(6.1f, 0.2f, 6.2f), new Vector3(9.15f, -0.1f, -9.1f), floor);
            Box("Wall_HandMix_E", group, new Vector3(0.2f, 3.2f, 6.2f), new Vector3(12.1f, 1.6f, -9.1f), wall);
            Box("Wall_HandMix_S", group, new Vector3(6.2f, 3.2f, 0.2f), new Vector3(9.1f, 1.6f, -12.2f), wall);

            RoomZone zone = MakeZone(room, "hand_mix_room", "Candy Factory", "Mix three colours to earn the Ink Blaster",
                                     new Color(0.95f, 0.45f, 0.65f), new Vector3(9.15f, 0f, -9.1f), Ability.PaintBrush, countsForFinal: false);
            BuildHandMixContents(group, zone, furniture);
        }

        static void BuildHandMixContents(Transform group, RoomZone zone, Material furniture)
        {
            EnsureFolder(TextureFolder);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            Shader paintable = Shader.Find("InkFlip/PaintableLit");

            LabMaterials m = CreateLabMaterials(lit, unlit);
            Material beltBody = CreateMaterial("FactoryBelt", paintable, "#3A3F4B", 0.3f);
            Material beltRail = CreateMaterial("FactoryRail", paintable, "#C9CED8", 0.6f);
            Material machine = CreateMaterial("FactoryMachine", paintable, "#E8505B", 0.4f);
            Material machineDark = CreateMaterial("FactoryMachineDark", paintable, "#2C3E66", 0.3f);
            Material screenMaterial = CreateMaterial("FactoryScreen", unlit, "#FFF6C8", 0f);

            // ---- the paint hoppers along the west wall: five tall tanks, one per paint, so every colour is easy to see ----
            Transform hoppers = Group("Hoppers", group);
            for (int i = 0; i < LabPaintColors.Length; i++)
            {
                Material tank = CreateMaterial("Hopper_" + LabPaintNames[i], lit, Hex(LabPaintColors[i]), 0.65f);
                Vector3 at = new Vector3(6.75f, 0f, -7.0f - i * 0.85f);
                FactoryPart(hoppers, "Tank_" + LabPaintNames[i], PrimitiveType.Cylinder, new Vector3(0.6f, 0.9f, 0.6f), at + new Vector3(0f, 1.25f, 0f), Quaternion.identity, tank);
                FactoryPart(hoppers, "Funnel_" + LabPaintNames[i], PrimitiveType.Cylinder, new Vector3(0.3f, 0.25f, 0.3f), at + new Vector3(0f, 0.3f, 0f), Quaternion.identity, tank);
                FactoryPart(hoppers, "Cap_" + LabPaintNames[i], PrimitiveType.Cylinder, new Vector3(0.64f, 0.05f, 0.64f), at + new Vector3(0f, 2.18f, 0f), Quaternion.identity, beltRail);
                MakeFactoryLabel(hoppers, LabPaintNames[i].ToUpperInvariant(), at + new Vector3(0.34f, 1.25f, 0f), Quaternion.LookRotation(Vector3.left), 0.07f);
            }

            // ---- the conveyor belt: candies in the four sample colours roll along it ----
            Transform belt = Group("Belt", group);
            const float BeltZ = -9.2f;
            Box("Belt_Body", belt, new Vector3(4.7f, 0.18f, 0.9f), new Vector3(9.4f, 0.8f, BeltZ), beltBody);
            Box("Belt_RailN", belt, new Vector3(4.7f, 0.1f, 0.06f), new Vector3(9.4f, 0.93f, BeltZ + 0.45f), beltRail);
            Box("Belt_RailS", belt, new Vector3(4.7f, 0.1f, 0.06f), new Vector3(9.4f, 0.93f, BeltZ - 0.45f), beltRail);
            foreach (float legX in new[] { 7.3f, 9.4f, 11.5f })
            {
                Box("Belt_Leg", belt, new Vector3(0.12f, 0.72f, 0.7f), new Vector3(legX, 0.36f, BeltZ), beltRail);
            }

            var candies = new GameObject("Candies").transform;
            candies.SetParent(belt, false);
            var paintColors = new List<Color>(LabPaintColors);
            // what the shelf asks for: a dark red, an orange, then two of any colour you like
            var requests = new (string name, int[] parts, bool any)[]
            {
                ("Dark Red", new[] { 3, 0, 0, 0, 1 }, false),
                ("Orange", new[] { 1, 1, 0, 0, 0 }, false),
                ("Any Colour", new int[5], true),
                ("Any Colour", new int[5], true),
            };
            Color[] requestColors =
            {
                ColorLabGame.MixColor(paintColors, requests[0].parts),
                ColorLabGame.MixColor(paintColors, requests[1].parts),
                new Color(0.42f, 0.84f, 0.76f),
                new Color(0.72f, 0.62f, 1f),
            };
            const int CandyCount = 12;
            for (int i = 0; i < CandyCount; i++)
            {
                int sample = i % requests.Length;
                Color candyColor = requestColors[sample];
                Material candyMaterial = CreateMaterial("Candy_" + sample, lit, Hex(candyColor), 0.8f);
                float x = 7.3f + i * (4.3f / CandyCount);
                float tilt = (i % 3) * 12f;
                FactoryPart(candies, "Candy", PrimitiveType.Sphere, new Vector3(0.3f, 0.12f, 0.3f), new Vector3(x, 0.96f, BeltZ + ((i % 2) - 0.5f) * 0.2f),
                            Quaternion.Euler(tilt, i * 37f, 0f), candyMaterial);
            }
            var conveyor = candies.gameObject.AddComponent<Conveyor>();
            conveyor.minX = 7.2f;
            conveyor.maxX = 11.5f;

            // ---- the sample shelf on the south wall, each sample with a big candy on the wall above it ----
            Box("SampleShelf_HandMix", group, new Vector3(5.2f, 0.06f, 0.45f), new Vector3(9.15f, 1.3f, -11.85f), furniture);
            Box("ShelfBracket_W", group, new Vector3(0.08f, 0.3f, 0.4f), new Vector3(6.8f, 1.12f, -11.87f), furniture);
            Box("ShelfBracket_E", group, new Vector3(0.08f, 0.3f, 0.4f), new Vector3(11.5f, 1.12f, -11.87f), furniture);

            var sampleBeakers = new LabBeaker[requests.Length];
            var recipes = new ColorLabGame.Sample[requests.Length];
            for (int i = 0; i < requests.Length; i++)
            {
                Color sampleColor = requestColors[i];
                Material liquidMaterial = CreateMaterial("Liquid_Sample" + i, unlit, Hex(sampleColor), 0f);
                float x = 9.15f + (i - (requests.Length - 1) * 0.5f) * 1.2f;
                sampleBeakers[i] = MakeBeaker(group, "HandSample_" + i + "_" + requests[i].name.Replace(' ', '_'), new Vector3(x, 1.33f, -11.85f),
                                              LabBeaker.Kind.Sample, requests[i].name, sampleColor, 0.75f, liquidMaterial, m, corked: true);

                GameObject light = LabCube(group, "HandSampleLight_" + i, new Vector3(x, 2.05f, -12.07f), new Vector3(0.16f, 0.16f, 0.04f), m.light);
                sampleBeakers[i].statusLight = light.GetComponent<MeshRenderer>();

                Material candyMaterial = CreateMaterial("Candy_" + i, lit, Hex(sampleColor), 0.8f);
                FactoryPart(group, "WallCandy_" + i, PrimitiveType.Cylinder, new Vector3(0.7f, 0.05f, 0.7f), new Vector3(x, 2.5f, -12.05f), Quaternion.Euler(90f, 0f, 0f), candyMaterial);

                recipes[i] = new ColorLabGame.Sample { name = requests[i].name, parts = (int[])requests[i].parts.Clone(), anyColor = requests[i].any };
            }

            // ---- the palette machine, in the north-east corner: make the ink colours you fire ----
            Transform press = Group("PaletteMachine", group);
            Box("Press_Body", press, new Vector3(1.1f, 1.2f, 0.9f), new Vector3(11.4f, 0.6f, -7.3f), machine);
            Box("Press_Base", press, new Vector3(1.25f, 0.12f, 1.05f), new Vector3(11.4f, 0.06f, -7.3f), machineDark);
            Box("Press_Top", press, new Vector3(1.0f, 0.5f, 0.8f), new Vector3(11.4f, 1.45f, -7.3f), machineDark);
            FactoryPart(press, "Press_Screen", PrimitiveType.Cube, new Vector3(0.8f, 0.3f, 0.03f), new Vector3(11.4f, 1.45f, -6.88f), Quaternion.identity, screenMaterial);
            MakeFactoryLabel(press, "INK PALETTE", new Vector3(11.4f, 1.45f, -6.86f), Quaternion.LookRotation(Vector3.back), 0.055f, new Color(0.15f, 0.12f, 0.2f));
            for (int i = 0; i < LabPaintColors.Length; i++)
            {
                Material dot = CreateMaterial("Hopper_" + LabPaintNames[i], lit, Hex(LabPaintColors[i]), 0.65f);
                FactoryPart(press, "Press_Dot", PrimitiveType.Sphere, Vector3.one * 0.12f, new Vector3(11.4f + (i - 2) * 0.17f, 0.85f, -6.84f), Quaternion.identity, dot);
            }

            // ---- the Ink Blaster on a pedestal: it appears when three colours are matched ----
            Transform pedestal = Group("BlasterPedestal", group);
            Vector3 pedestalAt = new Vector3(9.6f, 0f, -7.9f);
            Box("Pedestal_Base", pedestal, new Vector3(0.9f, 0.1f, 0.9f), new Vector3(pedestalAt.x, 0.05f, pedestalAt.z), machineDark);
            Box("Pedestal_Column", pedestal, new Vector3(0.6f, 0.75f, 0.6f), new Vector3(pedestalAt.x, 0.475f, pedestalAt.z), beltRail);
            Box("Pedestal_Top", pedestal, new Vector3(0.8f, 0.06f, 0.8f), new Vector3(pedestalAt.x, 0.88f, pedestalAt.z), machineDark);

            Material blasterBody = CreateMaterial("PickupBlasterBody", lit, "#2C3E66", 0.4f);
            Material blasterTank = CreateMaterial("PickupBlasterTank", lit, "#FF3D8B", 0.6f);
            var blaster = new GameObject("InkBlasterModel").transform;
            blaster.SetParent(pedestal, false);
            blaster.position = new Vector3(pedestalAt.x, 1.2f, pedestalAt.z);
            FactoryPart(blaster, "Body", PrimitiveType.Cube, new Vector3(0.14f, 0.16f, 0.4f), blaster.position, Quaternion.identity, blasterBody);
            FactoryPart(blaster, "Tank", PrimitiveType.Cylinder, new Vector3(0.18f, 0.1f, 0.18f), blaster.position + new Vector3(0f, 0.17f, -0.05f), Quaternion.identity, blasterTank);
            FactoryPart(blaster, "Nozzle", PrimitiveType.Cylinder, new Vector3(0.07f, 0.1f, 0.07f), blaster.position + new Vector3(0f, 0f, 0.27f), Quaternion.Euler(90f, 0f, 0f), blasterTank);
            var blasterBox = blaster.gameObject.AddComponent<BoxCollider>();
            blasterBox.center = new Vector3(0f, 0.05f, 0.05f);
            blasterBox.size = new Vector3(0.4f, 0.45f, 0.75f);

            var pickup = pedestal.gameObject.AddComponent<AbilityPickup>();
            pickup.ability = Ability.InkBlaster;
            pickup.model = blaster.gameObject;
            pickup.zone = zone;
            pickup.itemName = "Ink Blaster";
            pickup.pickedUpMessage = "You got the Ink Blaster!\nMake your own ink colours at the palette machine.";

            // ---- Smudge runs the factory ----
            PlaceTalker(group, "Smudge", "Assets/Prototype/Messy Image/buddy_04.png", new Vector3(8.3f, 0f, -7.3f), 1.1f);

            // ---- the game and the goal ----
            var gameObject = new GameObject("HandMix");
            gameObject.transform.SetParent(group, false);
            var game = gameObject.AddComponent<HandMixGame>();
            game.zone = zone;
            game.samples = recipes;
            game.sampleBeakers = sampleBeakers;
            game.paints = (Color[])LabPaintColors.Clone();
            game.paletteStation = press;
            game.blasterPickup = pickup;

            var objective = zone.gameObject.AddComponent<HandMixObjective>();
            objective.title = "Mix three colours";
            objective.description = "Click a sample and mix it by hand in the pool. Three matches earn the Ink Blaster.";
            objective.game = game;
            objective.zone = zone;
            objective.testKey = UnityEngine.InputSystem.Key.K;
            zone.objective = objective;
        }

        // a primitive without its collider (decoration)
        static void FactoryPart(Transform parent, string name, PrimitiveType type, Vector3 scale, Vector3 worldPosition, Quaternion rotation, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.position = worldPosition;
            part.transform.rotation = rotation;
            part.transform.localScale = scale;
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static void MakeFactoryLabel(Transform parent, string text, Vector3 worldPosition, Quaternion rotation, float characterSize, Color? color = null)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.position = worldPosition;
            go.transform.rotation = rotation;
            var mesh = go.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.characterSize = characterSize;
            mesh.fontSize = 64;
            mesh.color = color ?? Color.white;
        }
    }
}
