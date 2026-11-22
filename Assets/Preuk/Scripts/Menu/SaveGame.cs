using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// The game saves by itself; the player never picks a slot or a moment.
//   - Every few seconds while exploring the dungeon (not in a fight, a talk or a cutscene).
//   - Right when a loop starts (LoopManager.Loop): the loop is spent even if you quit straight away.
//   - When the game is closed or the browser tab is hidden, if it's a safe moment.
// CONTINUE on the start screen loads it. GAME OVER and the ending delete it.
// Stored in PlayerPrefs, so it works in the web build too (the browser keeps it).
//
// What a save holds: the loop count, where the party stands, HP, the bag, books, moves learned,
// who was cut free, and which monsters, books, chests and talks are already done.
// The scene starts over on CONTINUE (like a loop) and AutoSave puts all of that back.
public static class SaveGame
{
    const string Key = "autosave_v1";

    [System.Serializable]
    public class ItemCount { public string item; public int count; }

    [System.Serializable]
    public class Book { public string teaches; public string learner; }

    [System.Serializable]
    public class Unit { public string name; public int hp; public List<string> actions = new List<string>(); }

    [System.Serializable]
    public class Data
    {
        public int version = 1;
        public string scene;
        public int loopsUsed;
        public bool hasPosition;      // false = a loop checkpoint: wake up where the trap chest drops you
        public Vector3 position;
        public List<string> rescued = new List<string>();
        public List<ItemCount> items = new List<ItemCount>();
        public List<Book> books = new List<Book>();
        public List<Unit> party = new List<Unit>();
        public List<string> gone = new List<string>();       // monsters killed, books picked up
        public List<string> searched = new List<string>();   // crates and chests opened
        public List<string> played = new List<string>();     // talk zones already heard
    }

    public static bool Exists => PlayerPrefs.HasKey(Key) && Read() != null;

    // The scene was loaded by CONTINUE, and this is what to put back (AutoSave takes it)
    public static Data Pending { get; private set; }
    public static bool Continuing => Pending != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => Pending = null;

    public static Data Read()
    {
        string json = PlayerPrefs.GetString(Key, "");
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonUtility.FromJson<Data>(json); }
        catch { return null; }
    }

    public static void Write(Data d)
    {
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(d));
        PlayerPrefs.Save();
    }

    // GAME OVER, the ending, NEW GAME
    public static void Delete()
    {
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
    }

    // START SCREEN: CONTINUE
    public static bool Continue()
    {
        Data d = Read();
        if (d == null || string.IsNullOrEmpty(d.scene) || !Application.CanStreamedLevelBeLoaded(d.scene)) return false;
        Pending = d;
        LoopManager.SetLoopsUsed(d.loopsUsed);
        RunStats.Rescued.Clear();
        GameState.Current = GameMode.Exploring;
        GameState.InDungeon = true;
        SceneManager.LoadScene(d.scene);
        return true;
    }

    // AutoSave has put everything back
    public static void Done() => Pending = null;

    // A loop was just spent: the world starts over, only the loop count and the books in the bag carry on
    public static void SaveLoopCheckpoint(int loopsUsed)
    {
        var d = new Data { scene = SceneManager.GetActiveScene().name, loopsUsed = loopsUsed, hasPosition = false };
        foreach (ItemData book in PartyInventory.KeptBooks)
            if (book != null && book.teaches != null) d.books.Add(new Book { teaches = book.teaches.name, learner = book.learner });
        Write(d);
    }

    // ---------- Finding assets by name (everything the scene uses is loaded) ----------

    public static T Asset<T>(string name) where T : Object
    {
        if (string.IsNullOrEmpty(name)) return null;
        foreach (T a in Resources.FindObjectsOfTypeAll<T>())
            if (a.name == name) return a;
        return null;
    }
}

// Lives in every game scene (made by itself when one loads). Remembers what the scene looked like at the
// start, saves the differences while you play, and puts a save back after CONTINUE.
[DefaultExecutionOrder(-500)]
public class AutoSave : MonoBehaviour
{
    [Tooltip("Seconds between saves while exploring.")]
    public float every = 3f;

