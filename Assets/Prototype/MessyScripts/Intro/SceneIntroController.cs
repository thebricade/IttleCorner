using UnityEngine;
using System.Collections;

public class SceneIntroController : MonoBehaviour
{
    public NPCData introNPC;
    public Transform introNPCTransform;
    public float delay = 0.5f; 
    public SpriteRenderer paintingDisplay;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        StartCoroutine(TriggerIntroDialogue());
    }

    IEnumerator TriggerIntroDialogue()
    {
        yield return new WaitForSeconds(delay);

        // apply the intro painting to the sprite renderer
        if (DrawingManager.Instance.savedDrawings.Count > 0)
        {
            Texture2D texture = DrawingManager.Instance.savedDrawings[0].texture;
            Sprite paintingSprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f)
            );
            paintingDisplay.sprite = paintingSprite;
        }

        DialogueManager.Instance.StartDialogue(introNPC, introNPCTransform.position);
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
