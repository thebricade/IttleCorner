using UnityEngine;

namespace InkFlip
{
    // A door that stays shut until the player is allowed through, then slides out of the way (down into the floor by default).
    // It can wait for two things, either or both:
    //   - the secretary: talk to the character with a DoorKeeper first (ticked by default; ignored if the scene has no secretary)
    //   - an earned ability, e.g. the glove (set Requires to None to not need one)
    // A sign on both faces says which one is missing. To make a door, use GameObject > InkFlip > Hub Door.
    public class AbilityDoor : MonoBehaviour
    {
        [Tooltip("Stays shut until the player has talked to the secretary.")]
        public bool needsSecretary = true;
        public Ability requires = Ability.Glove;
        [Tooltip("How far the door moves when it opens, in world space (down into the floor by default).")]
        public Vector3 openOffset = new Vector3(0f, -2.6f, 0f);
        public float speed = 2.5f;
        [TextArea] public string secretaryText = "Locked\nSign in with the secretary";
        [TextArea] public string lockedText = "Locked\nEarn the glove first";
        [Tooltip("Which way the sign faces, in the door's own directions (the sign is put on both faces).")]
        public Vector3 signFacing = Vector3.forward;
        public Vector3 signOffset = new Vector3(0f, 0.2f, 0.12f);

        Vector3 closedPosition;
        bool open;
        TextMesh[] signs;

        void Start()
        {
            closedPosition = transform.position;
            signs = new TextMesh[2];
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? 1f : -1f;
                var signObject = new GameObject("LockedSign");
                signObject.transform.SetParent(transform, false);
                signObject.transform.localPosition = new Vector3(signOffset.x, signOffset.y, signOffset.z * side);
                // text reads correctly for someone looking along the sign's forward, so point it away from the viewer
                signObject.transform.rotation = Quaternion.LookRotation(-transform.TransformDirection(signFacing * side), Vector3.up);
                signObject.transform.localScale = Vector3.one * 0.5f;
                var sign = signObject.AddComponent<TextMesh>();
                sign.anchor = TextAnchor.MiddleCenter;
                sign.alignment = TextAlignment.Center;
                sign.characterSize = 0.1f;
                sign.fontSize = 48;
                sign.color = Color.white;
                signs[i] = sign;
            }
        }

        void Update()
        {
            bool secretaryOk = !needsSecretary || !DoorKeeper.Exists || DoorKeeper.DoorsOpen;
            bool abilityOk = requires == Ability.None || !Abilities.IsLocked(requires);
            open = secretaryOk && abilityOk;

            Vector3 target = open ? closedPosition + openOffset : closedPosition;
            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);

            if (signs == null) return;
            string text = secretaryOk ? lockedText : secretaryText;
            foreach (TextMesh sign in signs)
            {
                if (sign == null) continue;
                if (sign.gameObject.activeSelf == open) sign.gameObject.SetActive(!open);
                if (!open && sign.text != text) sign.text = text;
            }
        }
    }
}
