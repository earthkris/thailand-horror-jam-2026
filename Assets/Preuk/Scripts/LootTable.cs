using System.Collections.Generic;
using UnityEngine;

// What a kind of container can hold (crates, chests, coffins...). Each entry has a weight:
// weight 30 comes up three times as often as weight 10.
// Make new ones: right-click in the Project window -> Create -> Game -> Loot Table.
[CreateAssetMenu(menuName = "Game/Loot Table", fileName = "New Loot Table")]
public class LootTable : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public ItemData item;
        [Min(0)] public int weight = 10;
        [Min(1)] public int min = 1;
        [Min(1)] public int max = 1;
    }

    public List<Entry> entries = new List<Entry>();

    [Tooltip("Chance it holds nothing at all.")]
    [Range(0f, 1f)] public float emptyChance = 0.5f;
    [Tooltip("How many different draws from the table when it isn't empty.")]
    [Min(1)] public int minDraws = 1;
    [Min(1)] public int maxDraws = 1;

    [Tooltip("Said when it's empty. One is picked at random.")]
    [TextArea(1, 3)] public string[] emptyLines = { "Nothing but dust." };

    // One search: the items found (an empty list = nothing)
    public List<(ItemData item, int count)> Roll()
    {
        var found = new List<(ItemData, int)>();
        if (entries.Count == 0 || Random.value < emptyChance) return found;
        int total = 0;
        foreach (Entry e in entries) if (e.item != null) total += e.weight;
        if (total <= 0) return found;

        int draws = Random.Range(minDraws, maxDraws + 1);
        for (int i = 0; i < draws; i++)
        {
            int pick = Random.Range(0, total);
            foreach (Entry e in entries)
            {
                if (e.item == null) continue;
                if (pick < e.weight) { found.Add((e.item, Random.Range(e.min, e.max + 1))); break; }
                pick -= e.weight;
            }
        }
        return found;
    }

    public string EmptyLine => emptyLines != null && emptyLines.Length > 0 ? emptyLines[Random.Range(0, emptyLines.Length)] : "Nothing.";
}
