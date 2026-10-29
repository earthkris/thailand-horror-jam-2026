using UnityEngine;

public enum ItemEffect { Heal, None }

// One kind of item (Healing Potion, Holy Water...).
// Make new ones: right-click in the Project window -> Create -> Game -> Item.
// Then add it to the party's bag: Combat object -> PartyInventory -> Items.
[CreateAssetMenu(menuName = "Game/Item", fileName = "New Item")]
public class ItemData : ScriptableObject
{
    [Header("Shown in the menu")]
    public string displayName = "Item";
    [TextArea(2, 4)] public string description = "";

    [Header("Who and how far")]
    public ActionTarget target = ActionTarget.Self;
    [Tooltip("In tiles. 1 = yourself or someone right next to you.")]
    [Min(1)] public int range = 1;

    [Header("Effect")]
    public ItemEffect effect = ItemEffect.Heal;
    [Tooltip("Amount = (count)d(sides) + bonus")]
    [Min(0)] public int diceCount = 2;
    [Min(1)] public int diceSides = 4;
    public int bonus = 2;

    public string DiceText => bonus != 0 ? $"{diceCount}d{diceSides}{(bonus > 0 ? "+" : "")}{bonus}" : $"{diceCount}d{diceSides}";

    // Lowest and highest it can roll
    public int MinAmount => Mathf.Max(0, diceCount + bonus);
    public int MaxAmount => Mathf.Max(MinAmount, diceCount * diceSides + bonus);

    // For menus: "4–10" with a small faint "2d4+2" after it
    public string RangeText => DiceFormat.Rich(MinAmount, MaxAmount, DiceText);
}
