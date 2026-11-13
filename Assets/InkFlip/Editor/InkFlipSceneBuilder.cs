using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace InkFlip.EditorTools
{
    // Tools > InkFlip > Build Paint Test Room
    // Generates a small furnished room, the player + ink gun, the paint manager and
    // the colour HUD, then saves it as Assets/InkFlip/Scenes/PaintTestRoom.unity.
    // Safe to re-run: generated meshes are reused, materials are updated and the scene is overwritten.
    public static partial class InkFlipSceneBuilder
    {
        const string Root = "Assets/InkFlip";
        const string MeshFolder = Root + "/Generated/Meshes";
        const string MaterialFolder = Root + "/Generated/Materials";
        const string ScenePath = Root + "/Scenes/PaintTestRoom.unity";

        const int IgnoreRaycastLayer = 2;

        [MenuItem("Tools/InkFlip/Build Paint Test Room")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
            Debug.Log("InkFlip: built " + ScenePath + ". Press Play, click the Game view, and paint!");
        }

        public static Scene Build()
        {
            EnsureFolder(MeshFolder);
            EnsureFolder(MaterialFolder);
            EnsureFolder(Root + "/Scenes");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Shader paintableShader = Shader.Find("InkFlip/PaintableLit");
            // hero-institute palette: cool white panelled walls, dark slate tiles, steel-blue crates (see ApplyInstituteLook)
            Material wall = CreateMaterial("Wall", paintableShader, "#E3E9F1", 0.2f);
            Material floor = CreateMaterial("Floor", paintableShader, "#707B8C", 0.35f);
            Material furniture = CreateMaterial("Furniture", paintableShader, "#F1F4F8", 0.3f);
            Material crate = CreateMaterial("Crate", paintableShader, "#4F6A8F", 0.2f);
            ApplyInstituteLook(wall, floor);

            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            Material gunBody = CreateMaterial("GunBody", litShader, "#34343A", 0.6f);
            Material gunInk = CreateMaterial("GunInk", litShader, "#FF3D8B", 0.85f);
            Material projectile = CreateMaterial("InkBlob", Shader.Find("Universal Render Pipeline/Unlit"), "#FF3D8B", 0f);

            SetupLighting();

            var room = new GameObject("Room").transform;

            // shell: open-topped so the sun lights the inside. The hall is 18 m wide (x from -6 to 12) and 12 m deep;
            // the elevator is in the east wall (x = 12), see BuildElevator.
            Box("Floor", room, new Vector3(18f, 0.2f, 12f), new Vector3(3f, -0.1f, 0f), floor);
            // the north wall has two doorways (x = -3 and x = 3) into the Glove Room and the Ink Room
            Box("Wall_North_W", room, new Vector3(2.2f, 3.2f, 0.2f), new Vector3(-5.1f, 1.6f, 6.1f), wall);
            Box("Wall_North_M", room, new Vector3(4f, 3.2f, 0.2f), new Vector3(0f, 1.6f, 6.1f), wall);
            Box("Wall_North_E", room, new Vector3(2.2f, 3.2f, 0.2f), new Vector3(5.1f, 1.6f, 6.1f), wall);
            Box("Lintel_North_W", room, new Vector3(2f, 0.8f, 0.2f), new Vector3(-3f, 2.8f, 6.1f), wall);
            Box("Lintel_North_E", room, new Vector3(2f, 0.8f, 0.2f), new Vector3(3f, 2.8f, 6.1f), wall);
            Box("Wall_North_Ext", room, new Vector3(6f, 3.2f, 0.2f), new Vector3(9.2f, 1.6f, 6.1f), wall);
            // the south wall has two doorways (x = -3 and x = 3) into the Drawing Room and the Color Room
            Box("Wall_South_W", room, new Vector3(2.2f, 3.2f, 0.2f), new Vector3(-5.1f, 1.6f, -6.1f), wall);
            Box("Wall_South_M", room, new Vector3(4f, 3.2f, 0.2f), new Vector3(0f, 1.6f, -6.1f), wall);
            Box("Wall_South_E", room, new Vector3(2.2f, 3.2f, 0.2f), new Vector3(5.1f, 1.6f, -6.1f), wall);
            Box("Lintel_South_W", room, new Vector3(2f, 0.8f, 0.2f), new Vector3(-3f, 2.8f, -6.1f), wall);
            Box("Lintel_South_E", room, new Vector3(2f, 0.8f, 0.2f), new Vector3(3f, 2.8f, -6.1f), wall);
            // the east half of the south wall has a doorway (x from 9.1 to 11.1) into the Hand Mix room
            Box("Wall_South_Ext_W", room, new Vector3(2.9f, 3.2f, 0.2f), new Vector3(7.65f, 1.6f, -6.1f), wall);
            Box("Wall_South_Ext_E", room, new Vector3(1.1f, 3.2f, 0.2f), new Vector3(11.65f, 1.6f, -6.1f), wall);
            Box("Lintel_South_Ext", room, new Vector3(2f, 0.8f, 0.2f), new Vector3(10.1f, 2.8f, -6.1f), wall);
            // (the east wall is the elevator, built in BuildFinalArea)
            // the west wall has a doorway (z from -1 to 1) into the Sculpt Room
            Box("Wall_West_S", room, new Vector3(0.2f, 3.2f, 5f), new Vector3(-6.1f, 1.6f, -3.5f), wall);
            Box("Wall_West_N", room, new Vector3(0.2f, 3.2f, 5f), new Vector3(-6.1f, 1.6f, 3.5f), wall);
            Box("Lintel_West", room, new Vector3(0.2f, 0.8f, 2f), new Vector3(-6.1f, 2.8f, 0f), wall);

            // sofa
            Transform sofa = Group("Sofa", room);
            Box("Seat", sofa, new Vector3(2.4f, 0.45f, 0.9f), new Vector3(-3f, 0.225f, 4.5f), furniture);
            Box("Back", sofa, new Vector3(2.4f, 0.6f, 0.25f), new Vector3(-3f, 0.75f, 4.95f), furniture);
            Box("Arm_L", sofa, new Vector3(0.25f, 0.65f, 0.9f), new Vector3(-4.325f, 0.325f, 4.5f), furniture);
            Box("Arm_R", sofa, new Vector3(0.25f, 0.65f, 0.9f), new Vector3(-1.675f, 0.325f, 4.5f), furniture);

            // table with a ball on it
            Transform table = Group("Table", room);
            Box("Top", table, new Vector3(1.6f, 0.08f, 0.9f), new Vector3(1.5f, 0.76f, 0.5f), furniture);
            foreach (Vector2 leg in new[] { new Vector2(-0.7f, -0.35f), new Vector2(0.7f, -0.35f), new Vector2(-0.7f, 0.35f), new Vector2(0.7f, 0.35f) })
            {
                Box("Leg", table, new Vector3(0.08f, 0.72f, 0.08f), new Vector3(1.5f + leg.x, 0.36f, 0.5f + leg.y), furniture);
            }
            Primitive("Ball", table, "Sphere.fbx", new Vector3(0.6f, 0.6f, 0.6f), new Vector3(1.8f, 1.1f, 0.5f), furniture);

            // storage
            Box("Cabinet", room, new Vector3(1.2f, 1.8f, 0.5f), new Vector3(4.6f, 0.9f, 5.6f), furniture);
            Box("Bookshelf", room, new Vector3(0.4f, 2.2f, 2.2f), new Vector3(11.75f, 1.1f, 4.7f), furniture);
            Primitive("Pillar", room, "Cylinder.fbx", new Vector3(0.6f, 3.2f, 0.6f), new Vector3(-2.5f, 1.6f, -1.5f), wall);

            // crate stack + a ramp to climb
            Box("Crate_A", room, Vector3.one * 0.8f, new Vector3(-4.8f, 0.4f, -4.8f), crate);
            Box("Crate_B", room, Vector3.one * 0.8f, new Vector3(-3.9f, 0.4f, -4.9f), crate);
            Box("Crate_C", room, Vector3.one * 0.8f, new Vector3(-4.4f, 1.2f, -4.8f), crate, Quaternion.Euler(0f, 20f, 0f));
            Box("Ramp", room, new Vector3(2f, 0.2f, 4f), new Vector3(3.2f, 0.5f, -3.6f), furniture, Quaternion.Euler(-14f, 0f, 0f));

            BuildRoomsAndFinalArea(room, floor, wall, furniture, crate);
            BuildInstituteHall(room, furniture);

            // the glove can pick these up and resize them (the floor, walls and pillar stay put)
            foreach (string grabbable in new[] { "Crate_A", "Crate_B", "Crate_C", "Ramp", "Cabinet", "Bookshelf", "Sofa", "Table", "Table/Ball" })
            {
                Transform prop = room.Find(grabbable);
                if (prop != null) prop.gameObject.AddComponent<Grabbable>();
            }

            // the entry room is the free-play room: everything is allowed in here
            var entryZone = new GameObject("Zone_FreePlay").AddComponent<RoomZone>();
            entryZone.transform.SetParent(room, false);
            entryZone.roomId = "free_play";
            entryZone.displayName = "Free Play";
            entryZone.subtitle = "Everything is unlocked";
            entryZone.titleAccent = new Color(1f, 0.24f, 0.55f);
            entryZone.size = new Vector3(18f, 6f, 12f);
            entryZone.center = new Vector3(3f, 3f, 0f);
            entryZone.abilities = Ability.All;
            entryZone.countsForFinalArea = false;

            BuildManagers(projectile, gunBody, gunInk);

            // the room-title banner (a prefab you can restyle; it is not overwritten when the scene is rebuilt)
            RoomTitleCardBuilder.AddToScene();

            // the dialogue box, name plate and choice buttons (a prefab using your dialogue art; also not overwritten on rebuild)
            DialogueUIBuilder.AddToScene();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            return scene;
        }

        static void BuildManagers(Material projectileMaterial, Material gunBody, Material gunInk)
        {
            var paintManagerObject = new GameObject("PaintManager");
            var paintManager = paintManagerObject.AddComponent<PaintManager>();
            paintManager.splatShader = Shader.Find("Hidden/InkFlip/PaintSplat");
            paintManager.dilateShader = Shader.Find("Hidden/InkFlip/PaintDilate");

            var hudObject = new GameObject("PaintHUD");
            var selector = hudObject.AddComponent<PaintColorSelector>();
            var hud = hudObject.AddComponent<ColorPaletteHUD>();
            hud.colors = selector;

            // player - on Ignore Raycast so its own capsule never blocks shots or splats
            var player = new GameObject("Player") { layer = IgnoreRaycastLayer };
            player.transform.position = new Vector3(0f, 0.05f, -3.5f);
            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.35f;

            var pivot = new GameObject("CameraPivot").transform;
            pivot.SetParent(player.transform, false);
            pivot.localPosition = new Vector3(0f, 1.6f, 0f);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraObject.transform.SetParent(pivot, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 75f;
            camera.nearClipPlane = 0.03f;
            cameraObject.AddComponent<AudioListener>();

            var fps = player.AddComponent<FirstPersonController>();
            fps.cameraPivot = pivot;

            // gun viewmodel - lower right of the view, no colliders
            var gunRoot = new GameObject("InkGun").transform;
            gunRoot.SetParent(cameraObject.transform, false);
            gunRoot.localPosition = new Vector3(0.22f, -0.2f, 0.42f);
            gunRoot.localScale = Vector3.one * 0.7f;

            ViewModelPart("Body", gunRoot, PrimitiveType.Cube, new Vector3(0.09f, 0.11f, 0.34f), Vector3.zero, Quaternion.identity, gunBody);
            ViewModelPart("Grip", gunRoot, PrimitiveType.Cube, new Vector3(0.06f, 0.14f, 0.07f), new Vector3(0f, -0.1f, -0.08f), Quaternion.Euler(15f, 0f, 0f), gunBody);
            Renderer tank = ViewModelPart("InkTank", gunRoot, PrimitiveType.Capsule, new Vector3(0.08f, 0.09f, 0.08f), new Vector3(0f, 0.1f, -0.03f), Quaternion.Euler(90f, 0f, 0f), gunInk);
            Renderer nozzle = ViewModelPart("Nozzle", gunRoot, PrimitiveType.Cylinder, new Vector3(0.045f, 0.06f, 0.045f), new Vector3(0f, 0.01f, 0.22f), Quaternion.Euler(90f, 0f, 0f), gunInk);

            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(gunRoot, false);
            muzzle.localPosition = new Vector3(0f, 0.01f, 0.29f);

            var gun = gunRoot.gameObject.AddComponent<InkGun>();
            gun.aimCamera = camera;
            gun.muzzle = muzzle;
            gun.colors = selector;
            gun.tintRenderers = new[] { tank, nozzle };
            gun.projectileMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            gun.projectileMaterial = projectileMaterial;

            // paint brush viewmodel: handle, metal ferrule, ink-tinted bristles
            var brushRoot = new GameObject("PaintBrush").transform;
            brushRoot.SetParent(cameraObject.transform, false);
            brushRoot.localPosition = new Vector3(0.2f, -0.2f, 0.42f);

            var brushModel = new GameObject("Model").transform;
            brushModel.SetParent(brushRoot, false);
            brushModel.localRotation = Quaternion.Euler(-20f, -12f, 8f);

            Material handleMaterial = CreateMaterial("BrushHandle", Shader.Find("Universal Render Pipeline/Lit"), "#8A5A35", 0.35f);
            ViewModelPart("Handle", brushModel, PrimitiveType.Cylinder, new Vector3(0.028f, 0.13f, 0.028f), new Vector3(0f, 0f, -0.09f), Quaternion.Euler(90f, 0f, 0f), handleMaterial);
            ViewModelPart("Ferrule", brushModel, PrimitiveType.Cube, new Vector3(0.11f, 0.032f, 0.06f), new Vector3(0f, 0f, 0.07f), Quaternion.identity, gunBody);
            Renderer bristles = ViewModelPart("Bristles", brushModel, PrimitiveType.Cube, new Vector3(0.104f, 0.024f, 0.09f), new Vector3(0f, 0f, 0.145f), Quaternion.identity, gunInk);

            var brush = brushRoot.gameObject.AddComponent<PaintBrush>();
            brush.aimCamera = camera;
            brush.colors = selector;
            brush.brushModel = brushModel;
            brush.bristleRenderers = new[] { bristles };

            // pencil viewmodel: yellow wooden body, bare-wood cone, graphite tip, pink eraser
            var pencilRoot = new GameObject("Pencil").transform;
            pencilRoot.SetParent(cameraObject.transform, false);
            pencilRoot.localPosition = new Vector3(0.2f, -0.19f, 0.42f);
            pencilRoot.localRotation = Quaternion.Euler(-25f, -15f, 0f);

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Material pencilYellow = CreateMaterial("PencilBody", lit, "#F2C230", 0.4f);
            Material pencilWood = CreateMaterial("PencilWood", lit, "#E3C49A", 0.2f);
            Material graphite = CreateMaterial("PencilGraphite", lit, "#2E2E33", 0.5f);
            Material eraser = CreateMaterial("PencilEraser", lit, "#F08AA8", 0.1f);
            ViewModelPart("Body", pencilRoot, PrimitiveType.Cylinder, new Vector3(0.022f, 0.1f, 0.022f), Vector3.zero, Quaternion.Euler(90f, 0f, 0f), pencilYellow);
            ViewModelPart("Wood", pencilRoot, PrimitiveType.Cylinder, new Vector3(0.016f, 0.012f, 0.016f), new Vector3(0f, 0f, 0.11f), Quaternion.Euler(90f, 0f, 0f), pencilWood);
            ViewModelPart("Tip", pencilRoot, PrimitiveType.Capsule, new Vector3(0.008f, 0.012f, 0.008f), new Vector3(0f, 0f, 0.125f), Quaternion.Euler(90f, 0f, 0f), graphite);
            ViewModelPart("Ferrule", pencilRoot, PrimitiveType.Cylinder, new Vector3(0.024f, 0.008f, 0.024f), new Vector3(0f, 0f, -0.104f), Quaternion.Euler(90f, 0f, 0f), gunBody);
            ViewModelPart("Eraser", pencilRoot, PrimitiveType.Cylinder, new Vector3(0.022f, 0.01f, 0.022f), new Vector3(0f, 0f, -0.12f), Quaternion.Euler(90f, 0f, 0f), eraser);

            // glove viewmodel: a soft white glove with a coloured cuff
            var gloveRoot = new GameObject("Glove").transform;
            gloveRoot.SetParent(cameraObject.transform, false);
            gloveRoot.localPosition = new Vector3(0.2f, -0.2f, 0.42f);
            gloveRoot.localRotation = Quaternion.Euler(-20f, -12f, 0f);

            Material gloveWhite = CreateMaterial("GloveFabric", lit, "#F4F1EA", 0.25f);
            Material gloveCuff = CreateMaterial("GloveCuff", lit, "#FF3D8B", 0.3f);
            ViewModelPart("Palm", gloveRoot, PrimitiveType.Cube, new Vector3(0.085f, 0.03f, 0.09f), Vector3.zero, Quaternion.identity, gloveWhite);
            for (int finger = 0; finger < 4; finger++)
            {
                float x = -0.032f + finger * 0.0215f;
                float length = finger == 1 || finger == 2 ? 0.05f : 0.042f;
                ViewModelPart("Finger_" + finger, gloveRoot, PrimitiveType.Capsule, new Vector3(0.021f, length, 0.021f), new Vector3(x, 0.004f, 0.075f + length * 0.5f - 0.03f), Quaternion.Euler(90f, 0f, 0f), gloveWhite);
            }
            ViewModelPart("Thumb", gloveRoot, PrimitiveType.Capsule, new Vector3(0.024f, 0.04f, 0.024f), new Vector3(-0.058f, 0f, 0.02f), Quaternion.Euler(90f, 0f, 55f), gloveWhite);
            ViewModelPart("Cuff", gloveRoot, PrimitiveType.Cylinder, new Vector3(0.095f, 0.022f, 0.095f), new Vector3(0f, 0f, -0.07f), Quaternion.Euler(90f, 0f, 0f), gloveCuff);

            // selection outline + draw mode
            var selectionObject = new GameObject("Selection");
            var highlighter = selectionObject.AddComponent<OutlineHighlighter>();
            highlighter.outlineShader = Shader.Find("InkFlip/Outline");

            var switcher = player.AddComponent<ToolSwitcher>();

            // sculpt studio: B (or the on-screen button) opens it, Done drops the sculpture in front of you
            var sculptStudio = new GameObject("SculptStudio").AddComponent<SculptStudio>();
            sculptStudio.paintableShader = Shader.Find("InkFlip/PaintableLit");
            sculptStudio.player = fps;
            sculptStudio.tools = switcher;
            sculptStudio.worldCamera = camera;

            // conversations with characters (NPCTalker): F to talk
            var dialogueRunner = new GameObject("DialogueRunner").AddComponent<DialogueRunner>();
            dialogueRunner.player = fps;
            dialogueRunner.tools = switcher;

            var drawMode = selectionObject.AddComponent<SelectionDrawMode>();
            drawMode.drawCamera = camera;
            drawMode.player = fps;
            drawMode.tools = switcher;
            drawMode.colors = selector;
            drawMode.highlighter = highlighter;

            var pencil = pencilRoot.gameObject.AddComponent<PencilTool>();
            pencil.aimCamera = camera;
            pencil.highlighter = highlighter;
            pencil.drawMode = drawMode;

            var glove = gloveRoot.gameObject.AddComponent<GloveTool>();
            glove.aimCamera = camera;
            glove.highlighter = highlighter;
            glove.gloveModel = gloveRoot;

            // the glove can also raise blocks from the floor, in every room once the glove has been earned
            var shapeTool = gloveRoot.gameObject.AddComponent<ShapeTool>();
            shapeTool.aimCamera = camera;
            shapeTool.player = fps;
            shapeTool.glove = glove;
            shapeTool.colors = selector;
            shapeTool.outlineMaterial = CreateMaterial("ShaperLine", Shader.Find("Universal Render Pipeline/Unlit"), "#FFFFFF", 0f);
            shapeTool.blockMaterial = CreateMaterial("ShapedBlock", Shader.Find("Universal Render Pipeline/Lit"), "#FFFFFF", 0.25f);

            switcher.directKeys = new[] { UnityEngine.InputSystem.Key.Z, UnityEngine.InputSystem.Key.X, UnityEngine.InputSystem.Key.C, UnityEngine.InputSystem.Key.V };
            switcher.tools = new[]
            {
                new PaintTool { name = "Ink Blaster", controls = "LMB: blast   RMB: roller", root = gunRoot.gameObject, requires = Ability.InkBlaster },
                new PaintTool { name = "Paint Brush", controls = "Hold LMB and sweep", root = brushRoot.gameObject, requires = Ability.PaintBrush },
                new PaintTool { name = "Pencil", controls = "Look at an object, LMB: draw on it", root = pencilRoot.gameObject, requires = Ability.Pencil },
                new PaintTool { name = "Glove", controls = "Hold LMB: carry   Wheel: resize   Floor: draw a block", root = gloveRoot.gameObject, requires = Ability.Glove },
            };
            hud.tools = switcher;
            hud.drawMode = drawMode;
            hudObject.AddComponent<DrawModeHUD>().drawMode = drawMode;
        }

        static void SetupLighting()
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.color = new Color(1f, 0.96f, 0.9f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.72f, 0.78f, 0.9f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.6f, 0.58f);
            RenderSettings.ambientGroundColor = new Color(0.35f, 0.32f, 0.3f);
            RenderSettings.sun = sun;
        }

        // Five placeholder rooms around the entry room, each with its own rules and goal, plus the final area behind the gate.
        //   north-west: Glove Room   north-east: Ink Room   west: Sculpt Room
        //   south-west: Drawing Room   south-east: Color Room   east (behind the gate): Final Area
        // The goals are stand-ins: swap each room's objective for the real mini-game later.
        // Press K inside a room to finish it while testing.
        static void BuildRoomsAndFinalArea(Transform room, Material floor, Material wall, Material furniture, Material crate)
        {
            BuildGloveRoom(room, floor, wall, furniture, crate);
            BuildInkRoom(room, floor, wall, furniture);
            // the old Sculpt Room was removed: its doorway in the hall's west wall is filled in
            Box("Wall_West_Closed", room, new Vector3(0.2f, 3.2f, 2f), new Vector3(-6.1f, 1.6f, 0f), wall);
            BuildDrawingRoom(room, floor, wall, furniture);
            BuildCatRoom(room, floor, wall, furniture);
            BuildHandMixRoom(room, floor, wall, furniture);
            BuildFinalArea(room, floor, wall, furniture, crate);
            BuildHallPictures(room); // the colour pictures hang in the main hall
            BuildHubDoors(room);     // the doors stay shut until the secretary has signed you in
        }

        // the secretary at the reception desk, and a door in each doorway that stays shut until she has signed the player in
        // (the Cat Room makes its own door)
        static void BuildHubDoors(Transform room)
        {
            Transform group = Group("HubDoors", room);
            Material doorMaterial = CreateMaterial("LockedDoor", Shader.Find("InkFlip/PaintableLit"), "#C0443E", 0.3f);
            Vector3[] doorways =
            {
                new Vector3(-3f, 1.2f, 6.1f), new Vector3(3f, 1.2f, 6.1f),      // north: Glove Room, Ink Room
                new Vector3(-3f, 1.2f, -6.1f), new Vector3(10.1f, 1.2f, -6.1f), // south: Line Room, Candy Factory
            };
            foreach (Vector3 at in doorways)
            {
                Box("HubDoor", group, new Vector3(2f, 2.4f, 0.16f), at, doorMaterial);
                var door = group.GetChild(group.childCount - 1).gameObject.AddComponent<AbilityDoor>();
                door.needsSecretary = true;
                door.requires = Ability.None;
            }

            GameObject secretary = PlaceTalker(group, "Penny", "Assets/Prototype/Messy Image/buddy_05.png", new Vector3(9.7f, 0f, 5.3f), 1.1f);
            if (secretary != null) secretary.AddComponent<DoorKeeper>();
        }

        // A character you can talk to: a camera-facing sprite with a collider and an NPCTalker pointing at their
        // conversations. If the character's asset doesn't exist yet it is built from Dialogue.csv.
        static GameObject PlaceTalker(Transform parent, string npcName, string spritePath, Vector3 ground, float height)
        {
            string assetPath = $"Assets/Data/NPCs/{npcName}.asset";
            NPCData data = AssetDatabase.LoadAssetAtPath<NPCData>(assetPath);
            if (data == null)
            {
                GameContentImporter.ImportDialogue();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                data = AssetDatabase.LoadAssetAtPath<NPCData>(assetPath);
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (data == null || sprite == null)
            {
                Debug.LogWarning($"InkFlip: couldn't place {npcName} (asset found: {data != null}, sprite found: {sprite != null}). " +
                                 "Check Dialogue.csv has rows for them, then run Tools > Import Game Content.");
                return null;
            }

            var root = new GameObject(npcName);
            root.transform.SetParent(parent, false);
            root.transform.position = ground;

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.radius = 0.3f;
            capsule.height = height;
            capsule.center = new Vector3(0f, height * 0.5f, 0f);

            var talker = root.AddComponent<NPCTalker>();
            talker.npc = data;

            var art = new GameObject("Sprite");
            art.transform.SetParent(root.transform, false);
            art.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            art.transform.localScale = Vector3.one * (height / sprite.bounds.size.y);
            art.AddComponent<SpriteRenderer>().sprite = sprite;
            art.AddComponent<Billboard>();
            return root;
        }

        // a box over a room that sets what the player can do inside it
        static RoomZone MakeZone(Transform room, string id, string title, string subtitle, Color accent, Vector3 floorCentre, Ability abilities, bool countsForFinal = true)
        {
            var zoneObject = new GameObject("Zone_" + id);
            zoneObject.transform.SetParent(room, false);
            zoneObject.transform.position = new Vector3(floorCentre.x, 0f, floorCentre.z);

            var zone = zoneObject.AddComponent<RoomZone>();
            zone.roomId = id;
            zone.displayName = title;
            zone.subtitle = subtitle;
            zone.titleAccent = accent;
            zone.size = new Vector3(6f, 4f, 5.8f);
            zone.center = new Vector3(0f, 2f, 0f);
            zone.abilities = abilities;
            zone.countsForFinalArea = countsForFinal;
            return zone;
        }

        // north-west, x from -6 to 0: carry the crate onto the pad (glove only)
        static void BuildGloveRoom(Transform room, Material floor, Material wall, Material furniture, Material crate)
        {
            Transform group = Group("GloveRoom", room);
            Box("Floor_GloveRoom", group, new Vector3(6.2f, 0.2f, 6.2f), new Vector3(-3f, -0.1f, 9.1f), floor);
            Box("Wall_GloveRoom_W", group, new Vector3(0.2f, 3.2f, 6.2f), new Vector3(-6.1f, 1.6f, 9.1f), wall);
            Box("Wall_GloveRoom_E", group, new Vector3(0.2f, 3.2f, 6.2f), new Vector3(0.1f, 1.6f, 9.1f), wall);
            Box("Wall_GloveRoom_N", group, new Vector3(6.4f, 3.2f, 0.2f), new Vector3(-3f, 1.6f, 12.2f), wall);

            // you start here without the glove: decorate it with the pencil, pick it up, then solve the switch puzzle
            // (see InkFlipSceneBuilder.GloveRoom.cs). The glove stays locked everywhere until then.
            RoomZone zone = MakeZone(room, "glove_room", "Glove Room", "Decorate your glove, then put it to work",
                                     new Color(1f, 0.82f, 0.1f), new Vector3(-3f, 0f, 9.1f), Ability.Pencil | Ability.Glove);
            BuildGloveRoomContents(group, zone, wall, furniture, crate);
        }

        // north-east, x from 0 to 6: blaster and brush, a big blank wall to cover
        static void BuildInkRoom(Transform room, Material floor, Material wall, Material furniture)
        {
            Transform group = Group("InkRoom", room);
            Box("Floor_InkRoom", group, new Vector3(6.1f, 0.2f, 6.2f), new Vector3(3.1f, -0.1f, 9.1f), floor);
            Box("Wall_InkRoom_E", group, new Vector3(0.2f, 3.2f, 6.2f), new Vector3(6.1f, 1.6f, 9.1f), wall);
            Box("Wall_InkRoom_N", group, new Vector3(6.2f, 3.2f, 0.2f), new Vector3(3.1f, 1.6f, 12.2f), wall);
            Box("PaintTarget", group, new Vector3(4f, 2.2f, 0.15f), new Vector3(3.1f, 1.3f, 11.95f), furniture);

            RoomZone zone = MakeZone(room, "ink_room", "Ink Room", "Cover the wall with ink",
                                     new Color(0.2f, 0.55f, 1f), new Vector3(3.1f, 0f, 9.1f), Ability.InkBlaster | Ability.PaintBrush);
            var objective = zone.gameObject.AddComponent<InkCoverageObjective>();
            objective.title = "Cover the wall";
            objective.description = "Cover the walls with ink.";
            objective.zone = zone;
            zone.objective = objective;
        }

        // south-west, x from -6 to 0: the Lines room (race, colouring-book walls, lights out) - see InkFlipSceneBuilder.LineRoom.cs
        static void BuildDrawingRoom(Transform room, Material floor, Material wall, Material furniture)
        {
            Transform group = Group("DrawingRoom", room);
            Box("Floor_DrawingRoom", group, new Vector3(6.2f, 0.2f, 6.2f), new Vector3(-3f, -0.1f, -9.1f), floor);
            Box("Wall_DrawingRoom_W", group, new Vector3(0.2f, 3.2f, 6.2f), new Vector3(-6.1f, 1.6f, -9.1f), wall);
            Box("Wall_DrawingRoom_E", group, new Vector3(0.2f, 3.2f, 6.2f), new Vector3(0.1f, 1.6f, -9.1f), wall);
            Box("Wall_DrawingRoom_S", group, new Vector3(6.4f, 3.2f, 0.2f), new Vector3(-3f, 1.6f, -12.2f), wall);

            RoomZone zone = MakeZone(room, "drawing_room", "Line Room", "Power the room, then race your nanobots",
                                     new Color(1f, 0.55f, 0.15f), new Vector3(-3f, 0f, -9.1f), Ability.Pencil | Ability.PaintBrush | Ability.Glove);
            BuildLineRoomContents(group, zone, floor, wall, furniture);
        }

        // east, behind the elevator: the planetarium, the final challenge (see InkFlipSceneBuilder.Planetarium.cs)
        static void BuildFinalArea(Transform room, Material floor, Material wall, Material furniture, Material crate)
        {
            BuildPlanetarium(room);

            // the elevator in the hall's east wall: opens once every room that counts is finished
            BuildElevator(room, furniture, wall);
        }

        static Transform Group(string name, Transform parent)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        // a box with its size baked into the mesh (instead of transform scale), so the
        // generated lightmap UVs give every face paint resolution proportional to its area
        static void Box(string name, Transform parent, Vector3 size, Vector3 position, Material material, Quaternion? rotation = null)
        {
            string key = $"Box_{size.x:0.###}x{size.y:0.###}x{size.z:0.###}";
            Mesh mesh = LoadOrCreateMesh(key, () => BuildBoxMesh(size));
            CreatePaintable(name, parent, mesh, position, rotation ?? Quaternion.identity, material);
        }

        static void Primitive(string name, Transform parent, string builtinMesh, Vector3 size, Vector3 position, Material material)
        {
            string key = $"{Path.GetFileNameWithoutExtension(builtinMesh)}_{size.x:0.###}x{size.y:0.###}x{size.z:0.###}";
            Mesh mesh = LoadOrCreateMesh(key, () =>
            {
                // built-in cylinder is 2 units tall, sphere 1 unit across - normalise to a unit box first
                Mesh source = Resources.GetBuiltinResource<Mesh>(builtinMesh);
                Vector3 sourceSize = source.bounds.size;
                Vector3 scale = new Vector3(size.x / sourceSize.x, size.y / sourceSize.y, size.z / sourceSize.z);

                Mesh copy = Object.Instantiate(source);
                Vector3[] vertices = copy.vertices;
                for (int i = 0; i < vertices.Length; i++) vertices[i] = Vector3.Scale(vertices[i], scale);
                copy.vertices = vertices;
                copy.RecalculateBounds();
                copy.RecalculateTangents();
                return copy;
            });
            CreatePaintable(name, parent, mesh, position, Quaternion.identity, material);
        }

        static void CreatePaintable(string name, Transform parent, Mesh mesh, Vector3 position, Quaternion rotation, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            go.AddComponent<Paintable>();
        }

        static Renderer ViewModelPart(string name, Transform parent, PrimitiveType type, Vector3 scale, Vector3 position, Quaternion rotation, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.layer = IgnoreRaycastLayer;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = rotation;
            part.transform.localScale = scale;
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return renderer;
        }

        static Mesh BuildBoxMesh(Vector3 size)
        {
            Vector3 half = size * 0.5f;
            Vector3[] normals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };

            var vertices = new Vector3[24];
            var meshNormals = new Vector3[24];
            var uvs = new Vector2[24];
            var triangles = new int[36];

            for (int face = 0; face < 6; face++)
            {
                Vector3 n = normals[face];
                Vector3 u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.Cross(Vector3.up, n);
                Vector3 v = Vector3.Cross(n, u);

                Vector2[] corners = { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) };
                for (int c = 0; c < 4; c++)
                {
                    Vector3 p = Vector3.Scale(n + u * corners[c].x + v * corners[c].y, half);
                    int index = face * 4 + c;
                    vertices[index] = p;
                    meshNormals[index] = n;
                    uvs[index] = new Vector2(Vector3.Dot(p, u), Vector3.Dot(p, v)); // metres, so base textures tile at a real-world scale
                }

                // Unity treats clockwise (seen from outside) as front-facing
                int b = face * 4;
                Vector3 winding = Vector3.Cross(vertices[b + 1] - vertices[b], vertices[b + 2] - vertices[b]);
                bool outward = Vector3.Dot(winding, n) > 0f;
                int t = face * 6;
                if (outward)
                {
                    triangles[t] = b; triangles[t + 1] = b + 1; triangles[t + 2] = b + 2;
                    triangles[t + 3] = b; triangles[t + 4] = b + 2; triangles[t + 5] = b + 3;
                }
                else
                {
                    triangles[t] = b; triangles[t + 1] = b + 2; triangles[t + 2] = b + 1;
                    triangles[t + 3] = b; triangles[t + 4] = b + 3; triangles[t + 5] = b + 2;
                }
            }

            var mesh = new Mesh
            {
                vertices = vertices,
                normals = meshNormals,
                uv = uvs,
                triangles = triangles,
            };
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        static Mesh LoadOrCreateMesh(string key, System.Func<Mesh> build)
        {
            string path = $"{MeshFolder}/{key}.asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;

            Mesh mesh = build();
            mesh.name = key;

            // lightmap-style UV2 with margins between islands - this is the paint layout
            UnwrapParam.SetDefaults(out UnwrapParam unwrap);
            unwrap.packMargin = 8f / 1024f;
            Unwrapping.GenerateSecondaryUVSet(mesh, unwrap);

            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static Material CreateMaterial(string name, Shader shader, string hexColor, float smoothness)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            ColorUtility.TryParseHtmlString(hexColor, out Color color);
            material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
