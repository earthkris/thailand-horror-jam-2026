using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Which look a move has. Set it on a Combat Action ("Fx"), or on a monster's EnemyAI.
// New ones go on the end: assets store these as numbers.
public enum MoveFxStyle
{
    None,
    // Kairos
    SwordSlash, Sweep, SkySword, LayOnHands, Thunder, Judgement,
    // Lucius
    HolyFire, Heal, Bless, DivinePunishment, Rejuvenation, ShieldOfFaith, HolyNova,
    // Theron
    Arrow, AimedShot, PinningShot, Volley, Knife,
    // Magnus
    AxeCleave, RecklessSwing, Whirlwind, BloodRage,
    // Anyone
    Help, FireFlask, HolyWater, Elixir,
    // Monsters
    Bite, Claw, VenomBite, Wail, Drain, Slam, Acid, Bone, Hex,
}

// Every move gets its own effect instead of the same fireball and sparkles:
//   Cast   - the wind-up on the user (a sigil under the priest, a reticle on the target...)
//   Travel - how it gets there, for moves with their own way (lightning from the sky, a rain of arrows).
//            The rest fly the action's projectile as before.
//   Impact - what happens where it lands (a slash, a pillar of fire, a shield bubble...)
//   Splash - the smaller version on the others caught in an area
// Built from FxPiece (Kenney Particle Pack sprites in Resources/Fx). Nothing to set up in the scene.
public static class MoveFx
{
    // ---------- Colours ----------
    static readonly Color Gold = new Color(1f, 0.82f, 0.38f);
    static readonly Color Holy = new Color(1f, 0.96f, 0.82f);
    static readonly Color Green = new Color(0.5f, 1f, 0.5f);
    static readonly Color Leaf = new Color(0.35f, 0.9f, 0.45f);
    static readonly Color Bolt = new Color(0.62f, 0.8f, 1f);
    static readonly Color Steel = new Color(0.92f, 0.94f, 1f);
    static readonly Color Blood = new Color(0.95f, 0.15f, 0.1f);
    static readonly Color Ember = new Color(1f, 0.45f, 0.12f);
    static readonly Color Venom = new Color(0.55f, 0.95f, 0.2f);
    static readonly Color Ghost = new Color(0.72f, 0.5f, 1f);
    static readonly Color Ward = new Color(0.55f, 0.78f, 1f);
    static readonly Color Dust = new Color(0.55f, 0.48f, 0.4f, 0.8f);

    // ================= What the callers use =================

    // Moves that bring their own way to the target (no projectile flies)
    public static bool OwnsTravel(MoveFxStyle s) =>
        s == MoveFxStyle.Thunder || s == MoveFxStyle.Judgement || s == MoveFxStyle.DivinePunishment ||
        s == MoveFxStyle.Volley || s == MoveFxStyle.HolyNova || s == MoveFxStyle.Wail || s == MoveFxStyle.Hex;

