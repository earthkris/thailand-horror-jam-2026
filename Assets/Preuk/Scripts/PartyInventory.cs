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
