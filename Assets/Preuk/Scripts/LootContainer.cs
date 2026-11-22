using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A crate, chest, barrel or coffin you can search once (E or a click). What's inside comes from its Loot Table
// and goes into the party's bag. Every loop fills them again (the scene starts over).
// Coffins can hold a nasty surprise instead: 'Ambush Chance' spawns a monster next to it, and the fight starts.
// Put it on the root of the prop (the object with the collider). Searched props darken and tilt a little.
public class LootContainer : Interactable
{
    public LootTable table;
    [Tooltip("The word in the prompt: 'Search crate', 'Open chest'.")]
    public string what = "crate";

    [Header("Ambush (optional)")]
    [Range(0f, 1f)] public float ambushChance = 0f;
    public GameObject ambushPrefab;
    [Tooltip("The monster is this prop (a Mimic): the prop vanishes and the monster stands where it was.")]
    public bool ambushIsUs = false;
    public MonsterSpawner.Stats ambushStats = new MonsterSpawner.Stats
    {
        name = "Coffin Crawler", hp = 8, armorClass = 11, attackBonus = 4, diceSides = 6, damageBonus = 1,
        tint = new Color(0.7f, 0.75f, 0.6f), spriteScale = 0.75f
    };

    [Header("Look")]
    [Tooltip("A hinged part that swings open (a chest lid). Optional.")]
    public Transform lid;
    public float lidAngle = -100f;

    bool searched;

    protected override string Prompt => promptText + " " + what;

    protected override void Start()
    {
        if (promptText == "Pick up") promptText = what == "chest" || what == "coffin" ? "Open" : "Search";
        base.Start();
    }

    protected override void Interact()
    {
        if (searched) return;
        searched = true;
        StartCoroutine(Open());
    }

    IEnumerator Open()
    {
        if (player != null) player.StopWalking();
        Sfx.Play(what == "chest" ? "chest_open" : what == "coffin" ? "door_open" : "bag", 0.9f);
        yield return Shake();

        if (ambushPrefab != null && Random.value < ambushChance && Ambush())
        {
            Dim();
            enabled = false;
            yield break;
        }

        var bag = FindAnyObjectByType<PartyInventory>();
        var found = new Dictionary<ItemData, int>();   // the same item drawn twice shows once, "x2"
        bool full = false;
        if (table != null && bag != null)
            foreach (var (item, count) in table.Roll())
            {
                int kept = bag.Add(item, count);
                if (kept > 0) found[item] = (found.TryGetValue(item, out int n) ? n : 0) + kept;
                if (kept < count) full = true;
            }
        var lines = new List<string>();
        foreach (var pair in found) lines.Add(pair.Value > 1 ? $"{pair.Key.displayName} x{pair.Value}" : pair.Key.displayName);

        if (lines.Count > 0)
        {
            Sfx.Play("book_pickup", 0.6f, 1.3f);
            GameToast.Show($"Found: {string.Join(", ", lines)}", full ? "Your bag can't hold any more of some of it." : "In your bag (ITEMS in the menu).");
        }
        else if (full) GameToast.Show($"The {what} holds things you can't carry", "Your bag is full of those.");
        else GameToast.Show($"Empty {what}", table != null ? table.EmptyLine : "Nothing.");
        Dim();
        enabled = false;   // searched: no more prompt
    }

    // Something was waiting inside
    bool Ambush()
    {
        Vector3 spot;
        if (ambushIsUs)
        {
            if (player == null) return false;
            Vector3 me = TileMath.Snap(transform.position, player.tileSize);
            spot = new Vector3(me.x, player.transform.position.y, me.z);
        }
        else if (!TryFreeTileNextToUs(out spot)) return false;
        CombatUnit m = MonsterSpawner.Spawn(ambushPrefab, ambushStats, spot);
        if (m == null) return false;
        Sfx.Play("roar", 0.8f, 1.5f);
        if (ambushIsUs)
        {
            // The chest was the monster all along: it's gone, the mimic stands in its place
            foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.enabled = false;
            foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
            GameToast.Show($"The {what} has teeth!", "A Mimic.");
        }
        else
        {
            StartCoroutine(MonsterSpawner.RiseFromFloor(m.transform));
            GameToast.Show($"Something crawls out of the {what}!", "");
        }
        if (player != null && m.TryGetComponent(out EnemyEncounter e)) e.StartCombatWith(player.GetComponent<CombatUnit>());
        return true;
    }

    bool TryFreeTileNextToUs(out Vector3 spot)
    {
        spot = default;
        if (player == null) return false;
        float t = player.tileSize;
        Vector3 me = TileMath.Snap(transform.position, t);
        foreach (Vector2Int d in TileMath.Directions)
        {
            Vector3 tile = me + new Vector3(d.x, 0f, d.y) * t;
            if ((tile - TileMath.Snap(player.transform.position, t)).sqrMagnitude < 0.01f) continue;
            if (TileMath.IsOpenGround(tile, t, player.obstacleLayers))
            {
                spot = new Vector3(tile.x, player.transform.position.y, tile.z);
                return true;
            }
        }
        return false;
    }

    // A quick rattle, and the lid swings open if there is one
    IEnumerator Shake()
    {
        Vector3 home = transform.localPosition;
        yield return Tween.Over(0.25f, k => transform.localPosition = home + new Vector3(Mathf.Sin(k * 40f) * 0.04f * (1f - k), 0f, 0f));
        transform.localPosition = home;
        if (lid != null)
        {
            Quaternion closed = lid.localRotation;
            yield return Tween.Over(0.3f, k => lid.localRotation = closed * Quaternion.Euler(lidAngle * k, 0f, 0f), smooth: true);
        }
    }

    // Searched: a little darker, so you can tell at a glance
    void Dim()
    {
        var block = new MaterialPropertyBlock();
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            r.GetPropertyBlock(block);
            block.SetColor("_BaseColor", new Color(0.55f, 0.55f, 0.55f, 1f));
            block.SetColor("_Color", new Color(0.55f, 0.55f, 0.55f, 1f));
            r.SetPropertyBlock(block);
        }
    }
}
