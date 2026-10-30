using System.Collections;
using UnityEngine;

// The first boss's entrance (storyboard 7). Walk into the boss room and:
//   1. everyone stops, a shadow grows on the floor and the ground rumbles
//   2. the dialogue plays ("What is that thing?! It's HUGE!!!")
//   3. the boss drops out of the sky onto the shadow (BossAI.DropIn) with a shockwave
//   4. the fight starts.
// The boss stays hidden until then. The box (centred on this object, yellow in the Scene view)
// is where walking in starts it. It plays again after every loop.
public class BossIntro : MonoBehaviour
{
    public BossAI boss;
    [Tooltip("Walking onto a tile inside this box starts the intro. X = width, Y = depth (world Z).")]
    public Vector2 size = new Vector2(23f, 32f);
    [Tooltip("Element 0 = first run, 1 = after the 1st loop, 2 = after the 2nd... The last one is reused.")]
    public DialogueScriptableObject[] dialogueByLoop;
    [Tooltip("How far above its spot it falls from, in tiles.")]
    public float dropHeight = 14f;

    [Header("Door (closes behind the party)")]
    [Tooltip("A wall piece to copy into the doorway (it rises out of the floor, and sinks again when the boss dies).")]
    public GameObject doorTemplate;
    public Vector3 doorPosition;
    public Vector3 doorRotation;
    [Tooltip("How far below the floor it starts.")]
    public float doorDepth = 3.2f;
    [Tooltip("Kairos walks this many tiles further in first, so Lucius is through the door too.")]
    public int walkIn = 2;

    PlayerMovement player;
    bool played;
    GameObject shadow;
    GameObject door;

    void Start()
    {
        player = FindAnyObjectByType<PlayerMovement>();
        if (boss == null) { Debug.LogWarning("BossIntro: drag in the boss.", this); enabled = false; return; }
        boss.HideBody();
        if (boss.TryGetComponent(out EnemyEncounter enc)) enc.enabled = false;   // no touching an invisible boss
    }

    void Update()
    {
        if (played || player == null || GameState.Current != GameMode.Exploring) return;
        if (!Inside(player.CurrentTile)) return;
        played = true;
        StartCoroutine(Play());
    }

