using UnityEngine;

public class CameraSpin : MonoBehaviour
{
    public Transform target; // The object to rotate around
    public float rotationSpeed = 50f; // Speed of rotation in degrees per second

    void Update()
    {
        if (target != null)
        {
            // Rotate the camera around the target's position
            // Vector3.up defines the axis of rotation (vertical)
            // Time.deltaTime makes the rotation frame-rate independent
            transform.RotateAround(target.position, Vector3.forward, rotationSpeed * Time.deltaTime);
        }
    }
}