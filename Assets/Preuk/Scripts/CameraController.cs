using UnityEngine;

// Follows a target smoothly. Shake() gives it a short kick (HitEffects uses it on hits).
// Pan() slides the view away from the target (in a fight, when move mode is off); it snaps back on FocusOn.
public class CameraController : MonoBehaviour
{
    // The game camera, for scripts that only want to shake it or point it somewhere. Null if there is none.
    public static CameraController Main => main != null ? main : (main = FindAnyObjectByType<CameraController>());
    static CameraController main;

    public Transform target;
    public float distance = 10f;
    public float smoothTime = 0.3f;

    [Header("Panning (in a fight)")]
    [Tooltip("World units per second while a pan key is held.")]
    public float panSpeed = 7f;
    [Tooltip("How far the view can slide away from whoever it follows.")]
    public float maxPan = 16f;

    Vector3 velocity;
    Vector3 panOffset;
    Vector3 lastShake;      // last frame's shake offset, taken off again before following
    float shakeLeft, shakeTotal, shakeStrength;

    void LateUpdate()
    {
        // Where the camera would be without the shake
        Vector3 basePos = transform.position - lastShake;

        if (target != null)
        {
            Vector3 desiredPos = target.position + panOffset - transform.forward * distance;
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

    // Jump to the target with no smoothing (after a teleport, while the screen is black)
    public void SnapToTarget()
    {
        if (target == null) return;
        transform.position = target.position - transform.forward * distance;
        velocity = Vector3.zero;
        lastShake = Vector3.zero;
    }

    public void FocusOn(Transform newTarget)
    {
        target = newTarget;
        panOffset = Vector3.zero;   // a new turn: look at who's acting
    }

    // Slide the view along the floor. 'input' x = right, y = into the screen.
    public void Pan(Vector2 input, float deltaTime)
    {
        Vector3 right = transform.right; right.y = 0f; right.Normalize();
        Vector3 ahead = transform.forward; ahead.y = 0f; ahead.Normalize();
        panOffset += (right * input.x + ahead * input.y) * panSpeed * deltaTime;
        panOffset = Vector3.ClampMagnitude(panOffset, maxPan);
    }

    // Move the view by an exact world offset (dragging with the mouse)
    public void PanBy(Vector3 worldOffset)
    {
        worldOffset.y = 0f;
        panOffset = Vector3.ClampMagnitude(panOffset + worldOffset, maxPan);
    }

    public void ResetPan() => panOffset = Vector3.zero;

    // strength in world units, time in seconds
    public void Shake(float strength, float time)
    {
        shakeStrength = shakeLeft > 0f ? Mathf.Max(shakeStrength, strength) : strength;
        shakeLeft = Mathf.Max(shakeLeft, time);
        shakeTotal = shakeLeft;
    }
}
