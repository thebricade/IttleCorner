using UnityEditor;
using UnityEngine;

namespace InkFlip.EditorTools
{
    // The Cat Room (south-east of the hall, behind a door that opens once you have earned the glove):
    // a big grassy room with a tree, and a cat stuck on a branch. The way up is to build steps with the glove's
    // block power. Scripts: ShapeTool (on the glove), CatInTree, CatRoomObjective, AbilityDoor.
    public static partial class InkFlipSceneBuilder
    {
        const float CatRoomNorth = -6.1f;
        const float CatRoomSouth = -18.2f;

        static void BuildCatRoom(Transform room, Material floor, Material wall, Material furniture)
        {
            Transform group = Group("CatRoom", room);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Shader paintable = Shader.Find("InkFlip/PaintableLit");

            float length = CatRoomNorth - CatRoomSouth;          // 12.1
            float centreZ = (CatRoomNorth + CatRoomSouth) * 0.5f; // -12.15

            Material grass = CreateMaterial("CatGrass", paintable, "#79B866", 0.1f);
            Material bark = CreateMaterial("TreeBark", paintable, "#7A5236", 0.15f);
            Material leaves = CreateMaterial("TreeLeaves", paintable, "#4FA24B", 0.15f);
            Material doorMaterial = CreateMaterial("LockedDoor", paintable, "#C0443E", 0.3f);
            Material catFur = CreateMaterial("CatFur", lit, "#F29A3C", 0.2f);
            Material catDark = CreateMaterial("CatDark", lit, "#1A1A22", 0.2f);

            // the room: 6 m wide and twice as deep as the others
            Box("Floor_CatRoom", group, new Vector3(6.1f, 0.2f, length), new Vector3(3.1f, -0.1f, centreZ), floor);
            Box("Grass", group, new Vector3(5.7f, 0.02f, length - 0.4f), new Vector3(3.1f, 0.011f, centreZ), grass);
            Box("Wall_CatRoom_E", group, new Vector3(0.2f, 3.2f, length), new Vector3(6.1f, 1.6f, centreZ), wall);
            Box("Wall_CatRoom_S", group, new Vector3(6.4f, 3.2f, 0.2f), new Vector3(3.1f, 1.6f, CatRoomSouth), wall);
            // the west wall for the part past the Line Room (that room's own wall covers the first 6 m)
            Box("Wall_CatRoom_W", group, new Vector3(0.2f, 3.2f, 6.0f), new Vector3(0.1f, 1.6f, -15.3f), wall);

            // the door in the hall's south wall: shut until the glove is earned
            Box("CatRoomDoor", group, new Vector3(2f, 2.4f, 0.16f), new Vector3(3f, 1.2f, -6.1f), doorMaterial);
            var door = group.Find("CatRoomDoor").gameObject.AddComponent<AbilityDoor>();
            door.requires = Ability.Glove;
            door.lockedText = "Locked\nEarn the glove first";

            RoomZone zone = MakeZone(room, "cat_room", "Cat Rescue", "A cat is stuck up the tree - build a way up!",
                                     new Color(1f, 0.6f, 0.2f), new Vector3(3.1f, 0f, centreZ), Ability.Glove);
            zone.size = new Vector3(6f, 8f, length - 0.2f);
            zone.center = new Vector3(0f, 4f, 0f);

            // ---- the tree, with a branch sticking out to the east ----
            Vector3 trunk = new Vector3(2.6f, 0f, -15.4f);
            Primitive("TreeTrunk", group, "Cylinder.fbx", new Vector3(0.9f, 5.6f, 0.9f), new Vector3(trunk.x, 2.8f, trunk.z), bark);
            Box("Branch", group, new Vector3(2.4f, 0.2f, 0.26f), new Vector3(trunk.x + 1.3f, 2.45f, trunk.z), bark);
            Primitive("Leaves_Top", group, "Sphere.fbx", new Vector3(3.8f, 3.0f, 3.8f), new Vector3(trunk.x, 5.7f, trunk.z), leaves);
            Primitive("Leaves_W", group, "Sphere.fbx", new Vector3(2.4f, 2.0f, 2.4f), new Vector3(trunk.x - 0.9f, 4.7f, trunk.z - 0.6f), leaves);
            Primitive("Leaves_E", group, "Sphere.fbx", new Vector3(2.4f, 2.0f, 2.4f), new Vector3(trunk.x + 1.1f, 4.9f, trunk.z + 0.5f), leaves);
            Primitive("Bush_A", group, "Sphere.fbx", new Vector3(1.1f, 0.8f, 1.1f), new Vector3(0.9f, 0.3f, -17.3f), leaves);
            Primitive("Bush_B", group, "Sphere.fbx", new Vector3(1.4f, 1.0f, 1.2f), new Vector3(5.3f, 0.4f, -17.3f), leaves);

            // ---- the cat, sitting at the end of the branch ----
            Transform cat = BuildCat(group, new Vector3(trunk.x + 2.1f, 2.55f, trunk.z), catFur, catDark);

            var landSpot = new GameObject("CatLandingSpot").transform;
            landSpot.SetParent(group, false);
            landSpot.position = new Vector3(3.9f, 0f, -11.2f);

            // the person who lost the cat
            PlaceTalker(group, "Pippa", "Assets/Prototype/Messy Image/buddy_03.png", new Vector3(1.4f, 0f, -8.3f), 1.1f);

            var rescue = new GameObject("CatRescue");
            rescue.transform.SetParent(group, false);
            var catScript = rescue.AddComponent<CatInTree>();
            catScript.zone = zone;
            catScript.cat = cat;
            catScript.landSpot = landSpot;

            var objective = zone.gameObject.AddComponent<CatRoomObjective>();
            objective.title = "Rescue the cat";
            objective.description = "Build a way up to the cat with the glove's block power, then bring it down.";
            objective.cat = catScript;
            objective.zone = zone;
            zone.objective = objective;
        }

