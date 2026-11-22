using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// After picking something in the ACTION or BONUS ACTION menu: shows what it can reach,
// waits for you to click a target, then rolls and applies it.
//   Attacks: d20 + bonus vs the target's AC, rolled on the real dice (DiceTray).
//            Natural 20 = critical hit (damage dice twice). Natural 1 = always a miss.
//            Disadvantage (roll twice, keep the lower): the target is hidden in the dark,
//            or it's a ranged attack with an enemy right next to you.
//   Heals and items: no attack roll, just roll how much.
// Right-click or Esc cancels while aiming. Lives on the Combat object.
[RequireComponent(typeof(TurnManager))]
public class CombatTargeting : MonoBehaviour
{
    [Header("Highlight")]
    public Color enemyColor = Palette.Blood;
    public Color allyColor = new Color32(143, 176, 120, 255);  // pale green
    [Tooltip("Tiles inside the action's reach.")]
    [Range(0f, 1f)] public float rangeStrength = 0.12f;
    [Tooltip("Tiles of the targets you can hit.")]
    [Range(0f, 1f)] public float targetStrength = 0.45f;

    [Header("Effects")]
    [Tooltip("Default for ranged actions that have no Projectile of their own. Empty = plain ball.")]
    public GameObject rangedProjectile;
    [Tooltip("Tiles per second.")]
    public float projectileSpeed = 12f;
    [Tooltip("How high above the floor spells fly (about chest height).")]
    public float projectileHeight = 0.8f;

    TurnManager turns;
    CombatManager combat;
    CombatMovement movement;
    PartyInventory inventory;
    GridManager Grid => combat.grid;

    CombatUnit user;
    CombatAction action;   // a move, or the move an Action item performs (then 'item' is set too)
    ItemData item;         // set when an item is used

    // Heal / Cure items follow the item's own rules; everything else follows 'action'
    bool ItemRules => item != null && action == null;
    bool aiming;
    bool busy;

    public bool IsActive => aiming || busy;   // CombatMovement stands still while true
    public bool IsBusy => busy;               // the dice are rolling
    public bool IsAiming => aiming;           // waiting for you to click a target
    public string AimingName => aiming ? Name : "";   // e.g. "Holy Fireball"

    void Awake()
    {
        turns = GetComponent<TurnManager>();
        combat = GetComponent<CombatManager>();
        movement = GetComponent<CombatMovement>();
        inventory = FindAnyObjectByType<PartyInventory>();
    }

    // ================= Start / stop aiming =================

    public void BeginAction(CombatUnit who, CombatAction what)
    {
        if (busy) return;   // still resolving the last one
        Cancel();
        user = who; action = what; item = null;
        StartAiming();
    }

    public void BeginItem(CombatUnit who, ItemData what)
    {
        if (busy) return;
        Cancel();
        user = who; item = what;
        action = what.effect == ItemEffect.Action ? what.actsAs : null;   // a thrown flask aims like a spell
        StartAiming();
    }

    public void Cancel()
    {
        if (!aiming) return;
        aiming = false;
        if (Grid != null)
        {
            Grid.OnTileClicked -= OnTileClicked;
            Grid.ClearMarks();
        }
        if (movement != null) movement.Refresh();
        turns.NotifyChanged();   // the battle UI comes back
    }

    void StartAiming()
    {
        aiming = true;
        Grid.ClearMarks();   // e.g. the walking path preview
        Grid.OnTileClicked += OnTileClicked;
        if (movement != null) movement.Refresh();   // hide the walking range while aiming

        // Dim: everywhere it can reach. Bright: the targets you can actually hit (that you can see).
        Color color = Target == ActionTarget.Enemy ? enemyColor : allyColor;
        var reach = new List<Vector2Int>();
        for (int x = 0; x < Grid.Width; x++)
            for (int y = 0; y < Grid.Height; y++)
                if (Grid.GetTile(x, y) != null && user.ReachTo(Grid.OriginTile + new Vector2Int(x, y)) <= Range)
                    reach.Add(new Vector2Int(x, y));
        Grid.MarkTiles(reach, color, rangeStrength);

        var hits = new List<Vector2Int>();
        foreach (CombatUnit t in Candidates())
            if (CanReach(t) && FogOfWar.CanSee(t))
                foreach (Vector2Int w in t.GetOccupiedTiles())
                    hits.Add(w - Grid.OriginTile);
        Grid.MarkTiles(hits, color, targetStrength);

        Debug.Log($"{user.unitName}: choose a target for {Name}  (right-click / Esc to cancel)");
        turns.NotifyChanged();   // the battle UI hides, the cancel X shows
    }

