using UnityEngine;

// Heal = a potion. Book = read it (outside a fight) to learn a move. Cure = heals a little and ends burning
// and paralysis. Action = it works like a combat move ('Acts As'): a thrown flask, a bomb, an elixir. Fight only.
// (New kinds go at the end: the assets store the number.)
public enum ItemEffect { Heal, None, Book, Cure, Action }

// One kind of item (Healing Potion, Fire Flask...).
// Make new ones: right-click in the Project window -> Create -> Game -> Item.
// Then add it to the party's bag (Combat object -> PartyInventory -> Items) or to a Loot Table.
// Using any item in a fight takes the bonus action.
[CreateAssetMenu(menuName = "Game/Item", fileName = "New Item")]
public class ItemData : ScriptableObject
{
    [Header("Shown in the menu")]
    public string displayName = "Item";
    [TextArea(2, 4)] public string description = "";

    [Header("Who and how far (Heal / Cure)")]
    public ActionTarget target = ActionTarget.Self;
    [Tooltip("In tiles. 1 = yourself or someone right next to you.")]
    [Min(1)] public int range = 1;

    [Header("Effect")]
    public ItemEffect effect = ItemEffect.Heal;
    [Tooltip("Heal / Cure: amount = (count)d(sides) + bonus")]
    [Min(0)] public int diceCount = 2;
    [Min(1)] public int diceSides = 4;
    public int bonus = 2;

    [Header("Action only")]
    [Tooltip("The move this item performs: who it hits, how far, the damage or blessing. Its uses-per-battle are ignored.")]
    public CombatAction actsAs;

    [Header("Book only")]
    [Tooltip("The move reading it teaches.")]
    public CombatAction teaches;
    [Tooltip("Who learns it: the character's object name (Kairos, Lucius) or their Unit Name.")]
    public string learner = "Lucius";

    public bool FightOnly => effect == ItemEffect.Action;

    public string DiceText => bonus != 0 ? $"{diceCount}d{diceSides}{(bonus > 0 ? "+" : "")}{bonus}" : $"{diceCount}d{diceSides}";

    // Lowest and highest it can roll
    public int MinAmount => Mathf.Max(0, diceCount + bonus);
    public int MaxAmount => Mathf.Max(MinAmount, diceCount * diceSides + bonus);

    // For menus: "4–10" with a small faint "2d4+2" after it
    public string RangeText => DiceFormat.Rich(MinAmount, MaxAmount, DiceText);

    // What the menus show next to the name
    public string AmountText => effect == ItemEffect.Action && actsAs != null ? actsAs.AmountText
                              : effect == ItemEffect.Cure ? RangeText + "  cures" : RangeText;

    // Would it do anything for 'u' right now? (Heal: hurt or down. Cure: also burning or paralysed.)
    public bool HelpsNow(CombatUnit u) =>
        u != null && !u.IsDead &&
        (u.IsDowned || u.CurrentHP < u.maxHP || (effect == ItemEffect.Cure && u.HasHarmfulStatus));

    // Heal / Cure on someone. Returns the line for the menu or the dice banner: "+6 HP  Lucius (21/25)"
    public string ApplyTo(CombatUnit u)
    {
        int amount = Mathf.Max(DiceSimulator.Roll(diceCount, diceSides) + bonus, 0);
        int before = u.CurrentHP;
        bool cured = effect == ItemEffect.Cure && u.HasHarmfulStatus;
        if (cured) u.CureHarmfulStatuses();
        u.Heal(amount);
        Debug.Log($"{displayName} on {u.unitName}: {DiceText} = {amount}{(cured ? ", statuses cleared" : "")}");
        return $"+{u.CurrentHP - before} HP  {u.unitName} ({u.CurrentHP}/{u.maxHP})" + (cured ? "  —  cured" : "");
    }
}
