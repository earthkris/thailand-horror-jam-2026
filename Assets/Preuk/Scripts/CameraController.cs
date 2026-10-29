using UnityEngine;

// Follows a target smoothly. Shake() gives it a short kick (HitEffects uses it on hits).
public class CameraController : MonoBehaviour
{
    public Transform target;
    public float distance = 10f;
    public float smoothTime = 0.3f;

    Vector3 velocity;
    Vector3 lastShake;      // last frame's shake offset, taken off again before following
    float shakeLeft, shakeTotal, shakeStrength;

    void LateUpdate()
    {
        // Where the camera would be without the shake
        Vector3 basePos = transform.position - lastShake;

        if (target != null)
        {
            Vector3 desiredPos = target.position - transform.forward * distance;
            basePos = Vector3.SmoothDamp(basePos, desiredPos, ref velocity, smoothTime);
        }

        // Shake: random jolts that die down (real time, so it still plays during hit-stop)
        Vector3 shake = Vector3.zero;
        if (shakeLeft > 0f)
        {
            shakeLeft -= Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(shakeLeft / shakeTotal);
            shake = (transform.right * Random.Range(-1f, 1f) + transform.up * Random.Range(-1f, 1f)) * shakeStrength * k * k;
        }

        transform.position = basePos + shake;
        lastShake = shake;
    }

    // As a prefab in a new scene: follow the player, and stack overlay cameras
    // (the dice tray) on top of this one. A prefab can't keep those links by itself.
    void Awake()
    {
        if (target == null)
        {
            var player = FindAnyObjectByType<PlayerMovement>();
            if (player != null) target = player.transform;
        }

        var data = GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        if (data == null || data.renderType != UnityEngine.Rendering.Universal.CameraRenderType.Base) return;
        foreach (Camera cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            var other = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (other != null && other.renderType == UnityEngine.Rendering.Universal.CameraRenderType.Overlay && !data.cameraStack.Contains(cam))
                data.cameraStack.Add(cam);
        }
    }

    public void FocusOn(Transform newTarget)
    {
        target = newTarget;
    }

    // strength in world units, time in seconds
    public void Shake(float strength, float time)
    {
        shakeStrength = shakeLeft > 0f ? Mathf.Max(shakeStrength, strength) : strength;
        shakeLeft = Mathf.Max(shakeLeft, time);
        shakeTotal = shakeLeft;
    }
}
