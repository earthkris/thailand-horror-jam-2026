using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The pause menu: BACK, USE ITEM, LOOP, OPTIONS, EXIT.
//   Esc = open / go back one page.   P = open / close.
//   (P because on the web Esc also kicks the browser out of fullscreen.)
//   The small MENU button in the top-right corner opens it too.
// While it's open the game is frozen (Time.timeScale = 0) and nothing else reads keys or clicks.
// The look is all in the Hierarchy (GameMenu prefab): restyle it freely.
[DefaultExecutionOrder(-100)]   // runs before the other menus, so it can see who else wants Esc
public class PauseMenu : MonoBehaviour
{
    // Other scripts check this before reading keys or clicks
    public static bool IsOpen { get; private set; }
    public static bool BlocksInput => IsOpen || DeathScreen.IsShowing || Time.frameCount == closedFrame;
    static int closedFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() { IsOpen = false; closedFrame = -1; }

    [Header("Pages")]
    [Tooltip("The dark backdrop holding every page. Hidden while playing.")]
    public GameObject root;
    public GameObject mainPage;
    public GameObject listPage;
    public GameObject optionsPage;
    public GameObject confirmPage;

    [Header("Main Page")]
    public Button backButton;
    public Button itemButton;
    public Button loopButton;
    public Button optionsButton;
    public Button exitButton;
    [Tooltip("Optional. Shows how many loops are left.")]
    public TextMeshProUGUI loopsText;
    [Tooltip("Optional. One icon per loop: lit while that loop is still left.")]
    public Image[] loopIcons;
    public Sprite loopLeftSprite;
    public Sprite loopUsedSprite;
    [Tooltip("Optional. Small MENU button on screen (mouse / touch / web).")]
    public Button cornerButton;

    [Header("List Page (items, then who to use one on)")]
    public TextMeshProUGUI listTitle;
    [Tooltip("One row. Copied for every choice; the original stays hidden. Needs a child text 'Label'; 'Detail' is optional.")]
    public Button optionTemplate;
    [Tooltip("Text under the list: the hovered item's description, or what just happened.")]
    public TextMeshProUGUI listInfo;
    public Button listBackButton;

    [Header("Options Page")]
    public Slider volumeSlider;
    public TextMeshProUGUI volumeValue;
    [Tooltip("Needs a child text 'Detail' for ON / OFF.")]
    public Button fullscreenButton;
    public Button optionsBackButton;

    [Header("Confirm Page")]
    public TextMeshProUGUI confirmText;
    public Button yesButton;
    public Button noButton;

    [Header("Exit on the web")]
    [Tooltip("A web page can't close its own tab, so EXIT shows this instead.")]
    public GameObject exitScreen;
    public Button exitStartOverButton;
    [Tooltip("Tick to test the web exit screen in the editor.")]
    public bool testWebExitInEditor = false;

    [Range(0f, 1f)] public float disabledDetailAlpha = 0.3f;
    [Range(0f, 1f)] public float disabledLabelAlpha = 0.35f;

    const string VolumeKey = "volume";

    struct Row
    {
        public string label, detail, description;
        public bool enabled;
        public System.Action onPick;
    }

    readonly List<Button> rows = new List<Button>();
    System.Action onBack;   // what Back / Esc does on the page that's showing
    System.Action onYes;
    PartyInventory inventory;
    TurnManager turns;
    CombatTargeting targeting;
    bool exited;

    static bool OnWeb => Application.platform == RuntimePlatform.WebGLPlayer;

    void Awake()
    {
        inventory = FindAnyObjectByType<PartyInventory>();
        turns = FindAnyObjectByType<TurnManager>();
        targeting = FindAnyObjectByType<CombatTargeting>();

        optionTemplate.gameObject.SetActive(false);
        root.SetActive(false);
        if (exitScreen != null) exitScreen.SetActive(false);

        backButton.onClick.AddListener(Close);
        itemButton.onClick.AddListener(ShowItems);
        loopButton.onClick.AddListener(AskLoop);
        optionsButton.onClick.AddListener(ShowOptions);
        exitButton.onClick.AddListener(AskExit);
        if (cornerButton != null) cornerButton.onClick.AddListener(Open);

        listBackButton.onClick.AddListener(Back);
        optionsBackButton.onClick.AddListener(Back);
        yesButton.onClick.AddListener(() => onYes?.Invoke());
        noButton.onClick.AddListener(Back);
        if (exitStartOverButton != null) exitStartOverButton.onClick.AddListener(LoopManager.StartOver);

        // Volume is a setting, not a save: the browser remembers it between visits
        AudioListener.volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
        volumeSlider.SetValueWithoutNotify(AudioListener.volume);
        volumeSlider.onValueChanged.AddListener(SetVolume);
        fullscreenButton.onClick.AddListener(() => Screen.fullScreen = !Screen.fullScreen);
    }

    void OnDestroy()
    {
        // The scene reloaded (LOOP / START OVER) while we were open
        if (IsOpen)
        {
            IsOpen = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }
    }