    // Everything the save tracks, by a name that is the same every time the scene loads
    readonly Dictionary<string, GameObject> monsters = new Dictionary<string, GameObject>();
    readonly Dictionary<string, SpellBook> books = new Dictionary<string, SpellBook>();
    readonly Dictionary<string, LootContainer> containers = new Dictionary<string, LootContainer>();
    readonly Dictionary<string, DialogueZone> zones = new Dictionary<string, DialogueZone>();
    readonly Dictionary<string, RescuableAlly> cocoons = new Dictionary<string, RescuableAlly>();

    float timer;
    bool restoring;
    static AutoSave current;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // After every Awake in the scene, before any Start: the scene is still as it was built
    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Object.FindAnyObjectByType<PlayerMovement>() == null) return;   // the start / end screen
        var go = new GameObject("Auto Save");
        SceneManager.MoveGameObjectToScene(go, scene);
        current = go.AddComponent<AutoSave>();
        current.Register();
    }

    void Register()
    {
        foreach (EnemyAI e in FindObjectsByType<EnemyAI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (e.gameObject.activeInHierarchy) monsters[Id(e.transform)] = e.gameObject;
        foreach (SpellBook b in FindObjectsByType<SpellBook>(FindObjectsInactive.Include, FindObjectsSortMode.None)) books[Id(b.transform)] = b;
        foreach (LootContainer c in FindObjectsByType<LootContainer>(FindObjectsInactive.Include, FindObjectsSortMode.None)) containers[Id(c.transform)] = c;
        foreach (DialogueZone z in FindObjectsByType<DialogueZone>(FindObjectsInactive.Include, FindObjectsSortMode.None)) zones[Id(z.transform)] = z;
        foreach (RescuableAlly r in FindObjectsByType<RescuableAlly>(FindObjectsInactive.Include, FindObjectsSortMode.None)) cocoons[Id(r.transform)] = r;
    }

    // "Upper Wing#12/Ossuary#3/Tarzan Brute#5": names plus places in the hierarchy (names repeat)
    static string Id(Transform t)
    {
        string s = $"{t.name}#{t.GetSiblingIndex()}";
        for (Transform p = t.parent; p != null; p = p.parent) s = $"{p.name}#{p.GetSiblingIndex()}/" + s;
        return s;
    }

    IEnumerator Start()
    {
        if (!SaveGame.Continuing) yield break;
        restoring = true;
        yield return null;   // let every Start run (LoopStart, cocoons hiding their allies, followers lining up)
        yield return null;
        Restore(SaveGame.Pending);
        SaveGame.Done();
        restoring = false;
    }

    // ================= Saving =================

    void Update()
    {
        if (restoring || !CanSave) { timer = 0f; return; }
        timer += Time.unscaledDeltaTime;
        if (timer < every) return;
        timer = 0f;
        Save();
    }

    // Closing the game or switching browser tabs
    void OnApplicationPause(bool paused) { if (paused && CanSave) Save(); }
    void OnApplicationFocus(bool focused) { if (!focused && CanSave) Save(); }
    void OnApplicationQuit() { if (CanSave) Save(); }

    // Only in the dungeon, walking about freely: never mid-fight, mid-talk, in a cutscene or on the death screen
    static bool CanSave =>
        GameState.InDungeon && GameState.Current == GameMode.Exploring && !GameState.DialogueOpen &&
        !PauseMenu.IsOpen && !DeathScreen.IsShowing && Leader() != null && Leader().gameObject.activeInHierarchy;

    static PlayerMovement Leader() => FindAnyObjectByType<PlayerMovement>();

    // Pause menu EXIT: save now if it's not mid-fight (the pause menu itself doesn't count)
    public static void SaveNow()
    {
        if (current == null || !GameState.InDungeon || DeathScreen.IsShowing) return;
        if (GameState.Current == GameMode.Combat || GameState.Current == GameMode.Cutscene) return;
        current.Save();
    }

    public void Save()
    {
        PlayerMovement leader = Leader();
        if (leader == null) return;
        var d = new SaveGame.Data
        {
            scene = gameObject.scene.name,
            loopsUsed = LoopManager.LoopsUsed,
            hasPosition = true,
            position = leader.transform.position,
        };
        d.rescued.AddRange(RunStats.Rescued);

        var bag = FindAnyObjectByType<PartyInventory>();
        if (bag != null)
            foreach (PartyInventory.ItemStack s in bag.items)
            {
                if (s.item == null || s.count <= 0) continue;
                if (s.item.effect == ItemEffect.Book) continue;   // books are saved below
                d.items.Add(new SaveGame.ItemCount { item = s.item.name, count = s.count });
            }
        foreach (ItemData book in PartyInventory.KeptBooks)
            if (book != null && book.teaches != null) d.books.Add(new SaveGame.Book { teaches = book.teaches.name, learner = book.learner });

        foreach (CombatUnit u in FindObjectsByType<CombatUnit>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (u.team != Team.Party || !u.gameObject.activeInHierarchy) continue;   // allies still in their cocoon aren't in the party yet
            var unit = new SaveGame.Unit { name = u.unitName, hp = u.CurrentHP };
            foreach (CombatAction a in u.actions) if (a != null) unit.actions.Add(a.name);
            d.party.Add(unit);
        }

        foreach (var pair in monsters)
        {
            GameObject m = pair.Value;
            bool dead = m == null || !m.activeInHierarchy || (m.TryGetComponent(out CombatUnit cu) && !cu.IsAlive);
            if (dead) d.gone.Add(pair.Key);
        }
        foreach (var pair in books) if (pair.Value == null) d.gone.Add(pair.Key);
        foreach (var pair in containers) if (pair.Value != null && pair.Value.Searched) d.searched.Add(pair.Key);
        foreach (var pair in zones) if (pair.Value != null && pair.Value.Played) d.played.Add(pair.Key);

        SaveGame.Write(d);
    }

    // ================= Loading =================

    void Restore(SaveGame.Data d)
    {
        // The world: what's already been done stays done
        foreach (string id in d.gone)
        {
            if (monsters.TryGetValue(id, out GameObject m) && m != null) m.SetActive(false);
            if (books.TryGetValue(id, out SpellBook b) && b != null) Destroy(b.gameObject);
        }
        foreach (string id in d.searched)
            if (containers.TryGetValue(id, out LootContainer c) && c != null) c.MarkSearched();
        foreach (string id in d.played)
            if (zones.TryGetValue(id, out DialogueZone z) && z != null) z.Played = true;

        // Who was cut free walks with the party again
        foreach (string name in d.rescued)
            foreach (RescuableAlly r in cocoons.Values)
                if (r != null && r.allyName == name) r.FreeNow();

        // The bag (books first: they also go back on the list that survives loops).
        // A loop checkpoint keeps the bag a loop starts with, plus the books.
        var bag = FindAnyObjectByType<PartyInventory>();
        if (bag != null)
        {
            if (d.hasPosition) bag.items.Clear();
            foreach (SaveGame.Book b in d.books)
            {
                CombatAction teaches = SaveGame.Asset<CombatAction>(b.teaches);
                if (teaches != null) bag.AddBook(SpellBook.CreateBook(teaches, b.learner));
            }
            foreach (SaveGame.ItemCount s in d.items)
            {
                ItemData item = SaveGame.Asset<ItemData>(s.item);
                if (item != null) bag.items.Add(new PartyInventory.ItemStack { item = item, count = s.count });
            }
        }

        // Moves learned and HP (a loop checkpoint keeps everyone's starting moves and full HP)
        if (d.hasPosition)
            foreach (CombatUnit u in FindObjectsByType<CombatUnit>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (u.team != Team.Party) continue;
                SaveGame.Unit saved = d.party.Find(p => p.name == u.unitName);
                if (saved == null) continue;
                var actions = new List<CombatAction>();
                foreach (string a in saved.actions)
                {
                    CombatAction action = SaveGame.Asset<CombatAction>(a);
                    if (action != null) actions.Add(action);
                }
                if (actions.Count > 0) u.actions = actions;
                if (saved.hp > 0) u.SetHP(saved.hp);
            }

        // Back where they stood
        if (d.hasPosition)
        {
            PlayerMovement leader = Leader();
            if (leader != null) leader.TeleportParty(d.position);
            GameToast.Show("Continued", "The game saves by itself while you explore.");
        }
        Debug.Log($"Save loaded: loop {d.loopsUsed}, {d.gone.Count} things done, {d.rescued.Count} rescued");
    }
}
