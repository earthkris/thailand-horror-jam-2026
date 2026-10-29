using UnityEngine;

public enum ActionTarget { Enemy, Ally, Self }   // Ally = any party member, including yourself
public enum ActionEffect { Damage, Heal, Buff }   // Buff = Bless, Shield of Faith... (lasts until the fight ends)

// One thing a character can do with their action (Sword Strike, Holy Verse, Heal...).
// Make new ones: right-click in the Project window -> Create -> Game -> Combat Action.
// Then drag it into a character's "Actions" list on their CombatUnit.
[CreateAssetMenu(menuName = "Game/Combat Action", fileName = "New Action")]
public class CombatAction : ScriptableObject
{
    [Header("Shown in the menu")]
    public string displayName = "Strike";
    [TextArea(2, 4)] public string description = "";
    [Tooltip("Small type icon in front of the name (attack, heal, buff...).")]
    public Sprite typeIcon;

    [Header("Who and how far")]
    public ActionTarget target = ActionTarget.Enemy;
    [Tooltip("In tiles. 1 = only right next to you (melee).")]
    [Min(1)] public int range = 1;
    [Tooltip("Do walls and rocks block it?")]
    public bool needsLineOfSight = true;

    [Header("Effect")]
    public ActionEffect effect = ActionEffect.Damage;
    [Tooltip("Damage only: roll d20 + this, hit if it reaches the target's AC.")]
    public int attackBonus = 5;
    [Tooltip("Amount = (count)d(sides) + bonus")]
    [Min(0)] public int diceCount = 1;
    [Min(1)] public int diceSides = 8;
    public int bonus = 3;
    [Tooltip("Extra dice on top, e.g. 1d6 force for Arcane Strike. 0 = none.")]
    [Min(0)] public int extraDiceCount = 0;
    [Min(1)] public int extraDiceSides = 6;
    [Tooltip("Just for flavour text: slashing, radiant, force...")]
    public string damageType = "slashing";

    [Header("Visuals (ranged only, optional)")]
    [Tooltip("What flies at the target. Empty = the Combat object's default Ranged Projectile.")]
    public GameObject projectilePrefab;
    [Tooltip("Spawned where the projectile lands (e.g. an explosion), or on whoever gets healed.")]
    public GameObject impactPrefab;

    [Header("Visuals (any action, optional)")]
    [Tooltip("Spawned on the user as the action starts, e.g. sparkles when a spell is cast.")]
    public GameObject castEffect;

    [Header("Swing (melee, optional)")]
    [Tooltip("Played instead of the character's normal attack frames, e.g. Divine Smite's glowing sword. Empty = normal swing.")]
    public Sprite[] swingDownFrames;
    public Sprite[] swingUpFrames;
    [Tooltip("Drawn facing the same way as the character's walking side frames.")]
    public Sprite[] swingSideFrames;

    [Header("Buff (Buff effect only, lasts until the fight ends)")]
    [Tooltip("Adds this die to every attack roll. Bless = 4 (a d4). 0 = none.")]
    [Min(0)] public int blessDie = 0;
    [Tooltip("Extra Armor Class. Shield of Faith = 2.")]
    public int bonusAC = 0;
    [Tooltip("Buff every ally in range, not just the one you click.")]
    public bool wholeParty = false;

    [Header("Status on hit (Damage effect only, optional)")]
    [Tooltip("Burn: fire damage at the start of each of the target's turns. Paralyse: the target loses its turns.")]
    public StatusType inflicts = StatusType.None;
    [Tooltip("How many of the target's turns it lasts.")]
    [Min(1)] public int statusTurns = 1;
    [Tooltip("Chance on a hit, in percent.")]
    [Range(0, 100)] public int statusChance = 100;
    [Tooltip("Burn only: damage each turn = (count)d(sides).")]
    [Min(1)] public int statusDiceCount = 1;
    [Min(1)] public int statusDiceSides = 4;

    [Header("Limits")]
    [Tooltip("Times per battle. 0 = as often as you like.")]
    [Min(0)] public int usesPerBattle = 0;

    // "1d8+3" or "1d8+3 +1d6"
    public string DiceText
    {
        get
        {
            string s = $"{diceCount}d{diceSides}";
            if (bonus > 0) s += $"+{bonus}";
            else if (bonus < 0) s += bonus;
            if (extraDiceCount > 0) s += $" +{extraDiceCount}d{extraDiceSides}";
            return s;
        }
    }

    // Lowest and highest it can roll, without a crit. Damage is never below 1.
    public int MinAmount => Mathf.Max(effect == ActionEffect.Damage ? 1 : 0, diceCount + bonus + extraDiceCount);
    public int MaxAmount => Mathf.Max(MinAmount, diceCount * diceSides + bonus + extraDiceCount * extraDiceSides);

    // For menus: "4–11" with a small faint "1d8+3" after it
    public string RangeText => DiceFormat.Rich(MinAmount, MaxAmount, DiceText);

    // "+1d4 to hit", "+2 AC"
    public string BuffText
    {
        get
        {
            string s = "";
            if (blessDie > 0) s += $"+1d{blessDie} to hit";
            if (bonusAC != 0) s += (s.Length > 0 ? ", " : "") + $"{(bonusAC > 0 ? "+" : "")}{bonusAC} AC";
            return s;
        }
    }

    // What the menu shows next to the name: the amount, or what the buff gives
    public string AmountText => effect == ActionEffect.Buff ? BuffText : RangeText;
}
