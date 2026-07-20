using System;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target; // the player

    public Vector3 offset = new Vector3(0, 5f, -7f);

    public float followSpeed = 5f; 
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    private void LateUpdate()
    {
        Vector3 desiredPosition = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, followSpeed * Time.deltaTime);
        transform.LookAt(target);
    }
}
