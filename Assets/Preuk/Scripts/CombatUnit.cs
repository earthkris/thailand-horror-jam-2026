using System.Collections.Generic;
using UnityEngine;

public enum Team { Party, Enemy }

// D&D-style combat stats + how many tiles the unit covers.
// Put this on the ROOT of everything that can fight (Player, Friend, enemies, bosses).
public class CombatUnit : MonoBehaviour
{
    [Header("Identity")]
    public string unitName = "Unit";
    public Team team = Team.Party;

    [Header("Size")]
    [Tooltip("Tiles wide and deep. 1 = normal, 2 = 2x2 boss, 3 = 3x3 boss.")]
    [Range(1, 3)] public int size = 1;
    public float tileSize = 1f;

    [Header("Defense")]
    public int maxHP = 10;
    [Tooltip("Armor Class: an attack roll must reach this number to hit.")]
    public int armorClass = 12;

    [Header("Initiative")]
    [Tooltip("Dexterity modifier, added to the d20 initiative roll.")]
    public int dexModifier = 0;

    [Header("Attack")]
    [Tooltip("Added to the d20 attack roll.")]
    public int attackBonus = 3;
    [Tooltip("Damage = (count)d(sides) + bonus, e.g. 1d6+2")]
    public int damageDiceCount = 1;
    public int damageDiceSides = 6;
    public int damageBonus = 2;

    [Header("Movement")]
    [Tooltip("Speed in feet. Every 5 ft = 1 tile, so 30 ft = 6 tiles.")]
    public int speedFeet = 30;

    [Header("Actions (ACTION menu)")]
    [Tooltip("What this character can do with their action. Drag Combat Action assets here.")]
    public List<CombatAction> actions = new List<CombatAction>();

    public int CurrentHP { get; private set; }
    public bool IsAlive => CurrentHP > 0;
    public int MoveTiles => speedFeet / 5;
    public string DamageText => $"{damageDiceCount}d{damageDiceSides}+{damageBonus}";

    public event System.Action<CombatUnit> OnDied;

    void Awake()
    {
        CurrentHP = maxHP;
        SnapToGrid();
    }

    // ---------- Footprint (which tiles we cover) ----------

    // How far the root sits from the centre of the bottom-left tile.
    // 1x1 -> 0, 2x2 -> half a tile, 3x3 -> one tile
    float CenterOffset => (size - 1) * 0.5f * tileSize;

    // The bottom-left tile (lowest x and z) of the area we cover
    public Vector2Int AnchorTile => new Vector2Int(
        Mathf.RoundToInt((transform.position.x - CenterOffset) / tileSize),
        Mathf.RoundToInt((transform.position.z - CenterOffset) / tileSize));

    // Every tile we cover (1, 4 or 9 tiles)
    public List<Vector2Int> GetOccupiedTiles()
    {
        var list = new List<Vector2Int>();
        Vector2Int a = AnchorTile;
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
                list.Add(new Vector2Int(a.x + x, a.y + y));
        return list;
    }

    public bool Occupies(Vector2Int tile)
    {
        Vector2Int a = AnchorTile;
        return tile.x >= a.x && tile.x < a.x + size &&
               tile.y >= a.y && tile.y < a.y + size;
    }

    // Steps (no diagonals) from a tile to the NEAREST tile we cover. 0 = on top of us, 1 = next to us
    public int StepsTo(Vector2Int tile)
    {
        Vector2Int a = AnchorTile;
        int dx = Mathf.Max(a.x - tile.x, 0, tile.x - (a.x + size - 1));
        int dy = Mathf.Max(a.y - tile.y, 0, tile.y - (a.y + size - 1));
        return dx + dy;
    }

    // Where the root should be if our bottom-left tile is 'anchor'
    public Vector3 WorldPositionFor(Vector2Int anchor)
    {
        return new Vector3(anchor.x * tileSize + CenterOffset,
                           transform.position.y,
                           anchor.y * tileSize + CenterOffset);
    }

    public void SnapToGrid()
    {
        transform.position = WorldPositionFor(AnchorTile);
    }

    // Right-click the component header in the Inspector -> "Snap To Grid"
    [ContextMenu("Snap To Grid")]
    void SnapToGridMenu()
    {
#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(transform, "Snap To Grid");   // so Ctrl+Z works
#endif
        SnapToGrid();
    }

    // ---------- Health ----------

    public void TakeDamage(int amount)
    {
        if (!IsAlive) return;

        CurrentHP = Mathf.Max(CurrentHP - amount, 0);
        Debug.Log($"{unitName} takes {amount} damage ({CurrentHP}/{maxHP} HP)");

        if (CurrentHP == 0)
        {
            Debug.Log($"{unitName} is down!");
            OnDied?.Invoke(this);
        }
    }

    public void Heal(int amount)
    {
        if (!IsAlive) return;
        CurrentHP = Mathf.Min(CurrentHP + amount, maxHP);
    }

    // Shows the covered tiles as yellow squares in the Scene view when selected
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        foreach (Vector2Int t in GetOccupiedTiles())
            Gizmos.DrawWireCube(new Vector3(t.x * tileSize, 0.05f, t.y * tileSize),
                                new Vector3(tileSize * 0.9f, 0.01f, tileSize * 0.9f));
    }
}