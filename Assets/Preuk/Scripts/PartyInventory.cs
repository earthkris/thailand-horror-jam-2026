using System.Collections.Generic;
using UnityEngine;

// The party's shared bag. Lives on the Combat object.
// Add items in the Inspector: + under Items, drag in an Item asset, set how many.
public class PartyInventory : MonoBehaviour
{
    [System.Serializable]
    public class ItemStack
    {
        public ItemData item;
        [Min(0)] public int count = 1;

        // The small text next to the item's name in menus: "4–10 2d4+2   x3"
        public string DetailText => $"{item.AmountText}   x{count}";
    }

    public List<ItemStack> items = new List<ItemStack>();

    [Tooltip("The bag holds at most this many of one item (books don't count). Keeps loot from piling up.")]
    [Min(1)] public int maxPerItem = 4;

    // Spell books stay in the bag through a loop: the party wakes up with them, but has to read them again.
    // Static, so it survives the scene reloading. A fresh run (Play, START OVER) forgets them.
    static readonly List<ItemData> keptBooks = new List<ItemData>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => keptBooks.Clear();

    // For the save (SaveGame)
    public static IReadOnlyList<ItemData> KeptBooks => keptBooks;

    void Awake()
    {
        if (LoopManager.LoopsUsed == 0) { keptBooks.Clear(); return; }
        foreach (ItemData book in keptBooks)
            if (Count(book) == 0) items.Add(new ItemStack { item = book, count = 1 });
    }

    // Is the book that teaches this already in the bag (picked up this run, maybe in an earlier loop)?
    public static bool HasBookFor(CombatAction teaches) => keptBooks.Exists(b => b != null && b.teaches == teaches);

    public void AddBook(ItemData book)
    {
        if (Count(book) == 0) Add(book);
        if (!keptBooks.Contains(book)) keptBooks.Add(book);
    }

    public int Count(ItemData item)
    {
        foreach (ItemStack s in items)
            if (s.item == item) return s.count;
        return 0;
    }

    // Puts up to 'amount' in the bag. Returns how many fitted (the rest is left behind: the bag is full).
    public int Add(ItemData item, int amount = 1)
    {
        int cap = item.effect == ItemEffect.Book ? int.MaxValue : maxPerItem;
        int fits = Mathf.Clamp(cap - Count(item), 0, amount);
        if (fits == 0) return 0;
        foreach (ItemStack s in items)
            if (s.item == item)
            {
                s.count += fits;
                return fits;
            }
        items.Add(new ItemStack { item = item, count = fits });
        return fits;
    }

    // Uses up one. False if we don't have any.
    public bool Remove(ItemData item)
    {
        foreach (ItemStack s in items)
            if (s.item == item && s.count > 0)
            {
                s.count--;
                return true;
            }
        return false;
    }
}
