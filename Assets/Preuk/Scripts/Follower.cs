using UnityEngine;

public class Follower : MonoBehaviour
{
    public PlayerMovement leader;     // who to follow
    [Tooltip("Optional: walk behind this follower instead of right behind the leader (a line of allies).")]
    public Follower ahead;

    // "I'm leaving this tile" (the next one in line walks onto it)
    public event System.Action<Vector3> OnStepStarted;

    [Header("Start position")]
    [Tooltip("On start, move to a free tile next to the leader instead of staying where you were placed.")]
    public bool placeNextToLeaderOnStart = true;

    Vector3 targetPos;
    CombatUnit unit;

    // As a prefab in a new scene: follow whoever the player walks with
    void Awake()
    {
        if (leader == null) leader = FindAnyObjectByType<PlayerMovement>();
        unit = GetComponent<CombatUnit>();
    }

    void Start()
    {
        if (placeNextToLeaderOnStart && leader != null)
            PlaceNextToLeader();

        targetPos = transform.position;
    }

    void OnEnable()
    {
        Listen(true);
        targetPos = transform.position;   // after combat: stay put until the leader moves
    }

    void OnDisable() => Listen(false);

    void Listen(bool on)
    {
        if (ahead != null && ahead != this)
        {
            if (on) ahead.OnStepStarted += FollowTo; else ahead.OnStepStarted -= FollowTo;
        }
        else if (leader != null)
        {
            if (on) leader.OnStepStarted += FollowTo; else leader.OnStepStarted -= FollowTo;
        }
    }

    // Join the end of the line behind 'who' (a rescued ally)
    public void FollowBehind(Follower who)
    {
        Listen(false);
        ahead = who;
        if (isActiveAndEnabled) Listen(true);
    }

    // Find the first free tile around the leader and stand there
    void PlaceNextToLeader()
    {
        float t = leader.tileSize;

        // The leader's tile, rounded to the grid (the leader may not have snapped itself yet)
        Vector3 leaderTile = TileMath.Snap(leader.transform.position, t);
        leaderTile.y = transform.position.y;   // keep our own height

        // Order matters: the first free one wins
        Vector3[] offsets =
        {
            Vector3.left, Vector3.right,                     // beside
            Vector3.forward, Vector3.back,                   // behind (+Z, into the screen), in front
            new Vector3(-1, 0, 1), new Vector3(1, 0, 1),     // diagonals as a backup
            new Vector3(-1, 0, -1), new Vector3(1, 0, -1)
        };

        foreach (Vector3 offset in offsets)
        {
            Vector3 tile = leaderTile + offset * t;
            if (IsFree(tile))
            {
                transform.position = tile;
                return;
            }
        }

        // Every neighbour is blocked: share the leader's tile and step off on the first move
        transform.position = leaderTile;
        Debug.LogWarning("Follower: no free tile next to the leader, starting on the leader's tile.");
    }

    // No wall here (the same check the player moves by), ground under it, and no one else of the party on it
    bool IsFree(Vector3 tile)
    {
        if (!TileMath.IsOpenGround(tile, leader.tileSize, leader.obstacleLayers)) return false;
        if (Flat(leader.transform.position, tile)) return false;
        foreach (Follower f in FindObjectsByType<Follower>(FindObjectsSortMode.None))
            if (f != this && Flat(f.transform.position, tile)) return false;
        return true;
    }

    bool Flat(Vector3 a, Vector3 b) =>
        Mathf.Abs(a.x - b.x) < leader.tileSize * 0.5f && Mathf.Abs(a.z - b.z) < leader.tileSize * 0.5f;

    // After the leader teleports: stand next to them right away (no sliding across the map)
    public void SnapNextToLeader()
    {
        if (leader == null) return;
        PlaceNextToLeader();
        targetPos = transform.position;
    }

    // Runs every time the leader starts a step
    void FollowTo(Vector3 tile)
    {
        // Keep our own height, take the leader's old X and Z. Whoever walks behind us takes ours.
        Vector3 old = targetPos;
        targetPos = new Vector3(tile.x, transform.position.y, tile.z);
        OnStepStarted?.Invoke(old);
    }

    void Update()
    {
        if (leader == null) return;
        // A body on the floor doesn't follow anyone (and doesn't slide off once it gets up)
        if (unit != null && !unit.IsAlive) { targetPos = transform.position; return; }

        // Same speed as the leader, so we arrive at the same moment
        float speed = leader.moveSpeed * leader.tileSize;
        transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);
    }
}