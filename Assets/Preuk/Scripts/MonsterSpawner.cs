using System.Collections;
using UnityEngine;

// Makes a monster at runtime with its own stats: the boss's brood, a spider jumping out of a coffin.
// It is built switched off, so the stats are in place before CombatUnit.Awake reads maxHP.
public static class MonsterSpawner
{
    [System.Serializable]
    public struct Stats
    {
        public string name;
        public int hp, armorClass, attackBonus, diceSides, damageBonus;
        public Color tint;
        [Tooltip("Size of the sprite compared to the prefab's (0.7 = smaller).")]
        public float spriteScale;
    }

    public static CombatUnit Spawn(GameObject prefab, Stats s, Vector3 position)
    {
        if (prefab == null) return null;
        var holder = new GameObject("(spawning)");
        holder.SetActive(false);
        GameObject go = Object.Instantiate(prefab, position, Quaternion.identity, holder.transform);
        go.name = s.name;
        CombatUnit u = go.GetComponent<CombatUnit>();
        if (u == null) { Object.Destroy(holder); return null; }
        u.unitName = s.name;
        u.team = Team.Enemy;
        u.size = 1;
        u.maxHP = s.hp;
        u.armorClass = s.armorClass;
        u.attackBonus = s.attackBonus;
        u.damageDiceCount = 1;
        u.damageDiceSides = s.diceSides;
        u.damageBonus = s.damageBonus;
        if (go.TryGetComponent(out EnemyAI ai)) { ai.style = AttackStyle.Melee; ai.attackRange = 1; ai.meleeVerb = "bites"; }
        foreach (SpriteRenderer sr in go.GetComponentsInChildren<SpriteRenderer>(true))
        {
            sr.color = s.tint;
            if (s.spriteScale > 0f) sr.transform.localScale *= s.spriteScale;
        }
        go.transform.SetParent(null, true);
        Object.Destroy(holder);
        go.SetActive(true);
        return u;
    }

    // Grows up out of the floor
    public static IEnumerator RiseFromFloor(Transform t)
    {
        if (t == null) yield break;
        Vector3 full = t.localScale;
        yield return Tween.Over(0.35f, k => { if (t != null) t.localScale = full * Mathf.Max(0.05f, k); });
        if (t != null) t.localScale = full;
    }
}
