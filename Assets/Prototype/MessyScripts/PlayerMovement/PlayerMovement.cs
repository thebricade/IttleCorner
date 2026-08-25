using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    private CharacterController controller;
    
    private Vector3 targetPosition;
    private bool hasTarget = false;
    public LayerMask groundLayer;

    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        if (GameModeManager.Instance.currentMode != GameMode.Explore)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit,groundLayer))
            {
                targetPosition = hit.point;
                hasTarget = true;
            }
        }

        if (hasTarget)
        {
            Vector3 nextPosition = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
            Vector3 delta = nextPosition - transform.position;
            controller.Move(delta);
        }
        
        /*float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 moveDirection = new Vector3(horizontal, 0f, vertical);
        controller.Move(moveDirection * moveSpeed * Time.deltaTime); */ 
    }
}