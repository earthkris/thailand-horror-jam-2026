using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Makes hits feel heavy. Every time a unit takes damage:
//   1. Hit-stop: the whole game freezes for a split second.
//   2. The sprite flashes red. It glows, so you see it even in the dark.
//   3. The sprite shakes side to side.
//   4. The camera gets a small kick.
// Bigger hits (more damage) shake harder.
// Killing blow on a monster: it flashes, shudders, then collapses into the floor and goes dark.
// Healing gives a soft green glow instead.
// Monsters hidden in the dark don't flash (that would give them away); you still feel the camera kick.
// Lives on the Combat object. The sprites need the Preuk/Sprite Lit shader (it has the flash built in).
public class HitEffects : MonoBehaviour
{
    [Header("Flash")]
    public Color hitColor = new Color(0.95f, 0.12f, 0.08f);
    [Tooltip("Seconds the full red holds before it fades.")]
    public float flashHold = 0.08f;
    public float flashFade = 0.3f;

    [Header("Shake (the sprite)")]
    [Tooltip("How far it shakes, in tiles.")]
    public float shakeStrength = 0.08f;
    public float shakeTime = 0.3f;
    [Tooltip("Wiggles per second.")]
    public float shakeSpeed = 18f;

    [Header("Hit-stop")]
    [Tooltip("The game freezes this long on a hit. 0 = off.")]
    public float hitStop = 0.07f;

    [Header("Camera kick")]
    [Tooltip("Found automatically if empty.")]
    public CameraController cameraController;
    public float cameraShake = 0.07f;
    public float cameraShakeTime = 0.2f;

    [Header("Big hits")]
    [Tooltip("Damage that counts as a normal hit. More than this shakes harder (up to 2x), less is softer.")]
    [Min(1)] public int normalDamage = 6;

    [Header("Killing blow (monsters)")]
    public float collapseTime = 0.5f;

    [Header("Heal")]
    public Color healColor = new Color(0.55f, 0.9f, 0.5f);
    [Range(0f, 1f)] public float healGlow = 0.5f;
    public float healTime = 0.6f;

    static readonly int HitColorId = Shader.PropertyToID("_HitColor");
    static readonly int HitAmountId = Shader.PropertyToID("_HitAmount");

    readonly Dictionary<SpriteRenderer, Coroutine> flashes = new Dictionary<SpriteRenderer, Coroutine>();
    readonly Dictionary<Transform, Coroutine> shakes = new Dictionary<Transform, Coroutine>();
    readonly Dictionary<Transform, Vector3> shakeHome = new Dictionary<Transform, Vector3>();
    MaterialPropertyBlock block;
    int stopCount;

    void Awake()
    {
        block = new MaterialPropertyBlock();
        if (cameraController == null && TryGetComponent(out CombatManager combat)) cameraController = combat.cameraController;
        if (cameraController == null) cameraController = FindAnyObjectByType<CameraController>();
    }

    void OnEnable()
    {
        CombatUnit.OnAnyDamaged += OnDamaged;
        CombatUnit.OnAnyHealed += OnHealed;
    }

    void OnDisable()
    {
        CombatUnit.OnAnyDamaged -= OnDamaged;
        CombatUnit.OnAnyHealed -= OnHealed;
        // Don't leave the game frozen or a sprite off-centre
        if (stopCount > 0) { stopCount = 0; if (!PauseMenu.IsOpen) Time.timeScale = 1f; }
        foreach (var pair in shakeHome) if (pair.Key != null) pair.Key.localPosition = pair.Value;
        shakeHome.Clear();
        shakes.Clear();
        flashes.Clear();
    }

    // ================= Events =================

    void OnDamaged(CombatUnit unit, int amount)
    {
        float power = Mathf.Clamp((float)amount / normalDamage, 0.6f, 2f);
        SpriteRenderer sr = unit.GetComponentInChildren<SpriteRenderer>();
        bool visible = sr != null && sr.enabled && sr.gameObject.activeInHierarchy;

        if (hitStop > 0f) StartCoroutine(HitStop(hitStop * Mathf.Min(power, 1.5f)));
        if (cameraController != null) cameraController.Shake(cameraShake * power, cameraShakeTime);
        if (!visible) return;   // hidden in the dark: you feel it, you don't see it

        // A monster's sprite is hidden the moment it dies, so a copy plays the death
        if (!unit.IsAlive && unit.team == Team.Enemy)
        {
            StartCoroutine(Collapse(sr, power));
            return;
        }

        Flash(sr, hitColor, 1f, flashHold, flashFade);
        Shake(sr.transform, shakeStrength * unit.tileSize * power, shakeTime);
    }

    void OnHealed(CombatUnit unit, int amount)
    {
        SpriteRenderer sr = unit.GetComponentInChildren<SpriteRenderer>();
        if (sr == null || !sr.enabled) return;
        Flash(sr, healColor, healGlow, 0.1f, healTime);
    }