        // a chunky little cat out of primitives; it faces +Z, origin at its feet
        static Transform BuildCat(Transform parent, Vector3 position, Material fur, Material dark)
        {
            var root = new GameObject("Cat").transform;
            root.SetParent(parent, false);
            root.position = position;
            root.rotation = Quaternion.LookRotation(new Vector3(-0.6f, 0f, 0.8f), Vector3.up);

            CatPart(root, "Body", PrimitiveType.Sphere, new Vector3(0.34f, 0.28f, 0.52f), new Vector3(0f, 0.17f, 0f), Quaternion.identity, fur);
            CatPart(root, "Head", PrimitiveType.Sphere, new Vector3(0.26f, 0.24f, 0.24f), new Vector3(0f, 0.34f, 0.3f), Quaternion.identity, fur);
            CatPart(root, "EarL", PrimitiveType.Cube, new Vector3(0.07f, 0.09f, 0.03f), new Vector3(-0.08f, 0.5f, 0.3f), Quaternion.Euler(0f, 0f, 20f), fur);
            CatPart(root, "EarR", PrimitiveType.Cube, new Vector3(0.07f, 0.09f, 0.03f), new Vector3(0.08f, 0.5f, 0.3f), Quaternion.Euler(0f, 0f, -20f), fur);
            CatPart(root, "EyeL", PrimitiveType.Sphere, Vector3.one * 0.045f, new Vector3(-0.05f, 0.37f, 0.41f), Quaternion.identity, dark);
            CatPart(root, "EyeR", PrimitiveType.Sphere, Vector3.one * 0.045f, new Vector3(0.05f, 0.37f, 0.41f), Quaternion.identity, dark);
            CatPart(root, "Tail", PrimitiveType.Cylinder, new Vector3(0.05f, 0.2f, 0.05f), new Vector3(0f, 0.3f, -0.36f), Quaternion.Euler(60f, 0f, 0f), fur);
            return root;
        }

        static void CatPart(Transform parent, string name, PrimitiveType type, Vector3 scale, Vector3 localPosition, Quaternion rotation, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = rotation;
            part.transform.localScale = scale;
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