    // On the user as the move starts. Returns how long to wait before rolling.
    public static float Cast(MoveFxStyle s, CombatUnit user, CombatUnit target)
    {
        if (user == null) return 0f;
        Transform book = user.castPoint != null ? user.castPoint : user.transform;
        Vector3 hand = book.position + Front();
        switch (s)
        {
            case MoveFxStyle.HolyFire:
                FxPiece.Make("magic_05", hand, Gold, 0.3f, 0.45f).Grow(0.8f).Fade(0.2f, 0.6f).Light(Gold, 3f, 2f);
                return 0.3f;
            case MoveFxStyle.Heal:
            case MoveFxStyle.Rejuvenation:
                FxPiece.Make("twirl_01", hand, Green, 0.5f, 0.5f).Grow(0.9f).Spin(-540f).Light(Green, 3f, 1.5f);
                return 0.3f;
            case MoveFxStyle.Bless:
            case MoveFxStyle.ShieldOfFaith:
                FxPiece.Make("magic_03", hand, s == MoveFxStyle.Bless ? Gold : Ward, 0.3f, 0.6f).Grow(0.9f).Spin(120f).Light(Gold, 3f, 1.5f);
                return 0.35f;
            case MoveFxStyle.DivinePunishment:
                FxPiece.Make("magic_01", Feet(user) + Vector3.up * 0.03f, Ember, 0.5f, 0.9f).Grow(1.8f).Flat().Spin(160f).Fade(0.2f, 0.4f).Light(Ember, 4f, 2f);
                FxPiece.Make("magic_04", hand, Ember, 0.5f, 0.6f).Grow(1f).Spin(200f);
                return 0.45f;
            case MoveFxStyle.Judgement:
                FxPiece.Make("magic_01", Feet(user) + Vector3.up * 0.03f, Gold, 0.6f, 1f).Grow(1.6f).Flat().Spin(-140f).Light(Gold, 4f, 2.5f);
                return 0.4f;
            case MoveFxStyle.Thunder:
                for (int i = 0; i < 4; i++)
                    FxPiece.Make("spark_01", Body(user) + Random.insideUnitSphere * 0.3f, Bolt, 0.5f, 0.25f).Angle(Random.Range(0f, 360f)).Flicker().After(i * 0.08f);
                return 0.3f;
            case MoveFxStyle.AimedShot:
                if (target != null)
                    FxPiece.Make("magic_04", Body(target) + Front(), Blood, 1.6f, 0.6f).Grow(0.5f).Spin(90f).Fade(0.2f, 0.3f);
                return 0.45f;
            case MoveFxStyle.BloodRage:
                FxPiece.Make("circle_05", Body(user) + Front(), Blood, 0.6f, 0.5f).Grow(2f).Fade(0.1f, 0.7f).Light(Blood, 4f, 3f);
                return 0.2f;
            case MoveFxStyle.Whirlwind:
            case MoveFxStyle.RecklessSwing:
                FxPiece.Make("smoke_02", Feet(user) + Front(), Dust, 0.6f, 0.5f, false).Grow(1.2f);
                return 0.1f;
            case MoveFxStyle.Wail:
                FxPiece.Make("smoke_10", Body(user) + Front(), Ghost, 0.5f, 0.6f).Grow(1.4f).Spin(-90f);
                return 0.3f;
            case MoveFxStyle.Hex:
                FxPiece.Make("magic_01", Feet(user) + Vector3.up * 0.03f, Ghost, 0.4f, 0.8f).Grow(1.4f).Flat().Spin(-200f).Light(Ghost, 3f, 2f);
                FxPiece.Make("smoke_10", hand, new Color(0.3f, 0.1f, 0.4f, 0.8f), 0.3f, 0.5f, false).Grow(0.8f).Spin(300f);
                return 0.35f;
        }
        return 0f;
    }

    // For OwnsTravel moves: from the user to 'to' (where the shot lands; wide of the target on a miss)
    public static IEnumerator Travel(MoveFxStyle s, CombatUnit user, CombatUnit target, Vector3 to, bool hit, GameObject projectile, float speed)
    {
        switch (s)
        {
            case MoveFxStyle.Thunder:
            case MoveFxStyle.Judgement:
            case MoveFxStyle.DivinePunishment:
                yield return new WaitForSeconds(0.1f);   // it comes out of the sky / the ground, the impact draws it
                break;
            case MoveFxStyle.Volley:
                // Three arrows: one at the target, two at the ground either side, a beat apart
                Vector3 from = Body(user);
                Vector3 side = Vector3.Cross(Vector3.up, to - from).normalized * 0.6f * Tile(user);
                FxRunner.Run(Delayed(0.08f, CombatAnimation.Fly(projectile, from + Vector3.up * 0.1f, to + side, speed)));
                FxRunner.Run(Delayed(0.16f, CombatAnimation.Fly(projectile, from - Vector3.up * 0.05f, to - side, speed)));
                yield return CombatAnimation.Fly(projectile, from, to, speed);
                yield return new WaitForSeconds(0.16f);
                Puff(to + side, 0.5f);
                Puff(to - side, 0.5f);
                break;
            case MoveFxStyle.HolyNova:
                // A ring of light rolling out across the floor
                Vector3 c = Feet(user) + Vector3.up * 0.05f;
                FxPiece.Make("circle_02", c, Holy, 0.4f, 0.55f).Grow(5f * Tile(user)).Flat().Fade(0.05f, 0.5f).Light(Gold, 6f, 4f);
                FxPiece.Make("light_03", c, Gold, 0.4f, 0.5f).Grow(4f * Tile(user)).Flat().Fade(0.05f, 0.7f);
                FxPiece.Make("star_09", Body(user) + Front(), Holy, 0.6f, 0.4f).Grow(2.2f);
                Shake(0.08f);
                yield return new WaitForSeconds(0.3f);
                break;
            case MoveFxStyle.Hex:
                // A dark orb trailing smoke
                Vector3 h0 = Body(user) + Front();
                float time = Mathf.Max(Vector3.Distance(h0, to) / Mathf.Max(speed, 0.1f), 0.15f);
                FxPiece.Make("circle_05", h0, Ghost, 0.45f, time).Move((to - h0) / time).Light(Ghost, 3f, 2.5f).Fade(0.1f, 0f);
                FxPiece.Make("smoke_10", h0, new Color(0.18f, 0.05f, 0.25f, 0.9f), 0.5f, time, false).Move((to - h0) / time).Spin(500f).Fade(0.1f, 0f);
                for (float k = 0.15f; k < 1f; k += 0.15f)
                    FxPiece.Make("smoke_02", Vector3.Lerp(h0, to, k), new Color(0.25f, 0.1f, 0.35f, 0.55f), 0.3f, 0.5f, false).Grow(0.5f).After(time * k);
                yield return new WaitForSeconds(time);
                break;
            case MoveFxStyle.Wail:
                Vector3 g = Body(user) + Front();
                for (int i = 0; i < 3; i++)
                    FxPiece.Make("circle_02", g, Ghost, 0.3f, 0.6f).Grow(3.5f).Fade(0.05f, 0.6f).After(i * 0.12f);
                yield return new WaitForSeconds(0.4f);
                break;
            default:
                yield return CombatAnimation.Fly(projectile, Body(user), to, speed);
                break;
        }
    }