    IEnumerator Play()
    {
        GameState.Current = GameMode.Cutscene;
        player.StopWalking();
        while (player.IsStepping) yield return null;

        // 0. Both of them step in, then the door slams shut behind them
        Vector3 into = boss.transform.position - player.CurrentTile;
        into = Mathf.Abs(into.x) >= Mathf.Abs(into.z) ? new Vector3(Mathf.Sign(into.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(into.z));
        if (walkIn > 0 && player.WalkToScripted(player.CurrentTile + into * walkIn * boss.GetComponent<CombatUnit>().tileSize))
            while (player.IsWalking) yield return null;
        yield return new WaitForSeconds(0.35f);   // Lucius finishes his step
        yield return CloseDoor();

        // 1. Something big is coming
        var cam = FindAnyObjectByType<CameraController>();
        bool talking = true;
        StartCoroutine(GrowShadow(() => talking));
        if (cam != null) cam.Shake(0.08f, 0.8f);
        yield return new WaitForSeconds(0.6f);

        // 2. Dialogue
        if (dialogueByLoop != null && dialogueByLoop.Length > 0 && DialogueBox.Instance != null)
        {
            DialogueScriptableObject lines = dialogueByLoop[Mathf.Clamp(LoopManager.LoopsUsed, 0, dialogueByLoop.Length - 1)];
            if (lines != null)
            {
                DialogueBox.Instance.StartDialogue(lines);
                yield return null;
                while (DialogueBox.Instance != null && DialogueBox.Instance.IsContentOpen) yield return null;
            }
        }

        // 3. It drops in (the camera looks at it, then back)
        if (cam != null) cam.FocusOn(boss.transform);
        yield return new WaitForSeconds(0.5f);
        talking = false;
        yield return boss.DropIn(dropHeight * boss.GetComponent<CombatUnit>().tileSize);
        if (shadow != null) Destroy(shadow);
        yield return new WaitForSeconds(0.7f);
        if (cam != null) cam.FocusOn(player.transform);

        // 4. Fight (first run: the party can't win this one)
        boss.UpdateForcedRules();
        boss.GetComponent<CombatUnit>().OnDied += _ => StartCoroutine(OpenDoor());
        if (boss.TryGetComponent(out EnemyEncounter enc))
        {
            enc.enabled = true;
            enc.StartCombatWith(player.GetComponent<CombatUnit>());
        }
        else GameState.Current = GameMode.Exploring;
    }

    IEnumerator CloseDoor()
    {
        if (doorTemplate == null) yield break;
        Vector3 down = doorPosition + Vector3.down * doorDepth;
        door = Instantiate(doorTemplate, down, Quaternion.Euler(doorRotation));
        door.name = "Boss Door";
        door.SetActive(true);
        // A wall that blocks walking (Obstacles layer, with a collider)
        int blockLayer = LayerMask.NameToLayer("Obstacles");
        foreach (Transform t in door.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = blockLayer;
        if (door.GetComponentInChildren<Collider>() == null) door.AddComponent<BoxCollider>();
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.45f)
        {
            door.transform.position = Vector3.Lerp(down, doorPosition, t * t);   // speeds up, then slams
            yield return null;
        }
        door.transform.position = doorPosition;
        var cam = FindAnyObjectByType<CameraController>();
        if (cam != null) cam.Shake(0.15f, 0.3f);
        yield return new WaitForSeconds(0.3f);
    }

    IEnumerator OpenDoor()
    {
        yield return new WaitForSeconds(1f);
        if (door == null) yield break;
        Vector3 up = door.transform.position, down = up + Vector3.down * doorDepth;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 1.2f)
        {
            door.transform.position = Vector3.Lerp(up, down, t);
            yield return null;
        }
        Destroy(door);
    }

    // A dark round shadow on the boss's spot, growing while it is up there
    IEnumerator GrowShadow(System.Func<bool> stillUp)
    {
        const int res = 64;
        var px = new Color32[res * res];
        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float dx = (x + 0.5f) / res * 2f - 1f, dy = (y + 0.5f) / res * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                px[y * res + x] = new Color32(0, 0, 0, (byte)(Mathf.Clamp01((1f - r) * 2.5f) * 255f));
            }
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels32(px);
        tex.Apply();

        shadow = new GameObject("Boss Shadow", typeof(SpriteRenderer));
        var sr = shadow.GetComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res);   // 1 unit across
        Vector3 p = boss.transform.position;
        float floor = Physics.Raycast(p + Vector3.up, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y : p.y - 1f;
        shadow.transform.position = new Vector3(p.x, floor + 0.07f, p.z);
        shadow.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        float full = boss.GetComponent<CombatUnit>().size * boss.GetComponent<CombatUnit>().tileSize * 1.4f;
        float t = 0f;
        while (shadow != null)
        {
            t += Time.deltaTime;
            float grow = stillUp() ? Mathf.Lerp(0.3f, 0.6f, Mathf.Clamp01(t / 4f))   // hovering high up
                                   : Mathf.MoveTowards(shadow.transform.localScale.x / full, 1f, Time.deltaTime * 2.5f);   // falling
            shadow.transform.localScale = Vector3.one * full * grow;
            sr.color = new Color(1f, 1f, 1f, 0.35f + 0.35f * grow);
            yield return null;
        }
    }

    bool Inside(Vector3 p)
    {
        Vector3 c = transform.position;
        return Mathf.Abs(p.x - c.x) <= size.x * 0.5f + 0.01f && Mathf.Abs(p.z - c.z) <= size.y * 0.5f + 0.01f;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.1f, new Vector3(size.x, 0.2f, size.y));
    }
}
