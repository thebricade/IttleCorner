using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

namespace InkFlip
{
    // Camera shots for the trailer. Each shot is a Cinemachine camera that slowly orbits a point in a room.
    //
    // Play the game and press a shot's key: the HUD and held tool hide, the player freezes and the camera glides into
    // the orbit. Press the same key again (or Stop Key) to go back to playing. Record it with Window > General > Recorder.
    //
    // Tune a shot in the inspector (the orbit circle and look-at point are drawn in the Scene view when this object is
    // selected). The cameras are made when you press Play; for Timeline, record or animate the "Trailer_<name>" cameras
    // that appear under this object while playing, or use this as the starting point and copy one into the scene.
    public class TrailerShots : MonoBehaviour
    {
        public enum ShotType { Orbit, Fixed }

        [Serializable]
        public class Shot
        {
            public string name = "Shot";
            public Key key = Key.F6;
            [Tooltip("Orbit circles a point. Fixed sits still at Camera Position (and can drift slowly).")]
            public ShotType type = ShotType.Orbit;
            [Header("Orbit")]
            [Tooltip("The point on the floor the camera circles round.")]
            public Vector3 orbitCentre;
            public float radius = 4f;
            [Tooltip("Camera height above the floor.")]
            public float height = 2.5f;
            [Header("Fixed")]
            [Tooltip("Where a Fixed camera sits, in world space.")]
            public Vector3 cameraPosition;
            [Tooltip("A Fixed camera creeps this far each second (metres), for a slow push-in or slide. Zero = locked off.")]
            public Vector3 drift;
            [Header("Both")]
            [Tooltip("What the camera keeps looking at.")]
            public Vector3 lookAt = new Vector3(0f, 1.5f, 0f);
            public float degreesPerSecond = 12f;
            [Tooltip("Where on the circle the shot starts, in degrees.")]
            public float startAngle = 0f;
            public float fieldOfView = 60f;
        }

        public Shot[] shots =
        {
            new Shot { name = "Planetarium", key = Key.F6, orbitCentre = new Vector3(19.2f, 0f, 0f), radius = 5.2f, height = 4.4f,
                       lookAt = new Vector3(19.2f, 2.2f, 0f), degreesPerSecond = 9f, fieldOfView = 65f },
            new Shot { name = "Candy Factory", key = Key.F7, orbitCentre = new Vector3(9.15f, 0f, -9.1f), radius = 2.2f, height = 2.1f,
                       lookAt = new Vector3(9.15f, 1.1f, -9.1f), degreesPerSecond = 14f, fieldOfView = 72f },
            new Shot { name = "Cat Room", key = Key.F8, orbitCentre = new Vector3(3.1f, 0f, -13.4f), radius = 2.8f, height = 2.4f,
                       lookAt = new Vector3(3.6f, 3f, -15.4f), degreesPerSecond = 10f, fieldOfView = 60f },
            // the nanobot race table runs north-south along the Line Room's east wall (x = -0.65, from z = -7.25 down to -11.05)
            new Shot { name = "Race Side", key = Key.LeftBracket, type = ShotType.Fixed, cameraPosition = new Vector3(-3.4f, 1.25f, -9.15f),
                       lookAt = new Vector3(-0.65f, 0.9f, -9.15f), fieldOfView = 50f },
            new Shot { name = "Race Front", key = Key.RightBracket, type = ShotType.Fixed, cameraPosition = new Vector3(-0.65f, 1.15f, -11.8f),
                       lookAt = new Vector3(-0.65f, 0.9f, -8.6f), fieldOfView = 55f },
        };

        [Tooltip("Go back to playing.")]
        public Key stopKey = Key.F9;
        [Tooltip("Hide the HUD and the held tool while a shot is on.")]
        public bool hideHud = true;
        [Tooltip("Freeze the player while a shot is on.")]
        public bool freezePlayer = true;

