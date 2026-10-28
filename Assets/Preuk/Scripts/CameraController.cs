using UnityEngine;

public class CameraController : MonoBehaviour
{
    public Transform target;
    public float distance = 10f;
    public float smoothTime = 0.3f;

    Vector3 velocity;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPos = target.position - transform.forward * distance;
        transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref velocity, smoothTime);
    }

    public void FocusOn(Transform newTarget)
    {
        target = newTarget;
    }
}
