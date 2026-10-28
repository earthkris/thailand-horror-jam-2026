using UnityEngine;

public class Follower : MonoBehaviour
{
    public PlayerMovement leader;     // who to follow
    public SpriteRenderer sprite;     // this character's sprite (for flipping)

    Vector3 targetPos;

    void Start()
    {
        targetPos = transform.position;
    }

    void OnEnable()
    {
        if (leader != null) leader.OnStepStarted += FollowTo;   // start listening
    }

    void OnDisable()
    {
        if (leader != null) leader.OnStepStarted -= FollowTo;   // stop listening
    }

    // Runs every time the leader starts a step
    void FollowTo(Vector3 tile)
    {
        // Keep our own height, take the leader's old X and Z
        Vector3 newTarget = new Vector3(tile.x, transform.position.y, tile.z);

        // Face left/right
        float dx = newTarget.x - targetPos.x;
        if (sprite != null && dx != 0) sprite.flipX = dx < 0;

        targetPos = newTarget;
    }

    void Update()
    {
        // Same speed as the leader, so we arrive at the same moment
        float speed = leader.moveSpeed * leader.tileSize;
        transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);
    }
}