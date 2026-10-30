using System.Collections.Generic;
using UnityEngine;

// Starts combat when this enemy touches a party member (directly next to them, no diagonals).
// Remembers WHO was touched, so they stand in front when the battle starts.
[RequireComponent(typeof(CombatUnit))]
public class EnemyEncounter : MonoBehaviour
{
    // (enemy that started it, party member that got touched)
    public static event System.Action<EnemyEncounter, CombatUnit> OnCombatStarted;

    public CombatUnit Unit { get; private set; }
    public bool InCombat { get; private set; }

    readonly List<CombatUnit> party = new List<CombatUnit>();

    // Clear the event when Play starts, so old listeners from the last session don't stick around
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => OnCombatStarted = null;

    void Awake()
    {
        Unit = GetComponent<CombatUnit>();
    }

    void Start()
    {
        foreach (CombatUnit u in FindObjectsByType<CombatUnit>(FindObjectsSortMode.None))
            if (u.team == Team.Party) party.Add(u);
    }

    void Update()
    {
        if (InCombat || !GameState.CanMove) return;   // not during dialogue or cutscenes

        // Is anyone in the party touching us? If two touch in the same frame, the closer one counts.
        CombatUnit touched = null;
        float closest = float.MaxValue;
        foreach (CombatUnit p in party)
        {
            if (p == null || !p.IsAlive) continue;
            if (Unit.StepsTo(TileOf(p.transform.position)) > 1) continue;

            float d = Vector3.Distance(p.transform.position, transform.position);
            if (d < closest)
            {
                closest = d;
                touched = p;
            }
        }

        if (touched != null) StartCombat(touched);
    }

    // Start the fight from a script (a boss intro), as if 'touched' had walked into us
    public void StartCombatWith(CombatUnit touched)
    {
        if (InCombat || touched == null) return;
        StartCombat(touched);
    }

    // The combat manager calls this on other enemies that join the same fight
    public void MarkInCombat() => InCombat = true;

    void StartCombat(CombatUnit touched)
    {
        InCombat = true;
        GameState.Current = GameMode.Combat;
        Debug.Log($"{Unit.unitName} touched {touched.unitName}. Combat!");
        OnCombatStarted?.Invoke(this, touched);
    }

    Vector2Int TileOf(Vector3 pos)
    {
        return new Vector2Int(Mathf.RoundToInt(pos.x / Unit.tileSize),
                              Mathf.RoundToInt(pos.z / Unit.tileSize));
    }

    // Red squares in the Scene view = the tiles where a party member counts as "touching"
    void OnDrawGizmosSelected()
    {
        CombatUnit unit = GetComponent<CombatUnit>();   // Awake hasn't run in edit mode
        if (unit == null) return;

        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.8f);
        Vector2Int a = unit.AnchorTile;
        for (int x = a.x - 1; x <= a.x + unit.size; x++)
            for (int y = a.y - 1; y <= a.y + unit.size; y++)
                if (unit.StepsTo(new Vector2Int(x, y)) == 1)
                    Gizmos.DrawWireCube(new Vector3(x * unit.tileSize, 0.05f, y * unit.tileSize),
                                        new Vector3(unit.tileSize * 0.9f, 0.01f, unit.tileSize * 0.9f));
    }
}