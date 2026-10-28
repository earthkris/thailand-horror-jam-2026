using UnityEngine;

public class Follower : MonoBehaviour
{
    public PlayerMovement leader;     // who to follow

    [Header("Start position")]
    [Tooltip("On start, move to a free tile next to the leader instead of staying where you were placed.")]
    public bool placeNextToLeaderOnStart = true;

    Vector3 targetPos;

    void Start()
    {
        if (placeNextToLeaderOnStart && leader != null)
            PlaceNextToLeader();

        targetPos = transform.position;
    }

    void OnEnable()
    {
        if (leader != null) leader.OnStepStarted += FollowTo;   // start listening
        targetPos = transform.position;   // after combat: stay put until the leader moves
    }

    void OnDisable()
    {
        if (leader != null) leader.OnStepStarted -= FollowTo;   // stop listening
    }

    // Find the first free tile around the leader and stand there
    void PlaceNextToLeader()
    {
        float t = leader.tileSize;

        // The leader's tile, rounded to the grid (the leader may not have snapped itself yet)
        Vector3 lp = leader.transform.position;
        Vector3 leaderTile = new Vector3(
            Mathf.Round(lp.x / t) * t,
            transform.position.y,          // keep our own height
            Mathf.Round(lp.z / t) * t);

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

    bool IsFree(Vector3 tile)
    {
        // 1. No wall here (same box check the player uses for movement)
        Vector3 halfSize = new Vector3(0.4f, 1f, 0.4f) * leader.tileSize;
        if (Physics.CheckBox(tile, halfSize, Quaternion.identity, leader.obstacleLayers))
            return false;

        // 2. There is ground under it (don't spawn off the edge of the map)
        Vector3 rayStart = new Vector3(tile.x, tile.y + 1f, tile.z);
        return Physics.Raycast(rayStart, Vector3.down, 5f, ~leader.obstacleLayers);
    }

    // Runs every time the leader starts a step
    void FollowTo(Vector3 tile)
    {
        // Keep our own height, take the leader's old X and Z
        targetPos = new Vector3(tile.x, transform.position.y, tile.z);
    }

    void Update()
    {
        if (leader == null) return;

        // Same speed as the leader, so we arrive at the same moment
        float speed = leader.moveSpeed * leader.tileSize;
        transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);
    }
}