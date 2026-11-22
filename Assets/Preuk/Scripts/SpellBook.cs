using UnityEngine;

// A spell book lying on the floor. Stand next to it and press E (or click it) and it goes into
// the party's bag. Read it from the menu (ITEMS) and one of the party learns a new move,
// e.g. the yellow book teaches Lucius 'Bless'. The move shows up in their ACTION menu from the next fight on.
// The book stays in the bag through a loop, so after a loop you only have to read it again.
// On a loop where the book is already in the bag, a torn page lies here instead: read it (E) for a clue.
// (Or, if 'Replacement' is set, the book is gone and that object shows instead.)
// Put the book on a tile the player can reach.
public class SpellBook : Interactable
{
    [Tooltip("The move it teaches (a Combat Action asset).")]
    public CombatAction teaches;
    [Tooltip("Who learns it: the character's object name (Kairos, Lucius) or their Unit Name.")]
    public string learner = "Lucius";

    [Header("Later loops: a torn page")]
    [Tooltip("What the torn page says. Shown in the dialogue box. Make it a real hint for the next try.")]
    [TextArea(2, 6)] public string clue = "They feed on the fallen. Never leave a friend lying on the stones. Pull them up before the things in the dark reach them.";
    public string pageName = "Torn page";
    [Tooltip("The page's colour: the book sprite, faded.")]
    public Color pageTint = new Color(0.55f, 0.5f, 0.45f, 1f);
    [Tooltip("Optional. Shown instead of the torn page (the book is gone). Not a child of the book.")]
    public GameObject replacement;

    bool isPage;   // this loop it's a torn page, not the book

    [Header("Look")]
    [Tooltip("The book sprite. It floats gently up and down (never below where you put it).")]
    public Transform visual;
    public float bobHeight = 0.08f;
    public float bobSpeed = 2f;

    Vector3 home;

    // '[E]  Pick up Bless', or '[E]  Read Torn page'
    protected override string Prompt => isPage ? "Read " + pageName
                                      : teaches != null ? promptText + " " + teaches.displayName : promptText;

    protected override void Start()
    {
        bool carried = teaches != null && PartyInventory.HasBookFor(teaches);
        if (replacement != null) replacement.SetActive(carried);
        if (carried && replacement != null)
        {
            Destroy(gameObject);
            return;
        }

        base.Start();
        if (visual == null && transform.childCount > 0) visual = transform.GetChild(0);
        if (visual != null) home = visual.localPosition;
        if (carried) BecomePage();
    }

    // Same sprite, faded and smaller, lying still on the floor
    void BecomePage()
    {
        isPage = true;
        bobHeight = 0f;
        if (visual == null) return;
        visual.localScale *= 0.7f;
        foreach (SpriteRenderer sr in visual.GetComponentsInChildren<SpriteRenderer>()) sr.color *= pageTint;
    }

    void ReadPage()
    {
        if (player != null) player.StopWalking();
        if (DialogueBox.Instance == null)
        {
            GameToast.Show(pageName, clue);
            return;
        }
        var page = ScriptableObject.CreateInstance<DialogueScriptableObject>();
        SpriteRenderer look = visual != null ? visual.GetComponentInChildren<SpriteRenderer>() : null;
        page.lines.Add(new DialogueScriptableObject.Line { name = pageName, text = clue, spriteImage = look != null ? look.sprite : null });
        DialogueBox.Instance.StartDialogue(page);
    }

    protected override void Update()
    {
        if (visual != null)
        {
            float up = (Mathf.Sin(Time.time * bobSpeed) * 0.5f + 0.5f) * bobHeight;   // 0..bobHeight
            visual.localPosition = home + Vector3.up * up;
        }
        base.Update();   // E key, clicks, the prompt
    }

    protected override void Interact()
    {
        if (isPage) { ReadPage(); return; }   // stays, so it can be read again

        PartyInventory bag = FindAnyObjectByType<PartyInventory>();
        if (bag == null || teaches == null || FindLearner(learner) == null)
        {
            Debug.LogWarning($"SpellBook '{name}': no party bag, nobody called '{learner}' in the party, or no move set.", this);
            enabled = false;
            return;
        }

        ItemData book = MakeBook();
        bag.AddBook(book);
        Sfx.Play("book_pickup");
        Debug.Log($"Picked up {book.displayName}.");
        GameToast.Show($"{book.displayName} is in your bag", "Read it from the menu (ITEMS)");
        Destroy(gameObject);
    }

    // The bag item for this book. Made in code, so a new book needs no Item asset.
    // Kept loaded while the scene reloads for a loop.
    ItemData MakeBook() => CreateBook(teaches, learner);

    // Also used by the save to put books back in the bag
    public static ItemData CreateBook(CombatAction teaches, string learner)
    {
        var book = ScriptableObject.CreateInstance<ItemData>();
        book.hideFlags = HideFlags.DontUnloadUnusedAsset;
        book.name = book.displayName = $"Book of {teaches.displayName}";
        book.description = $"Teaches {learner} {teaches.displayName}. {teaches.description}";
        book.effect = ItemEffect.Book;
        book.target = ActionTarget.Self;
        book.teaches = teaches;
        book.learner = learner;
        return book;
    }

    // ---------- Reading (PauseMenu) ----------

    public static bool HasRead(ItemData book)
    {
        CombatUnit who = FindLearner(book.learner);
        return who != null && who.actions.Contains(book.teaches);
    }

    // Teaches the book's move. The book stays in the bag.
    public static string Read(ItemData book)
    {
        CombatUnit who = FindLearner(book.learner);
        if (who == null || book.teaches == null) return $"Nobody can make sense of {book.displayName}.";
        if (!who.actions.Contains(book.teaches)) who.actions.Add(book.teaches);
        Sfx.Play("book_read");
        Sfx.Play("buff", 0.6f);
        Debug.Log($"{who.unitName} learned {book.teaches.displayName}!");
        GameToast.Show($"{who.unitName} learned {book.teaches.displayName}", book.teaches.description);
        return $"{who.unitName} learned {book.teaches.displayName}.";
    }

    static CombatUnit FindLearner(string learner)
    {
        foreach (CombatUnit u in CombatUnit.OnTeam(Team.Party))
            if (u.gameObject.name == learner || u.unitName == learner)
                return u;
        return null;
    }
}
