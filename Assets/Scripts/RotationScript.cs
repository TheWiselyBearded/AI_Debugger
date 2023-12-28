using UnityEngine;

public class RotationScript : MonoBehaviour
{
    public float rotationSpeed = 30.0f; // Adjust the rotation speed in the Unity Editor

    void Update()
    {
        // Rotate the object around its Y-axis
        transform.Rotate(Vector3.up * rotationSpeed * Time.deltaTime);
    }
}
