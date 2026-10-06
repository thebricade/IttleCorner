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
    public static class InkFlipSceneBuilder
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
            Material wall = CreateMaterial("Wall", paintableShader, "#D8D3CA", 0.15f);
            Material floor = CreateMaterial("Floor", paintableShader, "#A9825A", 0.3f);
            Material furniture = CreateMaterial("Furniture", paintableShader, "#EEE8DF", 0.25f);
            Material crate = CreateMaterial("Crate", paintableShader, "#9C7350", 0.1f);

            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            Material gunBody = CreateMaterial("GunBody", litShader, "#34343A", 0.6f);
            Material gunInk = CreateMaterial("GunInk", litShader, "#FF3D8B", 0.85f);
            Material projectile = CreateMaterial("InkBlob", Shader.Find("Universal Render Pipeline/Unlit"), "#FF3D8B", 0f);

            SetupLighting();

            var room = new GameObject("Room").transform;

            // shell: open-topped so the sun lights the inside
            Box("Floor", room, new Vector3(12f, 0.2f, 12f), new Vector3(0f, -0.1f, 0f), floor);
            Box("Wall_North", room, new Vector3(12.4f, 3.2f, 0.2f), new Vector3(0f, 1.6f, 6.1f), wall);
            Box("Wall_South", room, new Vector3(12.4f, 3.2f, 0.2f), new Vector3(0f, 1.6f, -6.1f), wall);
            Box("Wall_East", room, new Vector3(0.2f, 3.2f, 12f), new Vector3(6.1f, 1.6f, 0f), wall);
            Box("Wall_West", room, new Vector3(0.2f, 3.2f, 12f), new Vector3(-6.1f, 1.6f, 0f), wall);

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
            Box("Bookshelf", room, new Vector3(0.4f, 2.2f, 2.2f), new Vector3(5.7f, 1.1f, -1.5f), furniture);
            Primitive("Pillar", room, "Cylinder.fbx", new Vector3(0.6f, 3.2f, 0.6f), new Vector3(-2.5f, 1.6f, -1.5f), wall);

            // crate stack + a ramp to climb
            Box("Crate_A", room, Vector3.one * 0.8f, new Vector3(-4.8f, 0.4f, -4.8f), crate);
            Box("Crate_B", room, Vector3.one * 0.8f, new Vector3(-3.9f, 0.4f, -4.9f), crate);
            Box("Crate_C", room, Vector3.one * 0.8f, new Vector3(-4.4f, 1.2f, -4.8f), crate, Quaternion.Euler(0f, 20f, 0f));
            Box("Ramp", room, new Vector3(2f, 0.2f, 4f), new Vector3(3.2f, 0.5f, -3.6f), furniture, Quaternion.Euler(-14f, 0f, 0f));

            BuildManagers(projectile, gunBody, gunInk);

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

            // selection outline + draw mode
            var selectionObject = new GameObject("Selection");
            var highlighter = selectionObject.AddComponent<OutlineHighlighter>();
            highlighter.outlineShader = Shader.Find("InkFlip/Outline");

            var switcher = player.AddComponent<ToolSwitcher>();

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

            switcher.tools = new[]
            {
                new PaintTool { name = "Ink Blaster", controls = "LMB: blast   RMB: roller", root = gunRoot.gameObject },
                new PaintTool { name = "Paint Brush", controls = "Hold LMB and sweep", root = brushRoot.gameObject },
                new PaintTool { name = "Pencil", controls = "Look at an object, LMB: draw on it", root = pencilRoot.gameObject },
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
