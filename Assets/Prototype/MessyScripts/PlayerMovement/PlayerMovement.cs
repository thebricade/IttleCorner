using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float bobSpeed = 3f;
    public float bobAmount = 0.02f;

    private CharacterController controller;
    private Vector3 targetPosition;
    private bool hasTarget = false;
    public SpriteRenderer playerSprite;
    private Vector3 spriteStartLocalPos;
    public LayerMask groundLayer;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        playerSprite = GetComponentInChildren<SpriteRenderer>();

        if (playerSprite != null)
            spriteStartLocalPos = playerSprite.transform.localPosition;
    }

    void Update()
    {
       
        if (GameModeManager.Instance.currentMode != GameMode.Explore)
        {
            hasTarget = false;
            return;
        }

        if (DialogueManager.Instance.dialogueCanvas.activeSelf)
        {
            hasTarget = false;
            return;
        }

        // click to move
        if (Input.GetMouseButton(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, Mathf.Infinity, groundLayer))
            {
                targetPosition = hit.point;
                hasTarget = true;
            }
        }

        // move toward target
        if (hasTarget)
        {
            Vector3 nextPosition = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                moveSpeed * Time.deltaTime
            );
            Vector3 delta = nextPosition - transform.position;
            controller.Move(delta);

            // flip sprite based on horizontal direction
            if (playerSprite != null)
            {
                Vector3 direction = targetPosition - transform.position;
                if (direction.x > 0.1f)
                    playerSprite.flipX = true;
                else if (direction.x < -0.1f)
                    playerSprite.flipX = false;
            }

            // bob up and down while moving
            if (playerSprite != null)
            {
                float bobY = Mathf.Sin(Time.time * bobSpeed) * bobAmount;
                playerSprite.transform.localPosition = new Vector3(
                    spriteStartLocalPos.x,
                    spriteStartLocalPos.y + bobY,
                    spriteStartLocalPos.z
                );
            }

            // stop when close enough
            if (Vector3.Distance(
                    new Vector3(transform.position.x, 0, transform.position.z),
                    new Vector3(targetPosition.x, 0, targetPosition.z)) < 0.5f)
            {
                hasTarget = false;
                if (playerSprite != null)
                    playerSprite.transform.localPosition = spriteStartLocalPos;
            } 
        }
    }
}