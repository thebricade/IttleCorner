using UnityEngine;
using UnityEngine.InputSystem;

namespace InkFlip
{
    // Finished when enough of the room's paintable walls are covered with ink.
    // Leave 'targets' empty to use every Paintable that sits inside the room's zone.
    public class InkCoverageObjective : RoomObjective
    {
        public RoomZone zone;
        [Tooltip("The surfaces to cover. Empty = every Paintable inside the zone.")]
        public Paintable[] targets;
        [Range(0.05f, 1f)] public float coverageNeeded = 0.15f;
        public float checkEverySeconds = 0.5f;
        [Tooltip("Press this key inside the room to finish it while testing.")]
        public Key testKey = Key.K;

#if UNITY_EDITOR
        // Right-click this component > "Make walls in this room paintable": adds Paintable to every object named
        // 'wall...' inside the zone (the Kenney walls). Undo-able, and saved with the scene.
        [ContextMenu("Make walls in this room paintable")]
        void MakeWallsPaintable()
        {
            if (zone == null) zone = GetComponent<RoomZone>();
            if (zone == null) return;
            Vector3 middle = zone.transform.TransformPoint(zone.center);
            int added = 0;
            foreach (MeshRenderer r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!r.name.StartsWith("wall") || r.GetComponent<Paintable>() != null) continue;
                Vector3 c = r.bounds.center;
                // walls sit right on the edge of the zone, so also test a point a little way inside
                if (!zone.Contains(c) && !zone.Contains(Vector3.MoveTowards(c, middle, 0.4f))) continue;
                UnityEditor.Undo.AddComponent<Paintable>(r.gameObject);
                added++;
            }
            Debug.Log($"InkCoverageObjective: made {added} wall(s) paintable.", this);
        }
#endif

        float coverage;
        float nextCheck;
        Texture2D readback;

        public override float Progress => IsComplete ? 1f : Mathf.Clamp01(coverage / Mathf.Max(0.01f, coverageNeeded));

        void Update()
        {
            if (IsComplete) return;
            if (zone == null) zone = GetComponent<RoomZone>();

            if (testKey != Key.None && zone != null && zone.PlayerInside && Keyboard.current != null && Keyboard.current[testKey].wasPressedThisFrame)
            {
                MarkComplete();
                return;
            }

            if (Time.time < nextCheck) return;
            nextCheck = Time.time + checkEverySeconds;

            if (targets == null || targets.Length == 0) Gather();
            coverage = Measure();

            if (zone != null && zone.PlayerInside && !LineRoomUI.Busy)
            {
                LineRoomUI ui = LineRoomUI.Instance;
                ui.SetStatus($"Ink coverage: {Mathf.RoundToInt(Progress * 100f)}%");
                ui.SetBar(Progress, new Color(0.35f, 0.8f, 1f));
            }

            if (coverage >= coverageNeeded) MarkComplete();
        }

        void Gather()
        {
            if (zone == null) return;
            var found = new System.Collections.Generic.List<Paintable>();
            foreach (Paintable p in FindObjectsByType<Paintable>(FindObjectsSortMode.None))
            {
                if (zone.Contains(p.Renderer.bounds.center)) found.Add(p);
            }
            targets = found.ToArray();
        }

        float Measure()
        {
            float alphaSum = 0f, surface = 0f;
            foreach (Paintable p in targets)
            {
                if (p == null) continue;
                // a target never hit has no texture yet; it still counts as surface to cover
                if (!p.IsAllocated) { surface += 1f; continue; }
                Color32[] mask = Read(p.MaskTexture);
                Color32[] island = Read(p.IslandTexture);
                float a = 0f, s = 0f;
                for (int i = 0; i < mask.Length; i++) { a += mask[i].a; s += island[i].r; }
                if (s < 1f) continue;
                // weight each target equally so a big wall can't be ignored
                alphaSum += Mathf.Clamp01(a / s);
                surface += 1f;
            }
            return surface > 0f ? alphaSum / surface : 0f;
        }

        Color32[] Read(RenderTexture source)
        {
            int size = Mathf.Min(source.width, 128);
            RenderTexture small = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Graphics.Blit(source, small);
            if (readback == null || readback.width != size) readback = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = small;
            readback.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(small);
            return readback.GetPixels32();
        }
    }
}
