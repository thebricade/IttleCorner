using System;
using UnityEngine;

namespace InkFlip
{
    // Put this on the secretary (the character with the NPCTalker). When a conversation runs the dialogue action
    // StartGame with the parameter "open_doors" (see Penny in Dialogue.csv), every door that needs the secretary
    // (AbilityDoor with Needs Secretary ticked) slides open as the conversation closes.
    //
    // If there is no DoorKeeper in the scene at all, those doors don't wait for one - so a scene without a secretary still works.
    public class DoorKeeper : MonoBehaviour
    {
        [Tooltip("The dialogue action parameter that opens the doors.")]
        public string openId = "open_doors";
        [Tooltip("Tick this (in the inspector, while playing) to open every door without talking to anyone.")]
        public bool debugOpen;

        public static bool DoorsOpen { get; private set; }
        // true while a secretary is in the scene
        public static bool Exists => instances > 0;
        public static event Action Opened;

        static int instances;
        bool pending;

        void OnEnable()
        {
            instances++;
            MiniGameEvents.Requested += OnRequested;
        }

        void OnDisable()
        {
            instances = Mathf.Max(0, instances - 1);
            MiniGameEvents.Requested -= OnRequested;
        }

        void OnRequested(string id)
        {
            if (id == openId) pending = true;
        }

        void Update()
        {
            if (debugOpen && !DoorsOpen) Open(announce: false);

            // wait for the conversation to close, so the message isn't buried under it
            if (pending && (DialogueRunner.Instance == null || !DialogueRunner.Instance.IsActive))
            {
                pending = false;
                Open(announce: true);
            }
        }

        [ContextMenu("Open the doors now")]
        void OpenFromMenu() => Open(announce: false);

        void Open(bool announce)
        {
            if (DoorsOpen) return;
            DoorsOpen = true;
            Opened?.Invoke();
            if (announce) LineRoomUI.Instance.Say("All signed in - the doors are open!", 3.5f);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            DoorsOpen = false;
            instances = 0;
            Opened = null;
        }
    }
}
