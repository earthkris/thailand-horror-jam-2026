using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    public float tileSize = 1f;
    public float moveSpeed = 5f;            // tiles per second
    public LayerMask obstacleLayers;        // set to "Obstacles" in the Inspector

    [Header("References")]
    public SpriteRenderer sprite;
    public InputActionReference moveAction; // optional
    public event System.Action<Vector3> OnStepStarted;   // "I'm leaving this tile"
    bool isMoving;
    Vector3 targetPos;

    void Start()
    {
        transform.position = SnapToGrid(transform.position);
        targetPos = transform.position;
    }

    void OnEnable()
    {
        if (moveAction != null) moveAction.action.Enable();
    }

    void Update()
    {
        // 1. If we're mid-step, keep sliding toward the target tile
        if (isMoving)
        {
            transform.position = Vector3.MoveTowards(
                transform.position, targetPos, moveSpeed * tileSize * Time.deltaTime);

            if (transform.position == targetPos)
                isMoving = false;   // arrived, fall through to check for the next step
            else
                return;             // still sliding, nothing else to do this frame
        }

        // 2. Don't start a new step during dialogue, menus, or combat
        if (!GameState.CanMove) return;

        // 3. Which direction is pressed?
        Vector2Int dir = GetDirection();
        if (dir == Vector2Int.zero) return;

        // 4. Face left/right
        if (sprite != null && dir.x != 0)
            sprite.flipX = dir.x < 0;

        // 5. Is the next tile free?
        Vector3 next = targetPos + new Vector3(dir.x, 0f, dir.y) * tileSize;
        if (IsBlocked(next)) return;

        // 6. Start the step
        Vector3 from = targetPos;      // the tile we're leaving
        targetPos = next;
        isMoving = true;
        OnStepStarted?.Invoke(from);   // tell anyone listening
    }

    // Turns input into exactly one of: up, down, left, right, or none
    Vector2Int GetDirection()
    {
        Vector2 input = ReadInput();

        if (Mathf.Abs(input.x) > 0.5f && Mathf.Abs(input.x) >= Mathf.Abs(input.y))
            return new Vector2Int(input.x > 0 ? 1 : -1, 0);

        if (Mathf.Abs(input.y) > 0.5f)
            return new Vector2Int(0, input.y > 0 ? 1 : -1);

        return Vector2Int.zero;
    }

    bool IsBlocked(Vector3 tileCenter)
    {
        Vector3 halfSize = new Vector3(0.4f, 1f, 0.4f) * tileSize;
        bool blocked = Physics.CheckBox(tileCenter, halfSize, Quaternion.identity, obstacleLayers);
        return blocked;
    }

    Vector3 SnapToGrid(Vector3 pos)
    {
        return new Vector3(
            Mathf.Round(pos.x / tileSize) * tileSize,
            pos.y,
            Mathf.Round(pos.z / tileSize) * tileSize);
    }

    Vector2 ReadInput()
    {
        if (moveAction != null)
            return moveAction.action.ReadValue<Vector2>();

        var kb = Keyboard.current;
        if (kb == null) return Vector2.zero;

        float x = 0f, y = 0f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y -= 1f;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y += 1f;
        return new Vector2(x, y);
    }
}