    // Where it lands. 'at' = the point (the target's body, or the floor beside it on a miss).
    // Returns how long the effect wants before the HIT / MISS banner.
    public static float Impact(MoveFxStyle s, CombatUnit user, CombatUnit target, Vector3 at, bool hit)
    {
        Vector3 p = at + Front();
        Vector3 floor = target != null && hit ? Feet(target) + Vector3.up * 0.04f : new Vector3(at.x, Feet(user).y + 0.04f, at.z);
        float tile = Tile(user);
        float a = hit ? 1f : 0.45f;   // a miss is a fainter version
        switch (s)
        {
            // ---------- Kairos ----------
            case MoveFxStyle.SwordSlash:
                Slash("slash_04", p, Steel * a, 1.3f, Random.Range(-35f, 35f));
                if (hit) Spark(p, Steel, 0.6f);
                return 0.15f;
            case MoveFxStyle.Sweep:
                FxPiece.Make("slash_01", Feet(user) + Vector3.up * 0.25f, Steel, 1.6f * tile, 0.35f).Grow(2.8f * tile).Flat().Spin(-720f).Fade(0.05f, 0.6f);
                if (hit) Slash("slash_04", p, Steel, 1.1f, Random.Range(-40f, 40f));
                return 0.2f;
            case MoveFxStyle.SkySword:
                // A blade of light drops out of the sky onto the target
                Vector3 top = p + Vector3.up * 3.5f * tile;
                FxPiece.Make("trace_06", top, Gold, 1.2f, 0.18f).Stretch(1.1f, 3f).Move((p - top) / 0.15f).Fade(0f, 0.3f);
                FxPiece.Make("trace_01", p + Vector3.up * 1.2f * tile, Holy, 1.4f, 0.5f).Stretch(1.2f, 3f).After(0.12f).Fade(0.05f, 0.7f).Light(Gold, 5f, 4f);
                FxPiece.Make("star_09", p, Gold * a, 0.6f, 0.4f).Grow(2.4f).After(0.12f);
                FxPiece.Make("circle_03", floor, Gold, 0.4f, 0.45f).Grow(2.6f * tile).Flat().After(0.12f);
                if (hit) Slash("slash_03", p, Gold, 1.4f, 10f, 0.12f);
                Shake(0.12f, 0.12f);
                return 0.35f;
            case MoveFxStyle.LayOnHands:
                FxPiece.Make("light_01", floor, Gold, 0.4f, 0.9f).Grow(1.8f * tile).Flat().Fade(0.15f, 0.5f).Light(Gold, 4f, 2.5f);
                FxPiece.Make("circle_05", p, Gold, 1.2f, 0.8f).Grow(1.8f).Fade(0.2f, 0.6f);
                Motes(floor, Gold, 9, 1.4f, "star_01");
                return 0.3f;
            case MoveFxStyle.Thunder:
                // Lightning from the sky, a white flash, a shockwave along the floor
                Vector3 sky = p + Vector3.up * 2.2f * tile;
                FxPiece.Make("spark_05", sky, Bolt * a, 1.6f, 0.3f).Stretch(1.4f, 3.2f).Flicker().Light(Bolt, 7f, 6f);
                FxPiece.Make("spark_06", sky + Vector3.right * 0.15f, Holy * a, 1.4f, 0.22f).Stretch(1.2f, 3.2f).Flicker().After(0.05f);
                FxPiece.Make("circle_05", p, Holy, 1f, 0.2f).Grow(3f).Fade(0f, 0.8f);
                FxPiece.Make("circle_03", floor, Bolt, 0.5f, 0.45f).Grow(3.5f * tile).Flat().After(0.05f);
                for (int i = 0; i < 5; i++)
                    FxPiece.Make("spark_01", p + Random.insideUnitSphere * 0.5f, Bolt, 0.6f, 0.2f).Angle(Random.Range(0f, 360f)).Flicker().After(0.05f + i * 0.06f);
                Shake(0.2f, 0.2f);
                return 0.35f;
            case MoveFxStyle.Judgement:
                // A sigil opens under the target, beams of light come down through it, then it bursts
                FxPiece.Make("magic_01", floor, Gold, 0.6f, 0.9f).Grow(2.2f * tile).Flat().Spin(200f).Fade(0.15f, 0.4f).Light(Gold, 6f, 4f);
                for (int i = 0; i < 3; i++)
                {
                    Vector3 off = Quaternion.Euler(0f, i * 120f, 0f) * Vector3.forward * 0.35f * tile;
                    FxPiece.Make("trace_01", p + off + Vector3.up * 1.3f * tile, Holy, 1.1f, 0.45f).Stretch(0.9f, 3f).After(0.15f + i * 0.07f).Fade(0.1f, 0.6f);
                }
                FxPiece.Make("star_09", p, Holy * a, 0.6f, 0.5f).Grow(3.2f).After(0.4f);
                FxPiece.Make("star_06", p, Gold * a, 0.8f, 0.6f).Grow(2.5f).Spin(90f).After(0.4f);
                FxRunner.Run(Delayed(0.4f, ShakeNow(0.22f, 0.25f)));
                return 0.65f;

            // ---------- Lucius ----------
            case MoveFxStyle.HolyFire:
                FxPiece.Make("fire_01", p, Gold * a, 0.5f, 0.45f).Grow(1.4f).Spin(60f).Light(Gold, 4f, 3f);
                FxPiece.Make("star_08", p, Holy * a, 0.4f, 0.3f).Grow(1.3f);
                for (int i = 0; i < 4; i++)
                    FxPiece.Make(i % 2 == 0 ? "flame_05" : "flame_06", p + Random.insideUnitSphere * 0.25f, Gold * a, 0.35f, 0.6f)
                        .Grow(0.15f).Move(Vector3.up * Random.Range(0.8f, 1.4f)).After(i * 0.05f);
                return 0.25f;
            case MoveFxStyle.Heal:
                FxPiece.Make("circle_02", floor, Green, 0.6f, 0.8f).Grow(1.4f * tile).Flat().Move(Vector3.up * 1.2f).Fade(0.1f, 0.6f).Light(Green, 3f, 2f);
                FxPiece.Make("circle_05", p, Green, 1f, 0.6f).Grow(1.5f).Fade(0.2f, 0.6f);
                Motes(floor, Green, 7, 1.2f, "star_01");
                return 0.3f;
            case MoveFxStyle.Bless:
                // A shaft of gold light from above and a star over the head
                FxPiece.Make("trace_06", p + Vector3.up * 1.1f * tile, Gold, 1.6f, 0.9f).Stretch(1.6f, 2.6f).Fade(0.25f, 0.5f).Light(Gold, 4f, 2.5f);
                FxPiece.Make("star_06", Head(target != null ? target : user) + Vector3.up * 0.25f + Front(), Gold, 0.2f, 1f).Grow(0.6f).Spin(60f).Fade(0.2f, 0.4f);
                Motes(p + Vector3.up * 1.4f * tile, Gold, 6, -0.9f, "star_01");
                return 0.35f;
            case MoveFxStyle.DivinePunishment:
                // The ground opens in a sigil, then a pillar of holy fire erupts
                FxPiece.Make("magic_01", floor, Ember * a, 0.5f, 0.9f).Grow(2f * tile).Flat().Spin(-220f).Fade(0.1f, 0.4f);
                FxPiece.Make("muzzle_01", p + Vector3.up * 0.4f * tile, Ember * a, 1.2f, 0.6f).Stretch(1f, 2.4f).After(0.25f).Fade(0.05f, 0.6f).Light(Ember, 6f, 5f);
                FxPiece.Make("muzzle_03", p + Vector3.up * 0.3f * tile, Gold * a, 0.9f, 0.5f).Stretch(1f, 2f).After(0.3f);
                for (int i = 0; i < 7; i++)
                    FxPiece.Make(i % 2 == 0 ? "flame_05" : "flame_06", floor + Random.insideUnitSphere * 0.5f * tile + Front(), Ember * a, 0.5f, 0.7f)
                        .Grow(0.2f).Move(Vector3.up * Random.Range(1.5f, 2.6f)).After(0.25f + i * 0.04f);
                FxPiece.Make("scorch_01", floor, new Color(0.1f, 0.06f, 0.04f, 0.8f), 1.4f * tile, 2.2f, false).Flat().Angle(Random.Range(0f, 360f)).After(0.3f).Fade(0.05f, 0.5f);
                FxPiece.Make("smoke_04", p + Vector3.up * 0.8f, new Color(0.2f, 0.18f, 0.16f, 0.6f), 1f, 1.2f, false).Grow(2f).Move(Vector3.up * 0.6f).After(0.45f);
                FxRunner.Run(Delayed(0.25f, ShakeNow(0.16f, 0.25f)));
                return 0.6f;
            case MoveFxStyle.Rejuvenation:
                FxPiece.Make("twirl_01", p, Leaf, 1f, 1f).Grow(1.4f).Spin(-360f).Fade(0.15f, 0.4f).Light(Leaf, 3f, 2f);
                FxPiece.Make("twirl_03", p + Vector3.up * 0.15f, Green, 0.8f, 1f).Grow(1.2f).Spin(300f).Fade(0.15f, 0.4f);
                Motes(floor, Leaf, 8, 1f, "star_01");
                return 0.35f;
            case MoveFxStyle.ShieldOfFaith:
                // A bubble pops up round the ally and settles
                FxPiece.Make("circle_03", p, Ward, 0.3f, 1f).Grow(1.9f * tile).Fade(0.05f, 0.4f).Light(Ward, 3f, 2f);
                FxPiece.Make("circle_05", p, new Color(Ward.r, Ward.g, Ward.b, 0.35f), 1.5f * tile, 1f).Fade(0.1f, 0.5f);
                FxPiece.Make("magic_03", p, Holy, 0.4f, 0.5f).Grow(1.1f).Spin(180f).After(0.15f);
                return 0.3f;
            case MoveFxStyle.HolyNova:
                FxPiece.Make("star_08", p, Gold * a, 0.4f, 0.35f).Grow(1.2f);
                FxPiece.Make("flame_05", p, Gold * a, 0.4f, 0.5f).Move(Vector3.up * 1.2f);
                return 0.1f;

            // ---------- Theron ----------
            case MoveFxStyle.Arrow:
                if (hit) Spark(p, Steel, 0.5f); else Puff(floor, 0.5f);
                return 0.1f;
            case MoveFxStyle.AimedShot:
                FxPiece.Make("star_09", p, Steel * a, 0.5f, 0.35f).Grow(1.8f).Light(Steel, 3f, 2f);
                FxPiece.Make("trace_04", p, Blood * a, 1f, 0.3f).Angle(90f).Stretch(0.6f, 1.6f);
                if (!hit) Puff(floor, 0.6f);
                return 0.2f;
            case MoveFxStyle.PinningShot:
                if (hit)
                {
                    Spark(p, Steel, 0.5f);
                    FxPiece.Make("circle_03", floor, Steel, 1.1f * tile, 0.6f).Grow(0.5f * tile).Flat().Fade(0.1f, 0.5f);   // pinned to the spot
                    FxPiece.Make("scratch_01", floor + Front() * 2f, Steel, 0.6f, 0.6f).Angle(45f);
                }
                else Puff(floor, 0.5f);
                return 0.2f;
            case MoveFxStyle.Volley:
                if (hit) Spark(p, Steel, 0.5f);
                Puff(floor, 0.7f);
                return 0.15f;
            case MoveFxStyle.Knife:
                Slash("scratch_01", p, Steel * a, 0.8f, 0f);
                Slash("scratch_01", p, Steel * a, 0.8f, 90f, 0.08f);
                return 0.15f;

            // ---------- Magnus ----------
            case MoveFxStyle.AxeCleave:
                Slash("slash_03", p, new Color(1f, 0.6f, 0.45f) * a, 1.6f, -20f);
                if (hit) { Blood_(p, 6); Puff(floor, 0.9f); Shake(0.1f); }
                return 0.2f;
            case MoveFxStyle.RecklessSwing:
                Slash("slash_01", p, Blood * a, 1.9f, 25f);
                Slash("slash_04", p, Steel * a, 1.6f, -40f, 0.06f);
                if (hit) { Blood_(p, 9); Puff(floor, 1.2f); Shake(0.15f, 0.18f); }
                return 0.25f;
            case MoveFxStyle.Whirlwind:
                FxPiece.Make("twirl_01", Feet(user) + Vector3.up * 0.3f, Steel, 1.6f * tile, 0.45f).Grow(2.8f * tile).Flat().Spin(-900f).Fade(0.05f, 0.5f);
                FxPiece.Make("twirl_03", Body(user) + Front(), Steel, 1.4f, 0.45f).Grow(2.2f).Spin(-1080f);
                Puff(Feet(user), 1.6f);
                if (hit) Slash("slash_04", p, Steel, 1.1f, Random.Range(-40f, 40f));
                return 0.25f;
            case MoveFxStyle.BloodRage:
                for (int i = 0; i < 14; i++)
                    FxPiece.Make(i % 2 == 0 ? "flame_05" : "flame_06", Feet(user) + Random.insideUnitSphere * 0.35f * tile + Front(), Blood, 0.5f, 0.7f)
                        .Grow(0.2f).Move(Vector3.up * Random.Range(1f, 2f)).After(i * 0.06f);
                FxPiece.Make("circle_02", Feet(user) + Vector3.up * 0.05f, Blood, 0.4f, 0.6f).Grow(2.4f * tile).Flat().Light(Blood, 4f, 3f);
                Shake(0.08f, 0.3f);
                return 0.6f;

            // ---------- Anyone ----------
            case MoveFxStyle.Help:
                FxPiece.Make("circle_05", p, Holy, 0.8f, 0.6f).Grow(1.3f).Fade(0.2f, 0.6f);
                Motes(floor, Holy, 5, 1f, "star_01");
                return 0.2f;
            case MoveFxStyle.FireFlask:
                FxPiece.Make("fire_01", p, Ember * a, 0.6f, 0.5f).Grow(1.8f).Spin(-80f).Light(Ember, 5f, 4f);
                for (int i = 0; i < 6; i++)
                    FxPiece.Make(i % 2 == 0 ? "flame_05" : "flame_06", floor + Random.insideUnitSphere * 0.5f + Front(), Ember, 0.4f, 0.8f)
                        .Grow(0.15f).Move(Vector3.up * Random.Range(0.6f, 1.4f)).After(i * 0.05f);
                FxPiece.Make("scorch_01", floor, new Color(0.08f, 0.05f, 0.03f, 0.75f), 1.2f * tile, 2f, false).Flat().Angle(Random.Range(0f, 360f)).Fade(0.05f, 0.5f);
                Shake(0.08f);
                return 0.3f;
            case MoveFxStyle.HolyWater:
                FxPiece.Make("circle_03", floor, Ward, 0.3f, 0.5f).Grow(1.8f * tile).Flat();
                FxPiece.Make("light_01", p, Holy * a, 0.5f, 0.5f).Grow(1.5f).Light(Ward, 3f, 3f);
                for (int i = 0; i < 6; i++)
                    FxPiece.Make("circle_05", p, Ward, 0.15f, 0.6f).Move(new Vector3(Random.Range(-1f, 1f), Random.Range(1.5f, 2.5f), Random.Range(-1f, 1f)), 6f);
                FxPiece.Make("smoke_02", p, new Color(1f, 1f, 1f, 0.5f), 0.6f, 1f, false).Grow(1.4f).Move(Vector3.up * 0.7f).After(0.15f);
                return 0.25f;
            case MoveFxStyle.Elixir:
                FxPiece.Make("twirl_03", p, Gold, 1f, 0.8f).Grow(1.5f).Spin(-500f).Light(Ember, 3f, 2f);
                Motes(floor, Ember, 7, 1.4f, "star_01");
                return 0.3f;

            // ---------- Monsters ----------
            case MoveFxStyle.Bite:
                Slash("scratch_01", p, Blood * a, 0.8f, 100f);
                Slash("scratch_01", p, Blood * a, 0.8f, -80f, 0.05f);
                if (hit) Blood_(p, 4);
                return 0.12f;
            case MoveFxStyle.Claw:
                Slash("scratch_01", p, new Color(0.9f, 0.85f, 0.8f) * a, 1.1f, Random.Range(-30f, 30f));
                if (hit) Blood_(p, 4);
                return 0.12f;
            case MoveFxStyle.VenomBite:
                Slash("scratch_01", p, Venom * a, 0.8f, 100f);
                if (hit)
                    for (int i = 0; i < 6; i++)
                        FxPiece.Make("circle_05", p, Venom, 0.15f, 0.7f).Move(new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(0.5f, 1.5f), Random.Range(-0.8f, 0.8f)), 3f);
                return 0.12f;
            case MoveFxStyle.Wail:
                FxPiece.Make("smoke_07", p, Ghost * a, 0.6f, 0.8f, false).Grow(1.4f).Spin(120f);
                if (hit) FxPiece.Make("spark_02", p, Ghost, 0.8f, 0.3f).Flicker();
                return 0.2f;
            case MoveFxStyle.Drain:
                Slash("scratch_01", p, Blood * a, 0.9f, 80f);
                if (hit && user != null)
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 from = p + Random.insideUnitSphere * 0.3f;
                        FxPiece.Make("circle_05", from, Blood, 0.18f, 0.45f).Move((Body(user) - from) / 0.45f).After(0.1f + i * 0.05f);
                    }
                return 0.3f;
            case MoveFxStyle.Slam:
                FxPiece.Make("circle_03", floor, Dust, 0.4f, 0.4f).Grow(2.2f * tile).Flat();
                Puff(floor, 1.2f);
                if (hit) { Spark(p, Steel, 0.7f); Shake(0.12f); }
                return 0.15f;
            case MoveFxStyle.Acid:
                FxPiece.Make("smoke_10", p, Venom * a, 0.4f, 0.6f).Grow(1.3f).Spin(60f);
                for (int i = 0; i < 6; i++)
                    FxPiece.Make("circle_05", p, Venom, 0.18f, 0.7f).Move(new Vector3(Random.Range(-1f, 1f), Random.Range(1f, 2f), Random.Range(-1f, 1f)), 5f);
                return 0.15f;
            case MoveFxStyle.Hex:
                FxPiece.Make("smoke_10", p, new Color(0.3f, 0.08f, 0.4f, 0.85f) * a, 0.4f, 0.6f, false).Grow(1.5f).Spin(-200f);
                FxPiece.Make("star_06", p, Ghost * a, 0.4f, 0.4f).Grow(1.3f).Spin(120f);
                return 0.15f;
            case MoveFxStyle.Bone:
                Slash("slash_04", p, new Color(0.9f, 0.88f, 0.78f) * a, 1.1f, Random.Range(-30f, 30f));
                if (hit) Spark(p, Steel, 0.5f);
                return 0.12f;
        }
        return 0f;
    }

    // The smaller hit on everyone else caught in an area
    public static void Splash(MoveFxStyle s, CombatUnit user, CombatUnit other)
    {
        if (other == null) return;
        Vector3 p = Body(other) + Front();
        switch (s)
        {
            case MoveFxStyle.DivinePunishment:
            case MoveFxStyle.FireFlask:
            case MoveFxStyle.HolyFire:
                FxPiece.Make("fire_01", p, s == MoveFxStyle.HolyFire ? Gold : Ember, 0.4f, 0.4f).Grow(1f).Light(Ember, 3f, 2f);
                break;
            case MoveFxStyle.Volley:
            case MoveFxStyle.Arrow:
                Spark(p, Steel, 0.4f);
                break;
            case MoveFxStyle.HolyNova:
            case MoveFxStyle.Judgement:
                FxPiece.Make("star_08", p, Gold, 0.4f, 0.35f).Grow(1.2f);
                break;
            case MoveFxStyle.Thunder:
                FxPiece.Make("spark_02", p, Bolt, 0.7f, 0.25f).Flicker();
                break;
            default:
                Slash("slash_04", p, Steel, 1f, Random.Range(-40f, 40f));
                break;
        }
    }

    // ================= Building blocks =================

    static void Slash(string sprite, Vector3 p, Color c, float size, float angle, float after = 0f)
    {
        c.a = Mathf.Clamp01(c.a);
        FxPiece.Make(sprite, p, c, size * 0.8f, 0.25f).Grow(size * 1.15f).Angle(angle).Spin(angle >= 0f ? -160f : 160f).Fade(0.05f, 0.55f).After(after);
    }

    static void Spark(Vector3 p, Color c, float size)
    {
        FxPiece.Make("star_08", p, c, size * 0.5f, 0.22f).Grow(size * 1.3f).Angle(Random.Range(0f, 90f));
    }

    static void Puff(Vector3 floor, float size)
    {
        FxPiece.Make("dirt_02", floor + Vector3.up * 0.15f + Front(), Dust, size * 0.5f, 0.5f, false).Grow(size).Angle(Random.Range(0f, 360f)).Move(Vector3.up * 0.3f).Fade(0.05f, 0.7f);
        FxPiece.Make("smoke_02", floor + Vector3.up * 0.2f + Front(), new Color(Dust.r, Dust.g, Dust.b, 0.45f), size * 0.4f, 0.7f, false).Grow(size * 1.1f).Move(Vector3.up * 0.4f);
    }

    static void Blood_(Vector3 p, int count)
    {
        for (int i = 0; i < count; i++)
            FxPiece.Make("circle_05", p, new Color(0.6f, 0.03f, 0.03f), Random.Range(0.08f, 0.16f), 0.6f, false)
                .Move(new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(0.8f, 2f), Random.Range(-1.2f, 1.2f)), 7f).Fade(0f, 0.3f);
    }

    // Little lights drifting up (rise > 0) or down (rise < 0)
    static void Motes(Vector3 floor, Color c, int count, float rise, string sprite)
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 at = floor + new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(0f, 0.5f), Random.Range(-0.4f, 0.4f)) + Front();
            FxPiece.Make(sprite, at, c, Random.Range(0.2f, 0.35f), Random.Range(0.7f, 1.1f)).Move(Vector3.up * rise * Random.Range(0.7f, 1.2f)).After(i * 0.05f).Fade(0.2f, 0.5f);
        }
    }

    static void Shake(float strength, float time = 0.15f)
    {
        if (CameraController.Main != null) CameraController.Main.Shake(strength, time);
    }

    static IEnumerator ShakeNow(float strength, float time)
    {
        Shake(strength, time);
        yield break;
    }

    static IEnumerator Delayed(float seconds, IEnumerator then)
    {
        yield return new WaitForSeconds(seconds);
        yield return then;
    }

    // ---------- Where things are on a unit ----------

    static float Tile(CombatUnit u) => u != null ? u.tileSize : 1f;

    // A little toward the camera, so the effect draws over the sprite instead of inside it
    static Vector3 Front()
    {
        Camera cam = Camera.main;
        return cam != null ? -cam.transform.forward * 0.25f : Vector3.zero;
    }

    // The body sprite (the one the SpriteAnimator drives, not a floating sword or book)
    static SpriteRenderer BodySprite(CombatUnit u)
    {
        SpriteAnimator anim = u.GetComponentInChildren<SpriteAnimator>();
        SpriteRenderer sr = anim != null ? anim.GetComponent<SpriteRenderer>() : u.GetComponentInChildren<SpriteRenderer>();
        return sr != null && sr.sprite != null ? sr : null;
    }

    public static Vector3 Body(CombatUnit u)
    {
        if (u == null) return Vector3.zero;
        SpriteRenderer sr = BodySprite(u);
        return sr != null ? sr.bounds.center : u.transform.position + Vector3.up * 0.6f * u.tileSize;
    }

    static Vector3 Feet(CombatUnit u)
    {
        if (u == null) return Vector3.zero;
        SpriteRenderer sr = BodySprite(u);
        if (sr == null) return u.transform.position;
        Bounds b = sr.bounds;
        return new Vector3(b.center.x, b.min.y, b.center.z);
    }

    static Vector3 Head(CombatUnit u)
    {
        if (u == null) return Vector3.zero;
        SpriteRenderer sr = BodySprite(u);
        if (sr == null) return u.transform.position + Vector3.up * 1.2f * u.tileSize;
        Bounds b = sr.bounds;
        return new Vector3(b.center.x, b.max.y, b.center.z);
    }
}

// Runs effect coroutines that outlive whoever started them
public class FxRunner : MonoBehaviour
{
    static FxRunner instance;

    public static void Run(IEnumerator routine)
    {
        if (instance == null)
        {
            var go = new GameObject("Fx Runner");
            instance = go.AddComponent<FxRunner>();
        }
        instance.StartCoroutine(routine);
    }
}
