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
    }

    public List<ItemStack> items = new List<ItemStack>();

    public event System.Action OnChanged;

    // Spell books stay in the bag through a loop: the party wakes up with them, but has to read them again.
    // Static, so it survives the scene reloading. A fresh run (Play, START OVER) forgets them.
    static readonly List<ItemData> keptBooks = new List<ItemData>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => keptBooks.Clear();

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

    public void Add(ItemData item, int amount = 1)
    {
        foreach (ItemStack s in items)
            if (s.item == item)
            {
                s.count += amount;
                OnChanged?.Invoke();
                return;
            }
        items.Add(new ItemStack { item = item, count = amount });
        OnChanged?.Invoke();
    }

    // Uses up one. False if we don't have any.
    public bool Remove(ItemData item)
    {
        foreach (ItemStack s in items)
            if (s.item == item && s.count > 0)
            {
                s.count--;
                OnChanged?.Invoke();
                return true;
            }
        return false;
    }
}
