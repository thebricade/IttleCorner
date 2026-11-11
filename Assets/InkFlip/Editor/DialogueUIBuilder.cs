using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlip.EditorTools
{
    // Makes the DialogueUI prefab (using the dialogue art from the original game) and puts a copy in the scene.
    // The prefab is only created if it doesn't exist yet, so rebuilding the scene never overwrites your styling:
    // restyle Assets/InkFlip/Prefabs/DialogueUI.prefab and it stays that way.
    //
    // The art it picks up, from Assets/Prototype/Messy Image:
    //   dialogBox_grey            the main text box (9-sliced)
    //   dialogBox_green_nameplate the name plate (9-sliced)
    //   dialogBox_blue            the choice buttons and the talk prompt
    //   UI_nextArrow              the continue arrow
    // Missing files are replaced with plain colour blocks, so it still works.
    public static class DialogueUIBuilder
    {
        const string PrefabFolder = "Assets/InkFlip/Prefabs";
        const string PrefabPath = PrefabFolder + "/DialogueUI.prefab";
        const string ArtFolder = "Assets/Prototype/Messy Image/";

        static readonly Color TextColor = new Color(0.14f, 0.15f, 0.2f);

        [MenuItem("Tools/InkFlip/Add Dialogue UI To Scene")]
        public static void AddFromMenu()
        {
            DialogueView view = AddToScene();
            if (view != null) Selection.activeGameObject = view.gameObject;
        }

        // Adds the dialogue UI to the open scene unless it's already there. Returns the view in the scene.
        public static DialogueView AddToScene()
        {
            var existing = Object.FindAnyObjectByType<DialogueView>();
            if (existing != null) return existing;

            GameObject prefab = EnsurePrefab();
            if (prefab == null) return null;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, "Add Dialogue UI");
            return instance.GetComponent<DialogueView>();
        }

        public static GameObject EnsurePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null) return existing;

            if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/InkFlip", "Prefabs");

            GameObject root = Build();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log("InkFlip: created " + PrefabPath + " - restyle it any way you like.");
            return prefab;
        }

        static GameObject Build()
        {
            Sprite greyBox = LoadSprite("dialogBox_grey.png");
            Sprite greenPlate = LoadSprite("dialogBox_green_nameplate.png");
            Sprite bluePill = LoadSprite("dialogBox_blue.png");
            Sprite nextArrow = LoadSprite("UI_nextArrow.png");

            var root = new GameObject("DialogueUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(DialogueView));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 45;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var view = root.GetComponent<DialogueView>();

            // --- talk prompt ---
            Button prompt = NewPill("TalkPrompt", root.transform, bluePill, new Vector2(760f, 110f), out TMP_Text promptLabel, "Press F to talk");
            Anchor((RectTransform)prompt.transform, new Vector2(0.5f, 0f), new Vector2(0f, 190f));
            view.promptButton = prompt;
            view.promptLabel = promptLabel;

            // --- the conversation: everything below turns on and off together ---
            RectTransform conversation = NewRect("Conversation", root.transform);
            Stretch(conversation);
            view.conversationGroup = conversation.gameObject;

            // main text box
            Image box = NewSliced("Box", conversation, greyBox, new Color(0.9f, 0.9f, 0.9f, 0.96f), 3.5f);
            Anchor(box.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 30f));
            box.rectTransform.sizeDelta = new Vector2(1500f, 360f);

            TMP_Text body = NewText("Body", box.transform, "What the character says goes here.", 38, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            body.color = TextColor;
            StretchWithMargins(body.rectTransform, 110f, 95f, 110f, 55f);
            view.bodyText = body;

            // name plate, overlapping the top-left of the box
            Image plate = NewSliced("NamePlate", box.transform, greenPlate, Color.white, 3.5f);
            Anchor(plate.rectTransform, new Vector2(0f, 1f), new Vector2(250f, 10f));
            plate.rectTransform.sizeDelta = new Vector2(420f, 140f);

            TMP_Text name = NewText("Name", plate.transform, "Name", 44, FontStyles.Bold, TextAlignmentOptions.Center);
            name.color = Color.white;
            name.outlineWidth = 0.15f;
            name.outlineColor = new Color32(30, 70, 35, 200);
            StretchWithMargins(name.rectTransform, 30f, 20f, 30f, 25f);
            view.nameText = name;

            // continue arrow
            Button next = NewButton("Continue", box.transform, nextArrow, Color.white);
            Anchor((RectTransform)next.transform, new Vector2(1f, 0f), new Vector2(-95f, 70f));
            ((RectTransform)next.transform).sizeDelta = new Vector2(90f, 92f);
            view.continueButton = next;

            // choices stack upward from just above the box
            RectTransform choices = NewRect("Choices", conversation);
            Anchor(choices, new Vector2(0.5f, 0f), new Vector2(0f, 410f));
            choices.sizeDelta = new Vector2(1100f, 560f);
            var layout = choices.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.LowerCenter;
            layout.spacing = 14f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            view.choiceContainer = choices;

            // the choice button that gets copied for every choice (stays switched off)
            Button template = NewPill("ChoiceButton", choices, bluePill, new Vector2(1000f, 112f), out _, "A choice");
            var element = template.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = 1000f;
            element.preferredHeight = 112f;
            template.gameObject.SetActive(false);
            view.choiceButtonPrefab = template;

            // --- messages ---
            TMP_Text notice = NewText("Notice", root.transform, "", 50, FontStyles.Bold, TextAlignmentOptions.Center);
            notice.color = new Color(1f, 0.85f, 0.4f);
            notice.outlineWidth = 0.2f;
            notice.outlineColor = new Color32(0, 0, 0, 200);
            Anchor(notice.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -130f));
            notice.rectTransform.sizeDelta = new Vector2(1300f, 80f);
            view.noticeText = notice;

            return root;
        }

        // ---------- helpers ----------

        static Sprite LoadSprite(string file)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + file);
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Anchor(RectTransform rect, Vector2 anchor, Vector2 position)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        static void StretchWithMargins(RectTransform rect, float left, float top, float right, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        // a stretchable panel: 9-sliced when the sprite has borders. The multiplier shrinks the borders so a small box still has room for text.
        static Image NewSliced(string name, Transform parent, Sprite sprite, Color color, float borderShrink)
        {
            Image image = NewRect(name, parent).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = sprite != null ? color : new Color(0.85f, 0.85f, 0.85f, 0.95f);
            image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            image.pixelsPerUnitMultiplier = borderShrink;
            image.raycastTarget = false;
            return image;
        }

        static Button NewButton(string name, Transform parent, Sprite sprite, Color color)
        {
            Image image = NewRect(name, parent).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = sprite != null ? color : new Color(0.5f, 0.75f, 0.85f);
            image.preserveAspect = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            return button;
        }

        // a pill-shaped button with a text label
        static Button NewPill(string name, Transform parent, Sprite sprite, Vector2 size, out TMP_Text label, string text)
        {
            Button button = NewButton(name, parent, sprite, Color.white);
            ((Image)button.targetGraphic).preserveAspect = false;
            ((RectTransform)button.transform).sizeDelta = size;

            label = NewText("Label", button.transform, text, 38, FontStyles.Bold, TextAlignmentOptions.Center);
            label.color = TextColor;
            StretchWithMargins(label.rectTransform, 70f, 12f, 70f, 12f);
            return button;
        }

        static TMP_Text NewText(string name, Transform parent, string text, float size, FontStyles style, TextAlignmentOptions alignment)
        {
            RectTransform rect = NewRect(name, parent);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null) label.font = TMP_Settings.defaultFontAsset;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.alignment = alignment;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }
    }
}
