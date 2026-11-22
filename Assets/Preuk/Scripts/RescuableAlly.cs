using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Someone wrapped in spider silk in the upper dungeon. Stand next to the cocoon and press E (or click it)
// to cut them free: they talk, then join the party and walk at the back of the line.
// They fight like Kairos and Lucius (their own CombatUnit and actions).
// Every loop puts them back in the silk (the scene starts over), but they remember a little more each time.
//
// Set up:  Cocoon (this script, on the floor tile)
//            Visual    <- the wrapped sprite (made in code if empty)
//          The ally itself: a party member object anywhere in the scene, switched OFF.
public class RescuableAlly : Interactable
{
    [Header("Who is inside")]
    [Tooltip("The party member, switched off until rescued.")]
    public GameObject ally;
    public string allyName = "Stranger";
    [Tooltip("What they say when cut free. Element 0 = first run, 1 = after the 1st loop... The last one is reused.")]
    public DialogueScriptableObject[] rescueByLoop;

    [Header("Look")]
    [Tooltip("Their idle sprite, drawn grey and wrapped. Empty = the ally's first sprite.")]
    public Sprite wrappedSprite;
    [Tooltip("Silk drawn over them (e.g. the web projectile sprite).")]
    public Sprite silkSprite;
    public Color wrapTint = new Color(0.62f, 0.6f, 0.66f, 1f);

    Transform visual;
    bool freeing;

    protected override string Prompt => promptText + " " + allyName;   // "Cut free Theron"

    protected override void Start()
    {
        if (promptText == "Pick up") promptText = "Cut free";   // the base class default doesn't fit a cocoon
        if (ally != null) ally.SetActive(false);
        BuildVisual();
        base.Start();
    }

    void BuildVisual()
    {
        visual = transform.Find("Visual");
        if (visual != null) return;
        visual = new GameObject("Visual").transform;
        visual.SetParent(transform, false);

        Sprite body = wrappedSprite;
        if (body == null && ally != null)
        {
            SpriteRenderer sr = ally.GetComponentInChildren<SpriteRenderer>(true);
            if (sr != null) body = sr.sprite;
        }
        var bodyGo = new GameObject("Body", typeof(SpriteRenderer));
        bodyGo.transform.SetParent(visual, false);
        var bodySr = bodyGo.GetComponent<SpriteRenderer>();
        bodySr.sprite = body;
        bodySr.color = wrapTint;
        if (ally != null)
        {
            SpriteRenderer src = ally.GetComponentInChildren<SpriteRenderer>(true);
            if (src != null)
            {
                bodySr.sharedMaterial = src.sharedMaterial;
                // Where their sprite sits on their own body, moved onto the cocoon's floor tile
                Vector3 onBody = src.transform.position - ally.transform.position;
                bodyGo.transform.localPosition = onBody + Vector3.up * (ally.transform.position.y - transform.position.y + 0.05f);
                bodyGo.transform.localScale = src.transform.lossyScale;
            }
        }

        if (silkSprite == null) return;
        for (int i = 0; i < 3; i++)   // three strands of silk across the body
        {
            var silk = new GameObject("Silk " + (i + 1), typeof(SpriteRenderer));
            silk.transform.SetParent(visual, false);
            var s = silk.GetComponent<SpriteRenderer>();
            s.sprite = silkSprite;
            s.sharedMaterial = bodySr.sharedMaterial;
            s.color = new Color(0.92f, 0.92f, 0.95f, 0.85f);
            s.sortingOrder = bodySr.sortingOrder + 1;
            float across = silkSprite.rect.width / silkSprite.pixelsPerUnit;
            float scale = 0.95f / Mathf.Max(across, 0.01f);
            silk.transform.localScale = Vector3.one * scale;
            silk.transform.localPosition = bodyGo.transform.localPosition + new Vector3(0f, 0.3f + i * 0.35f, -0.02f);
            silk.transform.localRotation = Quaternion.Euler(0f, 0f, i == 1 ? -18f : 15f);
        }
    }

    protected override void Update()
    {
        if (visual != null && !freeing)   // a faint struggle inside the silk
            visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 2.3f) * 2.5f * Mathf.Max(0f, Mathf.Sin(Time.time * 0.7f)));
        base.Update();
    }

    protected override void Interact()
    {
        if (freeing || ally == null) return;
        StartCoroutine(Free());
    }

    IEnumerator Free()
    {
        freeing = true;
        GameState.Current = GameMode.Cutscene;
        if (player != null) player.StopWalking();

        // Hack at the silk
        for (int i = 0; i < 3; i++)
        {
            Sfx.Play("swing", 0.9f, 1f + i * 0.08f);
            if (visual != null) visual.localRotation = Quaternion.Euler(0f, 0f, i % 2 == 0 ? 8f : -8f);
            yield return new WaitForSeconds(0.22f);
        }
        Sfx.Play("unlock", 0.8f);

        // The silk falls away
        if (visual != null)
        {
            Vector3 size = visual.localScale;
            var renderers = visual.GetComponentsInChildren<SpriteRenderer>();
            yield return Tween.Over(0.35f, t =>
            {
                visual.localScale = new Vector3(size.x * (1f + 0.3f * t), size.y * (1f - 0.2f * t), size.z);
                foreach (SpriteRenderer r in renderers) { Color c = r.color; c.a = 1f - t; r.color = c; }
            });
            visual.gameObject.SetActive(false);
        }

        // Out they step, at the back of the line
        Vector3 spot = transform.position;
        spot.y = ally.transform.position.y;
        ally.transform.position = TileMath.Snap(spot, player != null ? player.tileSize : 1f);
        ally.SetActive(true);
        if (ally.TryGetComponent(out Follower follower))
        {
            follower.leader = player;
            follower.FollowBehind(LastInLine(follower));
        }
        Sfx.Play("heal", 0.6f);
        yield return new WaitForSeconds(0.3f);

        GameState.Current = GameMode.Exploring;
        DialogueScriptableObject lines = StoryDialogue.Pick(rescueByLoop, LoopManager.LoopsUsed);
        yield return StoryDialogue.Play(lines);
        if (!RunStats.Rescued.Contains(allyName)) RunStats.Rescued.Add(allyName);
        GameToast.Show($"{allyName} joins the party", "They fight beside you from the next battle on.");
        Destroy(gameObject);
    }

    // A saved game: they were cut free before, so they're simply in the party (no silk, no talk)
    public void FreeNow()
    {
        if (ally == null || freeing) return;
        freeing = true;
        if (visual != null) visual.gameObject.SetActive(false);
        Vector3 spot = transform.position;
        spot.y = ally.transform.position.y;
        ally.transform.position = TileMath.Snap(spot, player != null ? player.tileSize : 1f);
        ally.SetActive(true);
        if (ally.TryGetComponent(out Follower follower))
        {
            follower.leader = player != null ? player : FindAnyObjectByType<PlayerMovement>();
            follower.FollowBehind(LastInLine(follower));
        }
        if (!RunStats.Rescued.Contains(allyName)) RunStats.Rescued.Add(allyName);
        Destroy(gameObject);
    }

    // The follower nobody walks behind yet
    static Follower LastInLine(Follower newcomer)
    {
        var all = new List<Follower>(FindObjectsByType<Follower>(FindObjectsSortMode.None));
        all.Remove(newcomer);
        foreach (Follower f in all)
        {
            bool someoneBehind = false;
            foreach (Follower g in all)
                if (g != f && g.ahead == f) { someoneBehind = true; break; }
            if (!someoneBehind) return f;
        }
        return null;   // nobody else: walk right behind the leader
    }
}
