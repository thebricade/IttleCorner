using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlip
{
    // The look of the dialogue: the box, the name plate, the choice buttons, the talk prompt.
    // It is a normal canvas prefab, so restyle it in the editor: open the DialogueUI prefab and swap the sprites, fonts,
    // colours, sizes and layout. The DialogueRunner only fills in the text and shows / hides things, so as long as the
    // references below point at something, it works with any art.
    //
    // To bring in a UI you already made: put your canvas under this one and drag your own text, button and
    // image objects into these slots (the old ChoiceButton prefab works as the Choice Button Prefab).
    public class DialogueView : MonoBehaviour
    {
        [Header("Talk prompt (shown when you can talk to someone)")]
        public Button promptButton;
        public TMP_Text promptLabel;

        [Header("Conversation (shown while talking)")]
        [Tooltip("Everything in the conversation. Turned on and off as a whole.")]
        public GameObject conversationGroup;
        public TMP_Text nameText;
        public TMP_Text bodyText;
        [Tooltip("Shown when a line has no choices, to move on to the next.")]
        public Button continueButton;

        [Header("Choices")]
        [Tooltip("Where the choice buttons are created. Give it a Vertical Layout Group to stack them.")]
        public RectTransform choiceContainer;
        [Tooltip("A button with a text label inside it, copied for every choice. Can be an object in this prefab or a prefab asset.")]
        public Button choiceButtonPrefab;
        [Tooltip("Put '1.' '2.' in front of each choice, matching the number keys.")]
        public bool numberChoices = true;

        [Header("Messages (e.g. 'New ability: Glove!')")]
        public TMP_Text noticeText;

        // starts hidden when the game runs (it's visible in the editor so you can style it)
        void Awake()
        {
            SetPrompt(false, "");
            SetConversationVisible(false);
            if (choiceButtonPrefab != null && choiceButtonPrefab.gameObject.scene.IsValid()) choiceButtonPrefab.gameObject.SetActive(false);
            if (noticeText != null) noticeText.text = "";
        }

        public void SetPrompt(bool visible, string text)
        {
            if (promptButton == null) return;
            if (promptButton.gameObject.activeSelf != visible) promptButton.gameObject.SetActive(visible);
            if (visible && promptLabel != null) promptLabel.text = text;
        }

        public void SetConversationVisible(bool visible)
        {
            if (conversationGroup != null) conversationGroup.SetActive(visible);
        }

        public void ShowNotice(string text)
        {
            if (noticeText != null) noticeText.text = text;
        }
    }
}
