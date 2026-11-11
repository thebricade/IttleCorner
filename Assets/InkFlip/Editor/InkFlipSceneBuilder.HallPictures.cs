using UnityEditor;
using UnityEngine;

namespace InkFlip.EditorTools
{
    // Two wall things that used to live in the Line Room:
    //  - the colour pictures (draw the outlines and the picture pops out) now hang in the main hall
    //  - the line-tracing power panel, a puzzle on the Line Room wall, built here as a reusable board
    public static partial class InkFlipSceneBuilder
    {
        static void BuildHallPictures(Transform room)
        {
            EnsureFolder(TextureFolder);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Shader paintable = Shader.Find("InkFlip/PaintableLit");

            Material panelMaterial = CreateMaterial("LinePanel", lit, "#FFFFFF", 0.05f);
            Material cutoutMaterial = CreateMaterial("LineCutout", lit, "#FFFFFF", 0.05f);
            cutoutMaterial.SetFloat("_AlphaClip", 1f);
            cutoutMaterial.SetFloat("_Cutoff", 0.5f);
            cutoutMaterial.SetFloat("_Cull", 0f);
            cutoutMaterial.EnableKeyword("_ALPHATEST_ON");
            cutoutMaterial.SetOverrideTag("RenderType", "TransparentCutout");
            cutoutMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            Material frameMaterial = CreateMaterial("PictureFrame", paintable, "#3B3340", 0.2f);

            Transform group = Group("HallPictures", room);
            var panels = new System.Collections.Generic.List<ColoringPanel>();

            // west wall of the hall (inner face x = -6), either side of the Sculpt Room doorway
            float[] westZ = { -4.7f, -2.7f, 2.7f, 4.7f };
            for (int i = 0; i < westZ.Length; i++)
            {
                panels.Add(MakePicturePanel(group, "Picture_W" + i, new Vector3(-6f, 1.65f, westZ[i]), Vector3.right, i,
                                            frameMaterial, BakePictureMaterial(i, lit)));
            }
            // north wall (inner face z = 6), between the Ink Room doorway and the first banner
            panels.Add(MakePicturePanel(group, "Picture_N0", new Vector3(7.0f, 1.65f, 6f), Vector3.back, 4,
                                        frameMaterial, BakePictureMaterial(4, lit)));

            RoomZone hallZone = null;
            Transform zoneTransform = room.Find("Zone_FreePlay");
            if (zoneTransform != null) hallZone = zoneTransform.GetComponent<RoomZone>();
            if (hallZone == null) Debug.LogWarning("InkFlip: couldn't find Zone_FreePlay, so the hall pictures have no zone to work in.");

            var book = group.gameObject.AddComponent<ColoringBook>();
            book.zone = hallZone;
            book.panels = panels.ToArray();
            book.panelMaterial = panelMaterial;
            book.cutoutMaterial = cutoutMaterial;
        }

        // A line-tracing board on a wall. 'intoRoom' points away from the wall. Everything about the puzzle itself is on the
        // LineTracePuzzle component (the Layout text), the board resizes to match it.
        static LineTracePuzzle BuildFlowBoard(Transform parent, Vector3 wallPoint, Vector3 intoRoom, Material frameMaterial, Material screenMaterial, Material lampMaterial)
        {
            var root = new GameObject("FlowPanel").transform;
            root.SetParent(parent, false);
            root.position = wallPoint + intoRoom * 0.05f;
            root.rotation = Quaternion.LookRotation(-intoRoom, Vector3.up); // local +Z points into the wall, the screen faces the room

            GameObject backing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backing.name = "Backing";
            Object.DestroyImmediate(backing.GetComponent<Collider>());
            backing.transform.SetParent(root, false);
            backing.GetComponent<MeshRenderer>().sharedMaterial = frameMaterial;

            GameObject screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
            screen.name = "Screen";
            Object.DestroyImmediate(screen.GetComponent<MeshCollider>());
            screen.transform.SetParent(root, false);
            var box = screen.AddComponent<BoxCollider>();
            box.size = new Vector3(1f, 1f, 0.05f);
            var screenRenderer = screen.GetComponent<MeshRenderer>();
            screenRenderer.sharedMaterial = screenMaterial;
            screenRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            GameObject strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strip.name = "PowerStrip";
            Object.DestroyImmediate(strip.GetComponent<Collider>());
            strip.transform.SetParent(root, false);
            var stripRenderer = strip.GetComponent<MeshRenderer>();
            stripRenderer.sharedMaterial = lampMaterial;
            stripRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var puzzle = root.gameObject.AddComponent<LineTracePuzzle>();
            puzzle.screen = screen.transform;
            puzzle.backing = backing.transform;
            puzzle.powerStrip = strip.transform;
            puzzle.screenRenderer = screenRenderer;
            puzzle.screenCollider = box;
            puzzle.powerLamps = new Renderer[0];
            puzzle.Rebuild();
            return puzzle;
        }
    }
}