    void Update()
    {
        if (aiming && PauseMenu.CancelPressed) Cancel();
    }

    void OnTileClicked(Vector2Int gridTile)
    {
        if (!aiming || busy) return;
        Vector2Int world = Grid.OriginTile + gridTile;

        CombatUnit target = null;
        foreach (CombatUnit c in Candidates())
            if (c.Occupies(world)) { target = c; break; }

        if (target == null) { Debug.Log("No target there."); return; }
        if (user.ReachTo(target) > Range) { Debug.Log($"{TargetName(target)} is out of reach."); return; }
        if (!HasLineOfSight(target)) { Debug.Log("Something is in the way."); return; }

        StartCoroutine(Resolve(target));
    }

    // ================= Doing it =================

    IEnumerator Resolve(CombatUnit target)
    {
        Cancel();
        busy = true;
        if (movement != null) movement.Refresh();

        if (item != null) Sfx.Play("bag", 0.8f);
        if (action != null) Sfx.ForAction(action);

        // An Action item is used up as it's thrown (a Heal / Cure item in UseItem)
        if (item != null && !ItemRules && (inventory == null || !inventory.Remove(item)))
        {
            Debug.Log($"No {item.displayName} left.");
            busy = false;
            yield break;
        }

        // Casting flourish, a moment before the dice: the move's own (MoveFx), or the old prefab
        if (action != null && action.fx != MoveFxStyle.None)
        {
            float wait = MoveFx.Cast(action.fx, user, target);
            if (wait > 0f) yield return new WaitForSeconds(wait);
        }
        else if (action != null && action.castEffect != null)
        {
            // Attached to the cast point (the book), so the sparkles float along with it
            Transform at = user.castPoint != null ? user.castPoint : user.transform;
            Instantiate(action.castEffect, at.position, Quaternion.identity, at);
            yield return new WaitForSeconds(0.3f);
        }

        if (ItemRules) yield return UseItem(target);
        else if (action.effect == ActionEffect.Damage) yield return Attack(target);
        else if (action.effect == ActionEffect.Buff) yield return Buff(target);
        else if (action.effect == ActionEffect.Help) yield return Help(target);
        else yield return Heal(action.displayName, action.diceCount, action.diceSides, action.bonus, target);

        if (action != null && item == null) turns.SpendUse(user, action);   // an item's move has no uses to count
        if (item != null || action.bonusAction) turns.MarkBonusUsed();   // potions and bonus spells
        else turns.MarkActed();
        busy = false;
        if (movement != null) movement.Refresh();
        turns.CheckForVictory();
    }