        CinemachineCamera[] cameras;
        Transform[] pivots;
        int active = -1;
        float shotTime;
        float[] angles;

        FirstPersonController player;
        ToolSwitcher tools;
        Canvas[] hiddenCanvases;
        bool[] toolWasOn;

        void Start()
        {
            player = FindFirstObjectByType<FirstPersonController>();
            tools = FindFirstObjectByType<ToolSwitcher>();

            Camera main = Camera.main;
            if (main != null && main.GetComponent<CinemachineBrain>() == null) main.gameObject.AddComponent<CinemachineBrain>();

            cameras = new CinemachineCamera[shots.Length];
            pivots = new Transform[shots.Length];
            angles = new float[shots.Length];
            for (int i = 0; i < shots.Length; i++) Build(i);
        }

        void Build(int i)
        {
            Shot shot = shots[i];

            var pivot = new GameObject("Trailer_" + shot.name + "_Orbit").transform;
            pivot.SetParent(transform, false);
            pivot.position = shot.type == ShotType.Orbit ? shot.orbitCentre : Vector3.zero;
            pivots[i] = pivot;

            var look = new GameObject("LookAt").transform;
            look.SetParent(transform, false);
            look.position = shot.lookAt;

            var cameraObject = new GameObject("Trailer_" + shot.name);
            cameraObject.transform.SetParent(pivot, false);
            cameraObject.transform.localPosition = shot.type == ShotType.Orbit ? new Vector3(0f, shot.height, shot.radius) : shot.cameraPosition;

            var cam = cameraObject.AddComponent<CinemachineCamera>();
            cam.Lens.FieldOfView = shot.fieldOfView;
            cam.Target.LookAtTarget = look;
            cameraObject.AddComponent<CinemachineHardLookAt>();
            cameraObject.SetActive(false);
            cameras[i] = cam;
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (active >= 0 && keyboard[stopKey].wasPressedThisFrame) Stop();
                for (int i = 0; i < shots.Length; i++)
                {
                    if (shots[i].key == Key.None || !keyboard[shots[i].key].wasPressedThisFrame) continue;
                    if (active == i) Stop();
                    else Play(i);
                    break;
                }
            }

            if (active >= 0)
            {
                // edits made in the inspector while the shot plays show up straight away
                Shot live = shots[active];
                cameras[active].Target.LookAtTarget.position = live.lookAt;
                cameras[active].Lens.FieldOfView = live.fieldOfView;

                if (live.type == ShotType.Orbit)
                {
                    pivots[active].position = live.orbitCentre;
                    cameras[active].transform.localPosition = new Vector3(0f, live.height, live.radius);
                    angles[active] += live.degreesPerSecond * Time.deltaTime;
                    pivots[active].rotation = Quaternion.Euler(0f, angles[active], 0f);
                }
                else
                {
                    shotTime += Time.deltaTime;
                    cameras[active].transform.localPosition = live.cameraPosition + live.drift * shotTime;
                }
            }
        }

        public void Play(int index)
        {
            if (index < 0 || index >= shots.Length) return;
            bool wasOff = active < 0;
            if (active >= 0) cameras[active].gameObject.SetActive(false);
            active = index;
            shotTime = 0f;
            angles[index] = shots[index].startAngle;
            pivots[index].rotation = Quaternion.Euler(0f, angles[index], 0f);
            cameras[index].gameObject.SetActive(true);
            if (wasOff) EnterTrailerMode();
        }

        public void Stop()
        {
            if (active < 0) return;
            cameras[active].gameObject.SetActive(false);
            active = -1;
            LeaveTrailerMode();
        }

