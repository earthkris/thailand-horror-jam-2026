using System.Collections;
using UnityEngine;

public enum RollMode { Normal, Advantage, Disadvantage }

// One d20 roll on the dice tray, by the rules every attack shares (the party's attacks, monsters'
// attacks and opportunity attacks):
//   Advantage / disadvantage: two dice thrown together, keep the higher / the lower.
//   Natural 20 always hits and is a critical hit. Natural 1 always misses.
// Usage:  var roll = new AttackRoll();  yield return roll.Throw("Sword Strike  —  need 12+");
public class AttackRoll
{
    public int Value { get; private set; }     // the d20 that counts
    public string Text { get; private set; }   // for the log: "d20 (14)" or "d20 (14, 6 -> 6, disadvantage)"

    public bool IsCrit => Value == 20;

    // Advantage / disadvantage throw both dice at once. 'secondHeading' says why ("Advantage: it is prone");
    // it is shown under the heading.
    public IEnumerator Throw(string heading, RollMode mode = RollMode.Normal, string secondHeading = "")
    {
        if (mode == RollMode.Normal)
        {
            int only = 0;
            yield return DiceTray.RollD20(heading, v => only = v);
            Value = only;
            Text = $"d20 ({only})";
            yield break;
        }

        bool advantage = mode == RollMode.Advantage;
        string why = string.IsNullOrEmpty(secondHeading) ? "" : "\n<size=70%>" + secondHeading.Replace("  —  second roll", "") + "</size>";
        int first = 0, second = 0, kept = 0;
        yield return DiceTray.RollD20Pair(heading + why, advantage, (a, b, k) => { first = a; second = b; kept = k; });
        Value = kept;
        Text = $"d20 ({first}, {second} -> {Value}, {(advantage ? "advantage" : "disadvantage")})";
    }

    // Does 'total' (the roll plus bonuses) beat the armour class?
    public bool Hits(int total, int armorClass) => Beats(Value, total, armorClass);

    // The d20 rule for attacks and saving throws alike: 20 always succeeds, 1 always fails
    public static bool Beats(int d20, int total, int target) => d20 == 20 || (d20 != 1 && total >= target);

    // The d20 roll an attack needs to hit, for the banner ("need 12+")
    public static int Needed(int armorClass, int attackBonus) => Mathf.Clamp(armorClass - attackBonus, 2, 20);

    // ---------- Banner texts ----------

    public string MissText => Value == 1 ? "NATURAL 1  —  MISS" : "MISS";
    public string HitText(int damage) => IsCrit ? $"CRITICAL!  {damage} damage" : $"HIT  —  {damage} damage";

    // A critical hit rolls every damage die twice
    public int DiceTimes => IsCrit ? 2 : 1;
}