    void Update()
    {
        if (cornerButton != null)
            cornerButton.gameObject.SetActive(!IsOpen && !DeathScreen.IsShowing && !exited);
        if (optionsPage.activeInHierarchy) RefreshOptionLabels();   // fullscreen can change by itself (Esc in a browser)

        if (exited || DeathScreen.IsShowing || Keyboard.current == null) return;
        bool esc = Keyboard.current.escapeKey.wasPressedThisFrame;
        bool p = Keyboard.current.pKey.wasPressedThisFrame;
        if (!esc && !p) return;

        if (IsOpen)
        {
            if (p) Close(); else Back();
            return;
        }
        if (esc && SomethingElseWantsEsc()) return;   // let aiming / the battle menu close first
        Open();
    }

    // Esc already means "cancel" while aiming or while the ACTION / USE ITEM list is up
    bool SomethingElseWantsEsc()
    {
        if (targeting != null && targeting.IsAiming) return true;
        foreach (ChoiceMenu m in FindObjectsByType<ChoiceMenu>(FindObjectsSortMode.None))
            if (m.IsOpen) return true;
        return false;
    }

    // ================= Open / close =================

    public void Open()
    {
        if (IsOpen || exited || DeathScreen.IsShowing) return;
        IsOpen = true;
        Time.timeScale = 0f;          // freeze everything: walking, monsters, dice
        AudioListener.pause = true;
        root.SetActive(true);
        ShowMain();
    }

    public void Close()
    {
        if (!IsOpen || exited) return;
        IsOpen = false;
        closedFrame = Time.frameCount;   // the key that closed us shouldn't also do something else
        Time.timeScale = 1f;
        AudioListener.pause = false;
        root.SetActive(false);
    }

    void Back() => (onBack ?? Close).Invoke();

