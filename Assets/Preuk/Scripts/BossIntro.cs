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
    CombatUnit bossUnit;
    bool played;
    GameObject shadow;
    GameObject door;

    void Start()
    {
        player = FindAnyObjectByType<PlayerMovement>();
        if (boss == null) { Debug.LogWarning("BossIntro: drag in the boss.", this); enabled = false; return; }
        bossUnit = boss.GetComponent<CombatUnit>();
        boss.HideBody();
        if (boss.TryGetComponent(out EnemyEncounter enc)) enc.enabled = false;   // no touching an invisible boss
    }

    void Update()
    {
        if (played || player == null || GameState.Current != GameMode.Exploring) return;
        if (!ZoneBox.Contains(transform.position, size, player.CurrentTile)) return;
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
        if (walkIn > 0 && player.WalkToScripted(player.CurrentTile + into * walkIn * bossUnit.tileSize))
            while (player.IsWalking) yield return null;
        yield return new WaitForSeconds(0.35f);   // Lucius finishes his step
        yield return CloseDoor();

        // 1. Something big is coming
        CameraController cam = CameraController.Main;
        bool talking = true;
        StartCoroutine(GrowShadow(() => talking));
        if (cam != null) cam.Shake(0.08f, 0.8f);
        yield return new WaitForSeconds(0.6f);

        // 2. Dialogue
        yield return StoryDialogue.Play(StoryDialogue.Pick(dialogueByLoop, LoopManager.LoopsUsed));

        // 3. It drops in (the camera looks at it, then back)
        if (cam != null) cam.FocusOn(boss.transform);
        yield return new WaitForSeconds(0.5f);
        talking = false;
        yield return boss.DropIn(dropHeight * bossUnit.tileSize);
        if (shadow != null) Destroy(shadow);
        yield return new WaitForSeconds(0.7f);
        if (cam != null) cam.FocusOn(player.transform);

        // 4. Fight (first run: the party can't win this one)
        boss.UpdateForcedRules();
        bossUnit.OnDied += _ => StartCoroutine(OpenDoor());
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
        Sfx.Play("door", 1f, 0.7f);
        Sfx.Boom(0.6f);
        CameraController cam = CameraController.Main;
        if (cam != null) cam.Shake(0.15f, 0.3f);
        yield return new WaitForSeconds(0.3f);
    }

    IEnumerator OpenDoor()
    {
        yield return new WaitForSeconds(1f);
        if (door == null) yield break;
        Vector3 up = door.transform.position, down = up + Vector3.down * doorDepth;
        Sfx.Play("door_open", 1f, 0.7f);
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
        const int res = 64;   // black, solid in the middle and soft at the edge, 1 unit across
        shadow = new GameObject("Boss Shadow", typeof(SpriteRenderer));
        var sr = shadow.GetComponent<SpriteRenderer>();
        sr.sprite = ProceduralSprite.Create(res, res,
            (dx, dy) => ProceduralSprite.Pixel(0, (1f - Mathf.Sqrt(dx * dx + dy * dy)) * 2.5f), FilterMode.Bilinear, res);
        Vector3 p = boss.transform.position;
        shadow.transform.position = new Vector3(p.x, TileMath.FloorY(p) + 0.07f, p.z);
        shadow.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        float full = bossUnit.size * bossUnit.tileSize * 1.4f;
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

    void OnDrawGizmos() => ZoneBox.DrawGizmo(transform.position, size);
}
