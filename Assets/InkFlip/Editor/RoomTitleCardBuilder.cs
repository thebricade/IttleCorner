using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlip.EditorTools
{
    // Makes the RoomTitleCard prefab and puts a copy in the scene.
    // The prefab is only created if it doesn't exist yet, so rebuilding the scene never overwrites your styling:
    // restyle Assets/InkFlip/Prefabs/RoomTitleCard.prefab and it stays that way.
    public static class RoomTitleCardBuilder
    {
        const string PrefabFolder = "Assets/InkFlip/Prefabs";
        const string PrefabPath = PrefabFolder + "/RoomTitleCard.prefab";

        [MenuItem("Tools/InkFlip/Add Room Title Card To Scene")]
        public static void AddFromMenu()
        {
            RoomTitleCard card = AddToScene();
            if (card != null) Selection.activeGameObject = card.gameObject;
        }

        // Adds a card to the open scene unless one is already there. Returns the card in the scene.
        public static RoomTitleCard AddToScene()
        {
            var existing = Object.FindAnyObjectByType<RoomTitleCard>();
            if (existing != null) return existing;

            GameObject prefab = EnsurePrefab();
            if (prefab == null) return null;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, "Add Room Title Card");
            return instance.GetComponent<RoomTitleCard>();
        }

        public static GameObject EnsurePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null) return existing;

            if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/InkFlip", "Prefabs");

            GameObject root = BuildCard();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log("InkFlip: created " + PrefabPath + " - restyle it any way you like.");
            return prefab;
        }

        static GameObject BuildCard()
        {
            var root = new GameObject("RoomTitleCard", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup), typeof(RoomTitleCard));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // never catch clicks
            var group = root.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            // everything that slides lives in one panel, near the top of the screen
            RectTransform panel = NewRect("Panel", root.transform);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.82f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(1400f, 300f);
            panel.anchoredPosition = Vector2.zero;

            // soft dark backing so the text reads over any wall (delete or restyle freely)
            Image backing = NewRect("Backing", panel).gameObject.AddComponent<Image>();
            backing.color = new Color(0f, 0f, 0f, 0.35f);
            backing.raycastTarget = false;
            Stretch(backing.rectTransform);

            TMP_Text title = NewText("Title", panel, "Room Title", 110, FontStyles.Bold);
            title.rectTransform.anchorMin = new Vector2(0f, 0.42f);
            title.rectTransform.anchorMax = new Vector2(1f, 0.95f);
            title.rectTransform.offsetMin = title.rectTransform.offsetMax = Vector2.zero;
            title.outlineWidth = 0.18f;
            title.outlineColor = new Color32(0, 0, 0, 200);

            Image accent = NewRect("Accent", panel).gameObject.AddComponent<Image>();
            accent.color = Color.white;
            accent.raycastTarget = false;
            accent.rectTransform.anchorMin = accent.rectTransform.anchorMax = new Vector2(0.5f, 0.4f);
            accent.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            accent.rectTransform.sizeDelta = new Vector2(420f, 6f);
            accent.rectTransform.anchoredPosition = Vector2.zero;

            TMP_Text subtitle = NewText("Subtitle", panel, "A short line about the room", 44, FontStyles.Normal);
            subtitle.rectTransform.anchorMin = new Vector2(0f, 0.05f);
            subtitle.rectTransform.anchorMax = new Vector2(1f, 0.36f);
            subtitle.rectTransform.offsetMin = subtitle.rectTransform.offsetMax = Vector2.zero;
            subtitle.color = new Color(1f, 1f, 1f, 0.9f);

            var card = root.GetComponent<RoomTitleCard>();
            card.group = group;
            card.titleText = title;
            card.subtitleText = subtitle;
            card.accent = accent;
            card.slideTarget = panel;
            return root;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        static TMP_Text NewText(string name, Transform parent, string text, float size, FontStyles style)
        {
            RectTransform rect = NewRect(name, parent);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null) label.font = TMP_Settings.defaultFontAsset;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }
    }
}
