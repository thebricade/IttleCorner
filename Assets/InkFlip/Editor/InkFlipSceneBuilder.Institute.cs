using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace InkFlip.EditorTools
{
    // The "hero institute" look: cool white panelled walls, dark tiled floors, navy banners with a lightning crest,
    // a floor emblem and a few low-poly props from the Kenney Space Station Kit (Assets/ThirdParty, see CREDITS.txt).
    //
    // The walls and floors stay the paintable boxes (so ink still sticks to them); the look comes from their colour and
    // a generated tile texture. Everything from the model kit is decoration that ink passes over.
    public static partial class InkFlipSceneBuilder
    {
        const string StationFolder = "Assets/ThirdParty/Kenney_SpaceStation/Models/FBX format/";
        const string StationColormap = StationFolder + "Textures/colormap.png";

        static readonly Color InstituteNavy = new Color32(0x1D, 0x2B, 0x4F, 255);
        static readonly Color InstituteGold = new Color32(0xFF, 0xC9, 0x3C, 255);

        // ---------- look of the shared wall / floor materials ----------

        static void ApplyInstituteLook(Material wall, Material floor)
        {
            EnsureFolder(TextureFolder);
            Texture2D wallTiles = LoadOrCreateTileTexture("InstituteWallTiles", new Color(0.97f, 0.98f, 1f), new Color(0.78f, 0.82f, 0.88f), true);
            Texture2D floorTiles = LoadOrCreateTileTexture("InstituteFloorTiles", new Color(0.95f, 0.95f, 0.97f), new Color(0.52f, 0.56f, 0.63f), false);

            foreach (var pair in new[] { (wall, wallTiles), (floor, floorTiles) })
            {
                if (pair.Item2 == null) continue;
                pair.Item1.SetTexture("_BaseMap", pair.Item2);
                pair.Item1.SetTextureScale("_BaseMap", Vector2.one);
                EditorUtility.SetDirty(pair.Item1);
            }
        }

        // a one-metre tile (the box UVs are in metres): flat colour, a darker seam on two edges, rivets for wall panels
        static Texture2D LoadOrCreateTileTexture(string name, Color face, Color seam, bool rivets)
        {
            string path = $"{TextureFolder}/{name}.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            const int Size = 128;
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    Color c = face;
                    bool edge = x < 2 || y < 2;
                    bool softEdge = x < 4 || y < 4;
                    if (edge) c = seam;
                    else if (softEdge) c = Color.Lerp(face, seam, 0.25f);

                    if (rivets)
                    {
                        // four small rivets near the corners of the panel
                        foreach (Vector2 r in new[] { new Vector2(12, 12), new Vector2(116, 12), new Vector2(12, 116), new Vector2(116, 116) })
                        {
                            if ((new Vector2(x, y) - r).sqrMagnitude < 6f) c = Color.Lerp(face, seam, 0.8f);
                        }
                    }
                    else
                    {
                        // every other tile a touch darker would need two tiles; a faint diagonal sheen keeps big floors from looking flat
                        c *= 1f - 0.04f * ((x + y) / (float)(2 * Size));
                    }

                    c.a = 1f;
                    texture.SetPixel(x, y, c);
                }
            }
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.mipmapEnabled = true;
                importer.anisoLevel = 8;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ---------- crest / banner textures ----------

        // the lightning bolt, as a polygon in 0..1 space (y up)
        static readonly Vector2[] Bolt =
        {
            new Vector2(0.60f, 0.95f), new Vector2(0.26f, 0.48f), new Vector2(0.46f, 0.48f),
            new Vector2(0.36f, 0.05f), new Vector2(0.76f, 0.58f), new Vector2(0.54f, 0.58f),
        };

        static bool InPolygon(Vector2[] polygon, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                if ((polygon[i].y > p.y) != (polygon[j].y > p.y) &&
                    p.x < (polygon[j].x - polygon[i].x) * (p.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x)
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        // round crest: navy disc, gold ring, gold bolt; transparent outside (used with the cut-out material)
        static Texture2D LoadOrCreateEmblemTexture()
        {
            string path = TextureFolder + "/InstituteEmblem.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            const int Size = 256;
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    var p = new Vector2((x + 0.5f) / Size, (y + 0.5f) / Size);
                    float r = (p - new Vector2(0.5f, 0.5f)).magnitude;
                    Color c = new Color(0f, 0f, 0f, 0f);
                    if (r < 0.5f)
                    {
                        c = InstituteNavy;
                        if (r > 0.43f && r < 0.47f) c = InstituteGold;
                        else if (r >= 0.47f) c = Color.white;
                        else if (InPolygon(Bolt, new Vector2(0.5f + (p.x - 0.5f) * 1.5f, 0.5f + (p.y - 0.5f) * 1.5f))) c = InstituteGold;
                    }
                    texture.SetPixel(x, y, c);
                }
            }
            texture.Apply();
            return SaveTexture(path, texture, TextureWrapMode.Clamp, true);
        }

        // tall banner: navy cloth, gold bolt, gold stripes top and bottom
        static Texture2D LoadOrCreateBannerTexture()
        {
            string path = TextureFolder + "/InstituteBanner.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            const int W = 128, H = 256;
            var texture = new Texture2D(W, H, TextureFormat.RGBA32, false);
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f) / W, v = (y + 0.5f) / H;
                    Color c = InstituteNavy;
                    if (v < 0.04f || v > 0.96f || u < 0.04f || u > 0.96f) c = InstituteGold;
                    // the bolt, squashed to fit the tall cloth
                    Vector2 q = new Vector2(0.5f + (u - 0.5f) * 1.6f, 0.5f + (v - 0.5f) * 1.2f);
                    if (InPolygon(Bolt, q)) c = InstituteGold;
                    texture.SetPixel(x, y, c);
                }
            }
            texture.Apply();
            return SaveTexture(path, texture, TextureWrapMode.Clamp, false);
        }

        static Texture2D SaveTexture(string path, Texture2D texture, TextureWrapMode wrap, bool alpha)
        {
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.wrapMode = wrap;
                importer.alphaIsTransparency = alpha;
                importer.mipmapEnabled = true;
                importer.anisoLevel = 4;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ---------- placing kit models ----------

        // Puts a model from the kit in the scene, scaled so it is targetHeight tall and standing on `ground` (its centre over that
        // point), turned by yaw degrees. The kit's materials are swapped for one URP Lit material that uses the kit's colour atlas.
        static GameObject PlaceModel(string file, Transform parent, string name, Vector3 ground, float yaw, float targetHeight, Material kitMaterial, bool solid = true)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StationFolder + file + ".fbx");
            if (prefab == null)
            {
                Debug.LogWarning("InkFlip: model not found: " + StationFolder + file + ".fbx (is Assets/ThirdParty/Kenney_SpaceStation there?)");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localScale = Vector3.one;

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Object.DestroyImmediate(instance);
                return null;
            }

            Bounds bounds = renderers[0].bounds;
            foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
            float scale = bounds.size.y > 0.0001f ? targetHeight / bounds.size.y : 1f;

            foreach (Renderer r in renderers)
            {
                var materials = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++) materials[i] = kitMaterial;
                r.sharedMaterials = materials;
            }

            // the pivot is wherever the artist left it, so move the model until the middle of its footprint is over `ground`
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 footprintCentre = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * scale;
            instance.transform.localScale = Vector3.one * scale;
            instance.transform.SetPositionAndRotation(ground - rotation * footprintCentre, rotation);

            if (solid)
            {
                var box = instance.AddComponent<BoxCollider>();
                box.center = bounds.center;
                box.size = bounds.size;
            }
            return instance;
        }

        static float TopOf(GameObject model)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
            return bounds.max.y;
        }

        // ---------- the entry hall ----------

        // A flat picture on a wall: `normal` is the direction the picture faces (into the room).
        static void WallBanner(Transform parent, string name, Vector3 wallPoint, Vector3 normal, Vector2 size, Material material)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(parent, false);
            quad.transform.SetPositionAndRotation(wallPoint + normal * 0.03f, Quaternion.LookRotation(-normal));
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        // Banners, the floor crest, and kit props for the entry hall (x from -6 to 12, z from -6 to 6).
        static void BuildInstituteHall(Transform room, Material furniture)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Transform hall = Group("InstituteDecor", room);

            Material bannerMaterial = CreateMaterial("InstituteBanner", lit, "#FFFFFF", 0.1f);
            Texture2D bannerTexture = LoadOrCreateBannerTexture();
            bannerMaterial.SetTexture("_BaseMap", bannerTexture);
            bannerMaterial.mainTexture = bannerTexture;

            Material crestMaterial = CreateMaterial("InstituteCrest", lit, "#FFFFFF", 0.15f);
            crestMaterial.SetFloat("_AlphaClip", 1f);
            crestMaterial.SetFloat("_Cutoff", 0.5f);
            crestMaterial.SetFloat("_Cull", 0f);
            crestMaterial.EnableKeyword("_ALPHATEST_ON");
            crestMaterial.SetOverrideTag("RenderType", "TransparentCutout");
            crestMaterial.renderQueue = (int)RenderQueue.AlphaTest;
            Texture2D crestTexture = LoadOrCreateEmblemTexture();
            crestMaterial.SetTexture("_BaseMap", crestTexture);
            crestMaterial.mainTexture = crestTexture;

            Material kit = CreateMaterial("StationKit", lit, "#FFFFFF", 0.25f);
            var colormap = AssetDatabase.LoadAssetAtPath<Texture2D>(StationColormap);
            if (colormap != null)
            {
                kit.SetTexture("_BaseMap", colormap);
                kit.mainTexture = colormap;
            }
            EditorUtility.SetDirty(bannerMaterial);
            EditorUtility.SetDirty(crestMaterial);
            EditorUtility.SetDirty(kit);

            // banners on the walls between the doorways (inner faces: north z=6, south z=-6, east x=12)
            var bannerSize = new Vector2(0.9f, 1.8f);
            foreach (float x in new[] { -5.1f, 0f, 8.8f, 11.0f })
            {
                WallBanner(hall, "Banner_N", new Vector3(x, 2.0f, 6f), Vector3.back, bannerSize, bannerMaterial);
            }
            foreach (float x in new[] { -5.1f, 0f, 5.1f, 11.55f })
            {
                WallBanner(hall, "Banner_S", new Vector3(x, 2.0f, -6f), Vector3.forward, bannerSize, bannerMaterial);
            }
            foreach (float z in new[] { -3.2f, 3.2f })
            {
                WallBanner(hall, "Banner_E", new Vector3(12f, 2.0f, z), Vector3.left, bannerSize, bannerMaterial);
            }

            // the crest on the floor in front of the elevator
            GameObject crest = GameObject.CreatePrimitive(PrimitiveType.Quad);
            crest.name = "FloorCrest";
            Object.DestroyImmediate(crest.GetComponent<Collider>());
            crest.transform.SetParent(hall, false);
            crest.transform.SetPositionAndRotation(new Vector3(8.5f, 0.012f, 0f), Quaternion.Euler(90f, 0f, 0f));
            crest.transform.localScale = new Vector3(4.2f, 4.2f, 1f);
            var crestRenderer = crest.GetComponent<MeshRenderer>();
            crestRenderer.sharedMaterial = crestMaterial;
            crestRenderer.shadowCastingMode = ShadowCastingMode.Off;

            // kit props: a hologram table on the crest, a row of lockers, a reception desk
            PlaceModel("table-display-planet", hall, "HologramTable", new Vector3(8.5f, 0f, 0f), 0f, 1.2f, kit);

            for (int i = 0; i < 3; i++) // (kept west of the Hand Mix doorway at x 9.1 - 11.1)
            {
                PlaceModel("container-tall", hall, "Locker_" + i, new Vector3(6.7f + i * 0.9f, 0f, -5.5f), 0f, 2.1f, kit);
            }

            GameObject desk = PlaceModel("table-large", hall, "ReceptionDesk", new Vector3(8.5f, 0f, 4.9f), 0f, 0.95f, kit);
            if (desk != null)
            {
                PlaceModel("computer-wide", hall, "ReceptionComputer", new Vector3(8.5f, TopOf(desk), 4.9f), 180f, 0.55f, kit, solid: false);
            }
            PlaceModel("chair-cushion", hall, "ReceptionChair", new Vector3(8.5f, 0f, 5.55f), 180f, 1.0f, kit);
        }

        // ---------- the elevator (the unlockable door) ----------

        // The east wall of the hall (x = 12) has the elevator: a frame, two doors that slide apart, and a sign. The FinalGate
        // on the left door counts finished rooms and opens both.
        static void BuildElevator(Transform room, Material furniture, Material wall)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            Material steel = CreateMaterial("InstituteSteel", lit, "#8E98A6", 0.6f);
            steel.SetFloat("_Metallic", 0.6f);
            Material doorMaterial = CreateMaterial("ElevatorDoor", lit, "#D5DBE3", 0.75f);
            Material navy = CreateMaterial("InstituteNavyTrim", lit, "#1D2B4F", 0.4f);
            Material gold = CreateMaterial("InstituteGoldGlow", unlit, "#FFC93C", 0f);
            EditorUtility.SetDirty(steel);

            Transform group = Group("Elevator", room);

            // wall around the opening (z from -1.2 to 1.2), with a lintel over it
            Box("Wall_East_S", group, new Vector3(0.2f, 3.2f, 5f), new Vector3(12.1f, 1.6f, -3.7f), wall);
            Box("Wall_East_N", group, new Vector3(0.2f, 3.2f, 5f), new Vector3(12.1f, 1.6f, 3.7f), wall);
            Box("Lintel_East", group, new Vector3(0.2f, 0.6f, 2.4f), new Vector3(12.1f, 2.9f, 0f), wall);

            // frame on the hall side
            Box("Jamb_S", group, new Vector3(0.35f, 2.8f, 0.3f), new Vector3(11.92f, 1.4f, -1.35f), steel);
            Box("Jamb_N", group, new Vector3(0.35f, 2.8f, 0.3f), new Vector3(11.92f, 1.4f, 1.35f), steel);
            Box("Header", group, new Vector3(0.35f, 0.5f, 3.0f), new Vector3(11.92f, 2.95f, 0f), steel);
            Box("HeaderBand", group, new Vector3(0.05f, 0.1f, 2.4f), new Vector3(11.72f, 2.72f, 0f), navy);
            Box("Threshold", group, new Vector3(0.9f, 0.02f, 2.4f), new Vector3(11.7f, 0.01f, 0f), steel);

            // call panel beside the door, with a gold button
            Box("CallPanel", group, new Vector3(0.1f, 0.4f, 0.2f), new Vector3(11.93f, 1.2f, 1.75f), navy);
            Box("CallButton", group, new Vector3(0.04f, 0.08f, 0.08f), new Vector3(11.86f, 1.25f, 1.75f), gold);

            // the doors: two leaves, the south one carries the FinalGate
            GameObject left = GameObject.CreatePrimitive(PrimitiveType.Cube);
            left.name = "ElevatorDoor_S";
            left.transform.SetParent(group, false);
            left.transform.position = new Vector3(12.1f, 1.3f, -0.6f);
            left.transform.localScale = new Vector3(0.3f, 2.6f, 1.2f);
            left.GetComponent<Renderer>().sharedMaterial = doorMaterial;

            GameObject right = GameObject.CreatePrimitive(PrimitiveType.Cube);
            right.name = "ElevatorDoor_N";
            right.transform.SetParent(group, false);
            right.transform.position = new Vector3(12.1f, 1.3f, 0.6f);
            right.transform.localScale = new Vector3(0.3f, 2.6f, 1.2f);
            right.GetComponent<Renderer>().sharedMaterial = doorMaterial;

            var gate = left.AddComponent<FinalGate>();
            gate.tintRenderers = new[] { left.GetComponent<Renderer>(), right.GetComponent<Renderer>() };
            gate.blockers = new[] { left.GetComponent<Collider>(), right.GetComponent<Collider>() };
            gate.openOffset = new Vector3(0f, 0f, -1.25f);
            gate.secondLeaf = right.transform;
            gate.secondOpenOffset = new Vector3(0f, 0f, 1.25f);
            gate.labelWorldOffset = new Vector3(-0.55f, 1.62f, 0.6f);
            gate.labelYaw = 90f;
            gate.labelSize = 0.04f;
        }
    }
}
