using UnityEngine;

public class PlacementController : MonoBehaviour
{
    public GameObject stickerPrefab; // the prefab we'll spawn - has SpriteRenderer

    void Update()
    {
        if (GameModeManager.Instance.currentMode != GameMode.Placing)
        {
            return; // only care about input while in Placing mode
        }

        if (Input.GetMouseButtonDown(0))
        {
            TryPlace();
        }
    }

    void TryPlace()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            PlaceSticker(hit.point);
        }
    }

    void PlaceSticker(Vector3 position)
    {
        GameObject newSticker = Instantiate(stickerPrefab, position, Quaternion.identity);

        SpriteRenderer sr = newSticker.GetComponent<SpriteRenderer>();
        Sprite drawingSprite = TextureToSprite(DrawingManager.Instance.selectedDrawing.texture);
        sr.sprite = drawingSprite;

        GameModeManager.Instance.SetGameMode(GameMode.Explore);
    }

    Sprite TextureToSprite(Texture2D texture)
    {
        return Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0f), // pivot at bottom-center
            50f // pixels per unit - adjust to taste
        );
    }
}
