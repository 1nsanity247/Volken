using UnityEngine;

public class CameraLook : MonoBehaviour
{
    public float rotationSpeed = 1.0f;
    public float orbitDistance = 5.0f;

    void Update()
    {
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
        transform.position = -orbitDistance * transform.forward;
    }
}