    IEnumerator Attack(CombatUnit target)
    {
        bool hidden = !FogOfWar.CanSee(target);
        bool ranged = action.range > 1;
        bool crowded = ranged && EnemyNextTo(user);
        bool hampered = hidden || crowded;
        bool favoured = action.advantage;
        var reasons = new List<string>();
        if (hidden) reasons.Add("can't see the target");
        if (crowded) reasons.Add("enemy right next to you");
        StatusRules.Edge(user, target, !ranged, ref favoured, ref hampered, reasons);   // poisoned, restrained, prone...
        RollMode mode = StatusRules.Mode(favoured, hampered);   // advantage and disadvantage cancel out
        string why = string.Join(", ", reasons);
        string tName = TargetName(target);
        int need = AttackRoll.Needed(target.ArmorClass, action.attackBonus);

        // 1. Roll to hit (twice with advantage or disadvantage)
        var roll = new AttackRoll();
        yield return roll.Throw(
            $"{action.displayName}  —  need {need}+"
                + (mode == RollMode.Disadvantage ? "   (disadvantage)" : mode == RollMode.Advantage ? "   (advantage)" : "")
                + (user.IsBlessed ? "   (blessed)" : ""),
            mode,
            mode == RollMode.Disadvantage ? $"Disadvantage: {why}  —  second roll" : reasons.Count > 0 ? $"Advantage: {why}  —  second roll" : "Advantage  —  second roll");
        string rollText = roll.Text;

        // 2. Hit or miss? (worked out first, so a missed spell can fly wide)
        // Bless: add the blessing die (a d4) and bonus to the roll
        int blessRoll = user.RollBless();
        if (blessRoll > 0) rollText += $" + bless ({blessRoll})";
        int bane = StatusRules.BaneRoll(user);
        if (bane > 0) rollText += $" - bane ({bane})";
        int total = roll.Value + action.attackBonus + blessRoll - bane;
        bool hit = roll.Hits(total, target.ArmorClass);

        if (action.glowSword) StartCoroutine(SwordGlow(user, action.glowColor, 2.4f));

        // 3. Show the swing / shot
        if (ranged) yield return Shoot(target, hit);
        else
        {
            yield return CombatAnimation.Swing(user, target, action);
            if (action.fx != MoveFxStyle.None)
            {
                float wait = MoveFx.Impact(action.fx, user, target, MoveFx.Body(target), hit);
                if (wait > 0f) yield return new WaitForSeconds(wait);
            }
        }

        string head = $"{user.unitName} uses {action.displayName} on {tName}: {rollText} + {action.attackBonus} = {total} vs AC {target.ArmorClass}";

        if (!hit)
        {
            Debug.Log($"{head} — miss{(roll.Value == 1 ? " (natural 1)" : "")}");
            yield return DiceTray.ShowOutcome(roll.MissText, false);
            yield break;
        }

        // 4. Damage (a critical hit rolls every damage die twice)
        int times = roll.DiceTimes;
        int damage = DiceSimulator.Roll(action.diceCount * times, action.diceSides) + action.bonus;
        if (action.extraDiceCount > 0) damage += DiceSimulator.Roll(action.extraDiceCount * times, action.extraDiceSides);
        damage = Mathf.Max(damage, 1);

        Debug.Log($"{head} — {(roll.IsCrit ? "CRITICAL HIT" : "hit")}! {damage} {action.damageType} damage");
        target.TakeDamage(damage);
        string killed = target.IsAlive ? "" : "  —  IT FALLS";

        // 5. Burn / paralyse (Holy Fireball, Divine Smite)
        if (target.TryAddStatus(action.inflicts, action.statusTurns, action.statusChance, action.statusDiceCount, action.statusDiceSides))
        {
            killed = "  —  " + StatusInfo.Word(action.inflicts).ToUpper();
            Debug.Log($"{tName} is {StatusInfo.Word(action.inflicts)} for {action.statusTurns} turn(s)");
        }
        // 6. Area: a splash round the target, or a sweep round the user. The same roll against each one's AC.
        string extra = "";
        if (action.area != ActionArea.Single)
            foreach (CombatUnit other in AreaTargets(target))
            {
                if (!roll.Hits(total, other.ArmorClass)) continue;
                int d = Mathf.Max(damage * action.areaDamagePercent / 100, 1);
                if (action.fx != MoveFxStyle.None) MoveFx.Splash(action.fx, user, other);
                other.TakeDamage(d);
                other.TryAddStatus(action.inflicts, action.statusTurns, action.statusChance, action.statusDiceCount, action.statusDiceSides);
                extra += $"   +{TargetName(other)} {d}";
                Debug.Log($"{action.displayName} also hits {other.unitName} for {d}");
            }
        yield return DiceTray.ShowOutcome(roll.HitText(damage) + killed + extra, true);
    }

    IEnumerator Heal(string what, int count, int sides, int bonus, CombatUnit target)
    {
        int amount = Mathf.Max(DiceSimulator.Roll(count, sides) + bonus, 0);
        int before = target.CurrentHP;
        target.Heal(amount);
        if (action != null && action.fx != MoveFxStyle.None) MoveFx.Impact(action.fx, user, target, MoveFx.Body(target), true);
        else if (action != null && action.impactPrefab != null) SpawnEffect(action.impactPrefab, target.transform);
        int healed = target.CurrentHP - before;
        // Lay on Hands: healing a friend heals you a bit too
        string selfText = "";
        if (action != null && action.selfHealFraction > 0f && target != user && user.IsAlive)
        {
            int selfAmount = Mathf.RoundToInt(amount * action.selfHealFraction);
            int b2 = user.CurrentHP;
            user.Heal(selfAmount);
            if (action.fx != MoveFxStyle.None) MoveFx.Impact(action.fx, user, user, MoveFx.Body(user), true);
            else if (action.impactPrefab != null) SpawnEffect(action.impactPrefab, user.transform);
            selfText = $"   +{user.CurrentHP - b2} HP  {user.unitName}";
        }
        Debug.Log($"{user.unitName} uses {what} on {target.unitName}: {count}d{sides}+{bonus} = {amount} -> healed {healed} ({target.CurrentHP}/{target.maxHP} HP)");
        yield return DiceTray.ShowMessage(what, $"+{healed} HP  {target.unitName}" + selfText, true);
    }

