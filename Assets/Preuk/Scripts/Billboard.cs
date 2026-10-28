using UnityEngine;

public class Billboard : MonoBehaviour
{
    [Tooltip("On = sprite stays standing up straight. Off = sprite tilts to face camera fully.")]
    public bool stayUpright = true;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void LateUpdate()
    {
        Transform cam = Camera.main.transform;

        if (stayUpright)
        {
            // Copy only the camera's left/right direction, ignore its tilt
            Vector3 forward = cam.forward;
            forward.y = 0f;
            transform.rotation = Quaternion.LookRotation(forward);
        }
        else
        {
            transform.rotation = cam.rotation;
        }
    }
}