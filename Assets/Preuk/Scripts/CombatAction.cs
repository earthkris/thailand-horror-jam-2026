using UnityEngine;

public enum ActionTarget { Enemy, Ally, Self }   // Ally = any party member, including yourself
public enum ActionEffect { Damage, Heal }

// One thing a character can do with their action (Sword Strike, Holy Verse, Heal...).
// Make new ones: right-click in the Project window -> Create -> Game -> Combat Action.
// Then drag it into a character's "Actions" list on their CombatUnit.
[CreateAssetMenu(menuName = "Game/Combat Action", fileName = "New Action")]
public class CombatAction : ScriptableObject
{
    [Header("Shown in the menu")]
    public string displayName = "Strike";
    [TextArea(2, 4)] public string description = "";

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
}