        void EnterTrailerMode()
        {
            if (freezePlayer && player != null) player.enabled = false;
            if (tools != null) tools.enabled = false;

            if (!hideHud) return;
            Canvas[] all = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            var list = new System.Collections.Generic.List<Canvas>();
            foreach (Canvas c in all)
            {
                if (c.enabled && c.renderMode != RenderMode.WorldSpace) list.Add(c);
            }
            hiddenCanvases = list.ToArray();
            foreach (Canvas c in hiddenCanvases) c.enabled = false;

            if (tools != null && tools.tools != null)
            {
                toolWasOn = new bool[tools.tools.Length];
                for (int i = 0; i < tools.tools.Length; i++)
                {
                    GameObject root = tools.tools[i].root;
                    if (root == null) continue;
                    toolWasOn[i] = root.activeSelf;
                    root.SetActive(false);
                }
            }
        }

        void LeaveTrailerMode()
        {
            if (player != null) player.enabled = true;
            if (tools != null) tools.enabled = true;

            if (hiddenCanvases != null)
            {
                foreach (Canvas c in hiddenCanvases) if (c != null) c.enabled = true;
                hiddenCanvases = null;
            }
            if (tools != null && tools.tools != null && toolWasOn != null)
            {
                for (int i = 0; i < tools.tools.Length && i < toolWasOn.Length; i++)
                {
                    if (tools.tools[i].root != null) tools.tools[i].root.SetActive(toolWasOn[i]);
                }
                toolWasOn = null;
            }
        }

        void OnDisable()
        {
            if (active >= 0) Stop();
        }

#if UNITY_EDITOR
        [Tooltip("Which shot 'Look through preview shot' uses (0 = the first).")]
        public int previewShot;

        // Right-click this component > "Look through preview shot": puts the Scene view where that shot's camera starts,
        // looking at its look-at point, so you can see what the shot sees without pressing Play.
        [ContextMenu("Look through preview shot")]
        void LookThroughPreviewShot()
        {
            if (shots == null || shots.Length == 0) return;
            Shot shot = shots[Mathf.Clamp(previewShot, 0, shots.Length - 1)];
            UnityEditor.SceneView view = UnityEditor.SceneView.lastActiveSceneView;
            if (view == null) return;
            Vector3 position = StartPosition(shot);
            Quaternion rotation = Quaternion.LookRotation(shot.lookAt - position, Vector3.up);
            view.orthographic = false;
            view.LookAt(position, rotation, 0.01f, false, false);
            view.Repaint();
        }
#endif

        static Vector3 StartPosition(Shot shot)
        {
            if (shot.type == ShotType.Fixed) return shot.cameraPosition;
            float a = shot.startAngle * Mathf.Deg2Rad;
            return shot.orbitCentre + new Vector3(Mathf.Sin(a) * shot.radius, shot.height, Mathf.Cos(a) * shot.radius);
        }

        void OnDrawGizmosSelected()
        {
            if (shots == null) return;
            foreach (Shot shot in shots)
            {
                Gizmos.color = new Color(1f, 0.8f, 0.2f);
                Vector3 previous = shot.orbitCentre + new Vector3(0f, shot.height, shot.radius);
                for (int s = 1; s <= 48 && shot.type == ShotType.Orbit; s++)
                {
                    float a = s / 48f * Mathf.PI * 2f;
                    Vector3 next = shot.orbitCentre + new Vector3(Mathf.Sin(a) * shot.radius, shot.height, Mathf.Cos(a) * shot.radius);
                    Gizmos.DrawLine(previous, next);
                    previous = next;
                }

                // the camera at its starting point, drawn as a view cone
                Vector3 start = StartPosition(shot);
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(shot.lookAt, 0.25f);
                Gizmos.DrawLine(start, shot.lookAt);
                Matrix4x4 old = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(start, Quaternion.LookRotation(shot.lookAt - start, Vector3.up), Vector3.one);
                Gizmos.DrawFrustum(Vector3.zero, shot.fieldOfView, Mathf.Min(2f, Vector3.Distance(start, shot.lookAt)), 0.1f, 16f / 9f);
                Gizmos.matrix = old;
#if UNITY_EDITOR
                UnityEditor.Handles.Label(start + Vector3.up * 0.3f, shot.name);
#endif
            }
        }
    }
}