    void ShowPage(GameObject page, System.Action back)
    {
        mainPage.SetActive(page == mainPage);
        listPage.SetActive(page == listPage);
        optionsPage.SetActive(page == optionsPage);
        confirmPage.SetActive(page == confirmPage);
        onBack = back;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);   // no stuck highlight
    }

    // ================= Main =================

    void ShowMain()
    {
        ShowPage(mainPage, Close);
        SetButton(loopButton, LoopManager.CanLoop && GameState.InDungeon);   // locked before the dungeon
        if (loopsText != null)
            loopsText.text = !GameState.InDungeon ? "LOOP LOCKED" : LoopManager.CanLoop ? "LOOPS LEFT" : "NO LOOPS LEFT";
        if (loopIcons != null)
            for (int i = 0; i < loopIcons.Length; i++)
                if (loopIcons[i] != null)
                    loopIcons[i].sprite = i < LoopManager.LoopsLeft ? loopLeftSprite : loopUsedSprite;
    }

    // ================= Use Item =================

    void ShowItems()
    {
        bool inBattle = turns != null && turns.InBattle;
        bool canUse = !inBattle || (turns.IsPartyTurn && !turns.HasActed && (targeting == null || !targeting.IsBusy));

        var options = new List<Row>();
        if (inventory != null)
            foreach (PartyInventory.ItemStack s in inventory.items)
            {
                if (s.item == null || s.count <= 0) continue;
                ItemData picked = s.item;
                options.Add(new Row
                {
                    label = s.item.displayName,
                    detail = $"{s.item.RangeText}   x{s.count}",
                    description = s.item.description,
                    enabled = canUse,
                    onPick = () => PickItem(picked)
                });
            }

        string info = options.Count == 0 ? "The bag is empty."
                    : !canUse ? "In a fight, items can only be used on your own turn, before your action."
                    : inBattle ? "Using an item takes your action. Pick one, then click who gets it."
                    : options[0].description;
        ShowList("ITEMS", options, info, ShowMain);
    }

    void PickItem(ItemData item)
    {
        if (turns != null && turns.InBattle)
        {
            // In a fight it works like the battle menu's USE ITEM: close, then click who gets it
            Close();
            turns.ChooseItem(item);
            return;
        }
        ShowTargets(item);
    }

    // Outside a fight the party is together, so anyone can get it
    void ShowTargets(ItemData item)
    {
        var options = new List<Row>();
        foreach (CombatUnit u in PartyMembers())
        {
            bool full = item.effect == ItemEffect.Heal && u.CurrentHP >= u.maxHP;
            CombatUnit who = u;
            options.Add(new Row
            {
                label = u.unitName,
                detail = u.IsAlive ? $"{u.CurrentHP}/{u.maxHP} HP" : "down",
                description = !u.IsAlive ? $"{u.unitName} is down." : full ? $"{u.unitName} is already at full health." : item.description,
                enabled = u.IsAlive && !full,
                onPick = () => UseItemOn(item, who)
            });
        }
        ShowList($"{item.displayName.ToUpper()}  -  ON WHO?", options, item.description, ShowItems);
    }

    void UseItemOn(ItemData item, CombatUnit u)
    {
        if (inventory == null || !inventory.Remove(item))
        {
            ShowItems();
            return;
        }

        string what = $"{u.unitName} uses {item.displayName}.";
        if (item.effect == ItemEffect.Heal)
        {
            int amount = Mathf.Max(DiceSimulator.Roll(item.diceCount, item.diceSides) + item.bonus, 0);
            int before = u.CurrentHP;
            u.Heal(amount);
            what = $"{item.displayName} on {u.unitName}:  +{u.CurrentHP - before} HP  ({u.CurrentHP}/{u.maxHP})   " + DiceFormat.Faint($"{item.DiceText} = {amount}");
        }
        Debug.Log(what);
        ShowItems();
        listInfo.text = what;
    }

    static List<CombatUnit> PartyMembers()
    {
        var list = new List<CombatUnit>();
        foreach (CombatUnit u in FindObjectsByType<CombatUnit>(FindObjectsSortMode.None))
            if (u.team == Team.Party) list.Add(u);
        // The one you walk with comes first
        list.Sort((a, b) => (b.GetComponent<PlayerMovement>() != null).CompareTo(a.GetComponent<PlayerMovement>() != null));
        return list;
    }

    // ================= Loop / Exit =================

    void AskLoop()
    {
        if (!LoopManager.CanLoop || !GameState.InDungeon) return;
        Ask("Loop back in time?\n<size=60%><color=#FFFFFFB3>You will die here, and the loop takes something from you.\n"
            + $"Loops left after this: {LoopManager.LoopsLeft - 1}</color></size>", LoopFromMenu, ShowMain);
    }

    // Looping means dying: the menu closes, the party falls, and the goddess takes it from there
    void LoopFromMenu()
    {
        Close();
        if (GodScene.TakesOverDeath) GodScene.Instance.DieAndLoop();
        else LoopManager.Loop();
    }

    void AskExit()
    {
        Ask("Leave the game?\n<size=60%><color=#FFFFFFB3>There is no save. Everything will be lost.</color></size>", ExitGame, ShowMain);
    }

    // The death screen's EXIT uses this too
    public void ExitGame()
    {
        if (OnWeb || (Application.isEditor && testWebExitInEditor))
        {
            ShowExitScreen();
            return;
        }
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;   // stop Play mode
#else
        Application.Quit();
#endif
    }

    // itch.io / browser: the game can't close the tab, so it says goodbye instead
    void ShowExitScreen()
    {
        root.SetActive(false);
        exited = true;
        IsOpen = true;          // keeps every other script's input off
        Time.timeScale = 0f;
        AudioListener.pause = true;
        Screen.fullScreen = false;
        if (exitScreen != null) exitScreen.SetActive(true);
    }

    void Ask(string question, System.Action yes, System.Action no)
    {
        ShowPage(confirmPage, no);
        confirmText.text = question;
        onYes = yes;
    }

    // ================= Options =================

    void ShowOptions()
    {
        ShowPage(optionsPage, () => { PlayerPrefs.Save(); ShowMain(); });
        volumeSlider.SetValueWithoutNotify(AudioListener.volume);
        RefreshOptionLabels();
    }

    void SetVolume(float v)
    {
        AudioListener.volume = v;
        PlayerPrefs.SetFloat(VolumeKey, v);
        RefreshOptionLabels();
    }

    void RefreshOptionLabels()
    {
        if (volumeValue != null) volumeValue.text = Mathf.RoundToInt(AudioListener.volume * 100f) + "%";
        SetText(fullscreenButton.transform, "Detail", Screen.fullScreen ? "ON" : "OFF", true);
    }

    // ================= List rows =================

    void ShowList(string title, List<Row> options, string info, System.Action back)
    {
        ShowPage(listPage, back);
        listTitle.text = title;
        listInfo.text = info;

        while (rows.Count < options.Count)
            rows.Add(Instantiate(optionTemplate, optionTemplate.transform.parent));

        for (int i = 0; i < rows.Count; i++)
        {
            Button b = rows[i];
            bool used = i < options.Count;
            b.gameObject.SetActive(used);
            if (!used) continue;

            Row r = options[i];
            SetText(b.transform, "Label", r.label, r.enabled);
            SetText(b.transform, "Detail", r.detail, r.enabled);
            b.interactable = r.enabled;

            b.onClick.RemoveAllListeners();
            System.Action pick = r.onPick;
            b.onClick.AddListener(() => pick?.Invoke());

            MenuHover hover = b.GetComponent<MenuHover>();
            if (hover == null) hover = b.gameObject.AddComponent<MenuHover>();
            string desc = r.description;
            hover.onEnter = () => { if (!string.IsNullOrEmpty(desc)) listInfo.text = desc; };
        }
    }

    void SetText(Transform button, string child, string text, bool enabled)
    {
        Transform t = button.Find(child);
        if (t == null || !t.TryGetComponent(out TMP_Text tmp)) return;
        tmp.text = text;
        tmp.alpha = enabled ? 1f : (child == "Label" ? disabledLabelAlpha : disabledDetailAlpha);
    }

    // Turns a button on/off and fades its label to match
    void SetButton(Button b, bool on)
    {
        b.interactable = on;
        TMP_Text label = b.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.alpha = on ? 1f : disabledLabelAlpha;
    }
}