    // Bless, Shield of Faith...: no roll, the blessing lasts until the fight ends
    IEnumerator Buff(CombatUnit target)
    {
        var who = new List<CombatUnit>();
        if (action.wholeParty)
        {
            foreach (CombatUnit u in Candidates())
                if (CanReach(u)) who.Add(u);   // everyone in range
        }
        else who.Add(target);

        foreach (CombatUnit u in who)
        {
            u.AddBuff(action.blessDie, action.bonusAC, action.blessBonus);
            if (action.regenTurns > 0) u.AddStatus(StatusType.Regen, action.regenTurns, action.regenDiceCount, action.regenDiceSides);
            if (action.fx != MoveFxStyle.None) MoveFx.Impact(action.fx, user, u, MoveFx.Body(u), true);
            else if (action.impactPrefab != null) SpawnEffect(action.impactPrefab, u.transform);
        }

        string names = string.Join(" & ", who.ConvertAll(u => u.unitName));
        Debug.Log($"{user.unitName} uses {action.displayName}: {names} get {action.BuffText} until the fight ends");
        turns.NotifyChanged();   // the turn panel shows "Blessed" / the new AC
        yield return DiceTray.ShowMessage(action.displayName, $"{names}   {action.BuffText}", true);
    }

    // Help: no roll, a downed ally is back on their feet with 1 HP
    IEnumerator Help(CombatUnit target)
    {
        target.Revive(1);
        if (action.fx != MoveFxStyle.None) MoveFx.Impact(action.fx, user, target, MoveFx.Body(target), true);
        Debug.Log($"{user.unitName} helps {target.unitName} back up (1 HP)");
        turns.NotifyChanged();
        yield return DiceTray.ShowMessage(action.displayName, $"{target.unitName} is back on their feet  (1 HP)", true);
    }

    IEnumerator UseItem(CombatUnit target)
    {
        if (inventory == null || !inventory.Remove(item))
        {
            Debug.Log($"No {item.displayName} left.");
            yield break;
        }
        if (item.effect == ItemEffect.Heal)
            yield return Heal(item.displayName, item.diceCount, item.diceSides, item.bonus, target);
        else if (item.effect == ItemEffect.Cure)
            yield return DiceTray.ShowMessage(item.displayName, item.ApplyTo(target), true);
        else
            Debug.Log($"{user.unitName} uses {item.displayName}.");
    }

    // ================= Rules =================

    ActionTarget Target => ItemRules ? item.target : action.target;
    int Range => ItemRules ? item.range : action.range;
    string Name => item != null ? item.displayName : action.displayName;
    bool NeedsSight => !ItemRules && action.needsLineOfSight;

    // Who could this be aimed at at all?
    IEnumerable<CombatUnit> Candidates()
    {
        if (Target == ActionTarget.Self) { yield return user; yield break; }
        var list = Target == ActionTarget.Enemy ? combat.Enemies : combat.Party;
        foreach (CombatUnit u in list)
            if (u != null && CanTarget(u)) yield return u;
    }

    // Help only reaches the downed; heals reach the standing and the downed; everything else the standing
    bool CanTarget(CombatUnit u)
    {
        if (action != null && action.effect == ActionEffect.Help) return u.IsDowned;
        if (Heals) return u.IsAlive || u.IsDowned;
        return u.IsAlive;
    }

    bool Heals => ItemRules ? item.effect == ItemEffect.Heal || item.effect == ItemEffect.Cure : action.effect == ActionEffect.Heal;

    bool CanReach(CombatUnit t) => user.ReachTo(t) <= Range && HasLineOfSight(t);

    bool HasLineOfSight(CombatUnit t)
    {
        if (!NeedsSight || t == user) return true;
        return !Physics.Linecast(user.transform.position, t.transform.position, Grid.obstacleLayers, QueryTriggerInteraction.Ignore);
    }

    bool EnemyNextTo(CombatUnit u)
    {
        foreach (CombatUnit e in combat.Enemies)
            if (e != null && e.IsAlive && e.ReachTo(u.AnchorTile) <= 1) return true;
        return false;
    }