    // ================= Flash =================

    void Flash(SpriteRenderer sr, Color color, float peak, float hold, float fade)
    {
        if (flashes.TryGetValue(sr, out Coroutine running) && running != null) StopCoroutine(running);
        flashes[sr] = StartCoroutine(FlashRoutine(sr, color, peak, hold, fade));
    }

    IEnumerator FlashRoutine(SpriteRenderer sr, Color color, float peak, float hold, float fade)
    {
        SetFlash(sr, color, peak);
        yield return new WaitForSecondsRealtime(hold);   // real time: hit-stop doesn't stretch it
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(fade, 0.01f))
        {
            if (sr == null) yield break;
            SetFlash(sr, color, peak * (1f - t));
            yield return null;
        }
        if (sr != null) SetFlash(sr, color, 0f);
        flashes.Remove(sr);
    }

    // The shader mixes the sprite toward 'color' by 'amount' (0 = normal, 1 = solid colour)
    void SetFlash(SpriteRenderer sr, Color color, float amount)
    {
        sr.GetPropertyBlock(block);
        block.SetColor(HitColorId, color);
        block.SetFloat(HitAmountId, amount);
        sr.SetPropertyBlock(block);
    }

    // ================= Shake =================

    void Shake(Transform t, float strength, float time)
    {
        if (shakes.TryGetValue(t, out Coroutine running) && running != null) StopCoroutine(running);
        if (!shakeHome.ContainsKey(t)) shakeHome[t] = t.localPosition;   // where it rests
        t.localPosition = shakeHome[t];
        shakes[t] = StartCoroutine(ShakeRoutine(t, strength, time));
    }

    IEnumerator ShakeRoutine(Transform t, float strength, float time)
    {
        Vector3 home = shakeHome[t];
        SpriteAnimator body = t.GetComponent<SpriteAnimator>();
        if (body == null) body = t.GetComponentInParent<SpriteAnimator>();
        Vector3 side = Camera.main != null ? Camera.main.transform.right : Vector3.right;   // left-right on screen
        if (t.parent != null) side = t.parent.InverseTransformDirection(side);

        for (float e = 0f; e < time; e += Time.unscaledDeltaTime)
        {
            if (t == null) yield break;
            float fall = 1f - e / time;
            t.localPosition = home + side * (Mathf.Sin(e * shakeSpeed * Mathf.PI * 2f) * strength * fall * fall);
            if (body != null) body.HoldFacing(0.1f);   // shaking isn't walking: don't turn
            yield return null;
        }
        if (t != null) t.localPosition = home;
        if (body != null) body.HoldFacing(0.1f);
        shakes.Remove(t);
        shakeHome.Remove(t);
    }

    // ================= Hit-stop =================

    IEnumerator HitStop(float seconds)
    {
        if (PauseMenu.IsOpen) yield break;
        stopCount++;
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(seconds);
        stopCount = Mathf.Max(stopCount - 1, 0);
        if (stopCount == 0 && !PauseMenu.IsOpen) Time.timeScale = 1f;   // the pause menu un-freezes by itself
    }

    // ================= Killing blow =================

    IEnumerator Collapse(SpriteRenderer source, float power)
    {
        // Copy the monster's sprite exactly as it looks right now
        var ghost = new GameObject(source.name + " (dying)");
        ghost.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        ghost.transform.localScale = source.transform.lossyScale;
        var sr = ghost.AddComponent<SpriteRenderer>();
        sr.sprite = source.sprite;
        sr.flipX = source.flipX;
        sr.flipY = source.flipY;
        sr.color = source.color;
        sr.sharedMaterial = source.sharedMaterial;
        sr.sortingLayerID = source.sortingLayerID;
        sr.sortingOrder = source.sortingOrder;
        sr.shadowCastingMode = source.shadowCastingMode;
        SetFlash(sr, hitColor, 1f);

        Vector3 basePos = ghost.transform.position;
        Vector3 baseScale = ghost.transform.localScale;
        Vector3 side = Camera.main != null ? Camera.main.transform.right : Vector3.right;

        // Shudder, solid red
        float shudder = shakeTime * 1.3f;
        float strength = shakeStrength * power * 1.3f;
        for (float e = 0f; e < shudder; e += Time.unscaledDeltaTime)
        {
            ghost.transform.position = basePos + side * (Mathf.Sin(e * shakeSpeed * Mathf.PI * 2f) * strength);
            yield return null;
        }
        ghost.transform.position = basePos;

        // Collapse into the floor (the pivot is at the feet), the red burning down to black
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(collapseTime, 0.01f))
        {
            float e = t * t;
            ghost.transform.localScale = new Vector3(baseScale.x * (1f + 0.25f * e), baseScale.y * (1f - e), baseScale.z);
            SetFlash(sr, Color.Lerp(hitColor, Color.black, t), 1f);
            yield return null;
        }
        Destroy(ghost);
    }
}
