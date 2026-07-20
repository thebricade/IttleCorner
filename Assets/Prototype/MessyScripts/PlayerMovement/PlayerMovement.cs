using UnityEngine;

public class PlayerMovement : MonoBehaviour
{

    public float moveSpeed = 5f; // public so i can adjust in the inspector
    private CharacterController controller;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    // Update is called once per frame
    void Update()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical"); // this may need to be looked at with the new input system
        Vector3 moveDirection = new Vector3(horizontal, 0f, vertical);
        controller.Move(moveDirection * moveSpeed * Time.deltaTime);
        
    }
}
