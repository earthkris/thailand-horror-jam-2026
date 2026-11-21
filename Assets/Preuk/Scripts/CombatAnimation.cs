using System.Collections;
using UnityEngine;

// How attacks look, shared by the party (CombatTargeting), monsters (EnemyAI) and opportunity attacks:
// the melee swing and lunge, and projectiles flying at their target.
public static class CombatAnimation
{
    // Melee: play the attacker's swing (if its SpriteAnimator has attack frames) and lunge forward
    // on the slash frame. No attack frames = just the lunge.
    // 'with' can bring its own swing frames (Divine Smite's glowing sword).
    public static IEnumerator Swing(CombatUnit who, CombatUnit at, CombatAction with = null)
    {
        SpriteAnimator anim = who.GetComponentInChildren<SpriteAnimator>();
        if (anim == null || !anim.HasAttackFrames)
        {
            yield return Lunge(who, at);
            yield break;
        }

        if (with != null) anim.PlayAttack(at.transform.position, with.swingDownFrames, with.swingUpFrames, with.swingSideFrames);
        else anim.PlayAttack(at.transform.position);
        while (anim.IsAttacking && !anim.OnLastAttackFrame) yield return null;   // wind-up
        yield return Lunge(who, at);                                              // slash
        while (anim.IsAttacking) yield return null;                               // follow-through
    }

    // Quick jab toward the target and back. Facing is held, so sliding back home doesn't turn us around.
    public static IEnumerator Lunge(CombatUnit who, CombatUnit at)
    {
        Transform body = who.transform;
        SpriteAnimator anim = who.GetComponentInChildren<SpriteAnimator>();
        Vector3 home = body.position;
        Vector3 toward = at.transform.position - home;
        toward.y = 0f;
        if (anim != null) anim.FaceToward(toward);
        Vector3 hitPos = home + toward.normalized * 0.35f * who.tileSize;

        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.1f) { body.position = Vector3.Lerp(home, hitPos, t); Hold(anim); yield return null; }
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.15f) { body.position = Vector3.Lerp(hitPos, home, t); Hold(anim); yield return null; }
        body.position = home;
        Hold(anim);
    }

    static void Hold(SpriteAnimator anim)
    {
        if (anim != null) anim.HoldFacing(0.1f);
    }

    // Where a shot at 'to' ends up: on the target, or (a miss) wide of it and a little past
    public static Vector3 ShotEnd(Vector3 from, Vector3 to, bool hit, float tileSize)
    {
        if (hit) return to;
        Vector3 flat = to - from;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.0001f) return to;
        Vector3 side = Vector3.Cross(Vector3.up, flat).normalized * (Random.value < 0.5f ? -0.7f : 0.7f);
        return to + (side + flat.normalized * 0.4f) * tileSize;
    }

    // Fly a projectile from one point to another ('speed' in world units per second), then remove it.
    // No prefab = a plain ball. It points the way it flies (arrows turn, flipbook sprites rotate on screen).
    public static IEnumerator Fly(GameObject prefab, Vector3 from, Vector3 to, float speed)
    {
        GameObject p = prefab != null ? Object.Instantiate(prefab) : PlaceholderBall();
        p.transform.position = from;
        if (to != from) p.transform.rotation = Quaternion.LookRotation(to - from);
        if (p.TryGetComponent(out SpriteFlipbook flipbook)) flipbook.travelDirection = to - from;

        float time = Vector3.Distance(from, to) / Mathf.Max(speed, 0.1f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(time, 0.01f))
        {
            p.transform.position = Vector3.Lerp(from, to, t);
            yield return null;
        }
        Object.Destroy(p);
    }

    static GameObject PlaceholderBall()
    {
        GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "Projectile (placeholder)";
        Object.Destroy(ball.GetComponent<Collider>());
        ball.transform.localScale = Vector3.one * 0.2f;
        return ball;
    }
}