    // Enemies caught in an area: near the target (splash) or near the user (sweep)
    List<CombatUnit> AreaTargets(CombatUnit main)
    {
        var list = new List<CombatUnit>();
        foreach (CombatUnit e in combat.Enemies)
        {
            if (e == null || !e.IsAlive || e == main) continue;
            CombatUnit centre = action.area == ActionArea.AroundUser ? user : main;
            if (centre.ReachTo(e) <= action.areaRadius) list.Add(e);
        }
        return list;
    }

    // Divine Smite: the floating sword glows gold (tinted, a halo and a light) for a moment
    IEnumerator SwordGlow(CombatUnit who, Color color, float seconds)
    {
        SpriteRenderer sword = null;
        foreach (SpriteRenderer sr in who.GetComponentsInChildren<SpriteRenderer>())
            if (sr.name == "Sword") { sword = sr; break; }
        if (sword == null) yield break;
        Color baseColor = sword.color;
        Light glowLight = SceneLights.NewGlow("Sword Glow", sword.transform, color, 2.5f);
        var halo = new GameObject("Sword Halo", typeof(SpriteRenderer));
        halo.transform.SetParent(sword.transform, false);
        halo.transform.localPosition = new Vector3(0f, 0f, 0.01f);
        halo.transform.localScale = Vector3.one * 1.35f;
        var hr = halo.GetComponent<SpriteRenderer>();
        hr.sharedMaterial = sword.sharedMaterial;
        hr.sortingOrder = sword.sortingOrder - 1;
        for (float t = 0f; t < seconds && sword != null; t += Time.deltaTime)
        {
            float k = Mathf.Min(t / 0.2f, 1f) * Mathf.Clamp01((seconds - t) / 0.4f);   // fade in, fade out
            float pulse = 0.8f + 0.2f * Mathf.Sin(t * 18f);
            sword.color = Color.Lerp(baseColor, color, 0.6f * k);
            hr.sprite = sword.sprite;
            hr.color = new Color(color.r, color.g, color.b, 0.55f * k * pulse);
            glowLight.intensity = 3f * k * pulse;
            yield return null;
        }
        if (sword != null) sword.color = baseColor;
        Destroy(glowLight.gameObject);
        Destroy(halo);
    }

    // A one-off effect (it should destroy itself when done, like the sparkle bursts)
    static void SpawnEffect(GameObject prefab, Transform at)
    {
        Instantiate(prefab, at.position, Quaternion.identity);
    }

    static string TargetName(CombatUnit t) => FogOfWar.CanSee(t) ? t.unitName : "something in the dark";

    // ================= Animation (the swing and the flight itself are in CombatAnimation) =================

    // Ranged: fly the action's projectile at the target, then play its impact effect.
    // A miss flies wide and bursts on the floor beside the target.
    IEnumerator Shoot(CombatUnit target, bool hit)
    {
        Vector3 lift = Vector3.up * projectileHeight;
        Vector3 a = user.transform.position + lift;
        Vector3 b = CombatAnimation.ShotEnd(a, target.transform.position + lift, hit, user.tileSize);

        GameObject prefab = action.projectilePrefab != null ? action.projectilePrefab : rangedProjectile;
        if (action.fx != MoveFxStyle.None)
        {
            // The move's own look: its way there (lightning, a rain of arrows, or the projectile), then its impact
            if (MoveFx.OwnsTravel(action.fx)) yield return MoveFx.Travel(action.fx, user, target, b, hit, prefab, projectileSpeed * user.tileSize);
            else yield return CombatAnimation.Fly(prefab, a, b, projectileSpeed * user.tileSize);
            float wait = MoveFx.Impact(action.fx, user, target, b, hit);
            if (wait > 0f) yield return new WaitForSeconds(wait);
            yield break;
        }
        yield return CombatAnimation.Fly(prefab, a, b, projectileSpeed * user.tileSize);

        // Impact (explosion). Wait for most of it before the HIT / MISS banner.
        if (action.impactPrefab != null)
        {
            GameObject boom = Instantiate(action.impactPrefab, b, Quaternion.identity);
            SpriteFlipbook boomFlip = boom.GetComponent<SpriteFlipbook>();
            yield return new WaitForSeconds(boomFlip != null ? boomFlip.Duration * 0.7f : 0.3f);
        }
    }
}
