using System;
using UnityEngine;

namespace InkFlip
{
    // The door to the final area. It stays shut, with a sign counting finished rooms, until every required room
    // is complete; then it turns green and slides open.
    //
    // By default every RoomZone that "counts for the final area" is required. List room ids below to require
    // a specific set instead.
    public class FinalGate : MonoBehaviour
    {
        [Tooltip("Leave empty to require every room zone that counts for the final area.")]
        public string[] requiredRoomIds;

        [Header("Door")]
        [Tooltip("Tinted red while locked and green once open.")]
        public Renderer[] tintRenderers;
        [Tooltip("Switched off when the gate opens so the player can walk through.")]
        public Collider[] blockers;
        [Tooltip("How far the gate slides when it opens.")]
        public Vector3 openOffset = new Vector3(0f, 2.6f, 0f);
        public float openSeconds = 1.2f;
        [Tooltip("Optional second door leaf (an elevator has two). It slides by Second Open Offset while this one slides by Open Offset.")]
        public Transform secondLeaf;
        public Vector3 secondOpenOffset;
        public Color lockedColor = new Color(0.85f, 0.25f, 0.25f);
        public Color unlockedColor = new Color(0.3f, 0.8f, 0.4f);

        [Header("Sign")]
        [Tooltip("Where the sign floats, relative to the gate in world directions.")]
        public Vector3 labelWorldOffset = new Vector3(-0.35f, 0.4f, 0f);
        [Tooltip("Turn the sign so it faces the side the player approaches from.")]
        public float labelYaw = 90f;
        public float labelSize = 0.06f;

        [Header("Debug")]
        [Tooltip("Tick this (in the inspector, while playing or in the scene) to open the gate without finishing the rooms.")]
        public bool debugOpen;

        public bool IsUnlocked { get; private set; }

        // fired once, the moment the gate opens
        public static event Action Unlocked;

        TextMesh label;
        Vector3 closedPosition;
        Vector3 secondClosedPosition;
        float openTimer = -1f;
        MaterialPropertyBlock block;

        void Awake()
        {
            closedPosition = transform.position;
            if (secondLeaf != null) secondClosedPosition = secondLeaf.position;
            BuildLabel();
            Tint(lockedColor);
        }

        void OnEnable()
        {
            GameProgress.RoomCompleted += OnRoomCompleted;
            Refresh();
        }

        void OnDisable()
        {
            GameProgress.RoomCompleted -= OnRoomCompleted;
        }

        void OnDestroy()
        {
            if (label != null) Destroy(label.gameObject);
        }

        void OnRoomCompleted(string roomId)
        {
            Refresh();
        }

        void CountRooms(out int done, out int total)
        {
            if (requiredRoomIds != null && requiredRoomIds.Length > 0)
            {
                total = requiredRoomIds.Length;
                done = 0;
                foreach (string id in requiredRoomIds)
                {
                    if (GameProgress.IsComplete(id)) done++;
                }
                return;
            }

            total = RoomZone.CountedTotal;
            done = RoomZone.CountedComplete;
        }

        void Refresh()
        {
            if (IsUnlocked) return;

            CountRooms(out int done, out int total);
            if (label != null) label.text = $"FINAL AREA\n{done} / {total} rooms";

            // with no rooms set up there's nothing to finish, so don't open on its own
            if (total > 0 && done >= total) Unlock();
        }

        void Unlock()
        {
            IsUnlocked = true;
            openTimer = 0f;
            if (blockers != null)
            {
                foreach (Collider blocker in blockers)
                {
                    if (blocker != null) blocker.enabled = false;
                }
            }

            Tint(unlockedColor);
            if (label != null) label.text = "FINAL AREA\nOPEN";
            Unlocked?.Invoke();
        }

        // right-click the component in the inspector
        [ContextMenu("Open now")]
        public void OpenNow()
        {
            if (!IsUnlocked) Unlock();
        }

        // right-click the component while playing: prints which rooms the gate is waiting on
        [ContextMenu("Log status")]
        void LogStatus()
        {
            CountRooms(out int done, out int total);
            Debug.Log($"FinalGate: {done} / {total} rooms done, unlocked = {IsUnlocked}", this);
            foreach (RoomZone zone in FindObjectsByType<RoomZone>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Debug.Log($"  {zone.roomId} ({zone.displayName}): counts = {zone.countsForFinalArea}, complete = {zone.IsComplete}, enabled = {zone.isActiveAndEnabled}", zone);
            }
        }

        void Update()
        {
            if (debugOpen && !IsUnlocked) Unlock();
            if (openTimer < 0f) return;

            openTimer += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(openTimer / Mathf.Max(0.01f, openSeconds)));
            transform.position = closedPosition + openOffset * t;
            if (secondLeaf != null) secondLeaf.position = secondClosedPosition + secondOpenOffset * t;

            // once it's up inside the lintel there's nothing left to see
            if (t >= 1f)
            {
                openTimer = -1f;
                if (tintRenderers != null)
                {
                    foreach (Renderer r in tintRenderers)
                    {
                        if (r != null) r.enabled = false;
                    }
                }
            }
        }

        void Tint(Color color)
        {
            if (tintRenderers == null) return;
            block ??= new MaterialPropertyBlock();
            foreach (Renderer r in tintRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor("_BaseColor", color);
                r.SetPropertyBlock(block);
            }
        }

        // a floating sign made at runtime, so the scene doesn't have to store font references
        void BuildLabel()
        {
            var signObject = new GameObject("GateSign");
            signObject.transform.SetParent(transform.parent, true);
            signObject.transform.position = transform.position + labelWorldOffset;
            signObject.transform.rotation = Quaternion.Euler(0f, labelYaw, 0f);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label = signObject.AddComponent<TextMesh>();
            label.font = font;
            label.fontSize = 64;
            label.characterSize = labelSize;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = Color.white;
            signObject.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }
    }
}
