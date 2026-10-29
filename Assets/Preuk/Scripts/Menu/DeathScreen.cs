using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Sekiro-style death screen, shown when the whole party falls.
//   Loops left -> white "YOU'RE DEAD"   [LOOP BACK]  [ACCEPT DEATH]
//   None left  -> red   "GAME OVER"     [START OVER] [EXIT]
// The title is painted on left to right like a brush stroke (a RectMask2D wipe).
// Runs on unscaled time, so it still plays if the game is frozen.
public class DeathScreen : MonoBehaviour
{
    public static bool IsShowing { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => IsShowing = false;

    [Header("Parts")]
    [Tooltip("The whole death screen. Hidden until the party falls.")]
    public CanvasGroup screen;
    [Tooltip("Parent of the title, with a RectMask2D. Wiping its padding paints the title on.")]
    public RectMask2D titleMask;
    public TextMeshProUGUI title;
    public TextMeshProUGUI subtitle;
    public CanvasGroup buttons;
    public Button loopButton;
    public Button acceptButton;
    public Button startOverButton;
    public Button exitButton;
    [Tooltip("EXIT goes through the pause menu (it knows how to leave on the web). Found automatically if empty.")]
    public PauseMenu pauseMenu;

    [Header("Text")]
    public string deadText = "YOU'RE DEAD";
    public string gameOverText = "GAME OVER";
    [Tooltip("{0} = how many loops are left, e.g. '2 loops left'")]
    public string loopSubtitle = "The candle still flickers.    {0}";
    public string finalSubtitle = "The candle has gone out.";

    [Header("Colors")]
    public Color loopColor = new Color32(245, 242, 236, 255);   // white
    public Color finalColor = new Color32(194, 59, 59, 255);     // blood

    [Header("Timing (seconds)")]
    [Tooltip("Wait after the last hit before the screen darkens.")]
    public float delay = 1f;
    public float fadeTime = 1.5f;
    public float paintTime = 1.1f;
    public float buttonsDelay = 0.6f;

    float baseSpacing;

    void Awake()
    {
        if (pauseMenu == null) pauseMenu = FindAnyObjectByType<PauseMenu>();
        baseSpacing = title.characterSpacing;
        screen.gameObject.SetActive(false);

        loopButton.onClick.AddListener(LoopManager.Loop);
        acceptButton.onClick.AddListener(() => StartCoroutine(AcceptDeath()));
        startOverButton.onClick.AddListener(LoopManager.StartOver);
        exitButton.onClick.AddListener(() => { if (pauseMenu != null) pauseMenu.ExitGame(); });
    }

    void OnEnable() => TurnManager.OnPartyFallen += Show;
    void OnDisable() => TurnManager.OnPartyFallen -= Show;
    void OnDestroy() => IsShowing = false;

    // Right-click the component header -> "Test Death" (in Play mode) to see it without dying
    [ContextMenu("Test Death")]
    public void Show()
    {
        if (IsShowing || !Application.isPlaying) return;
        StartCoroutine(Play(final: !LoopManager.CanLoop));
    }

    IEnumerator Play(bool final)
    {
        IsShowing = true;
        GameState.Current = GameMode.Menu;   // nothing moves behind the screen

        yield return new WaitForSecondsRealtime(delay);

        screen.gameObject.SetActive(true);
        screen.alpha = 0f;
        HideButtons();
        SetLook(final);
        SetPaint(0f);
        subtitle.alpha = 0f;

        yield return Fade(screen, 0f, 1f, fadeTime);   // the world goes dark
        yield return Paint();                          // the words brush on
        yield return new WaitForSecondsRealtime(buttonsDelay);
        yield return ShowButtons();
    }

    // ACCEPT DEATH: give up the remaining loops. The white words are wiped away and come back red.
    IEnumerator AcceptDeath()
    {
        HideButtons();
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.5f)
        {
            SetPaint(1f - t);
            subtitle.alpha = 1f - t;
            yield return null;
        }
        SetPaint(0f);
        subtitle.alpha = 0f;

        SetLook(true);
        yield return new WaitForSecondsRealtime(0.4f);
        yield return Paint();
        yield return new WaitForSecondsRealtime(buttonsDelay);
        yield return ShowButtons();
    }

    void SetLook(bool final)
    {
        title.text = final ? gameOverText : deadText;
        title.color = final ? finalColor : loopColor;

        int left = LoopManager.LoopsLeft;
        subtitle.text = final ? finalSubtitle
                              : string.Format(loopSubtitle, left == 1 ? "1 loop left" : $"{left} loops left");

        loopButton.gameObject.SetActive(!final);
        acceptButton.gameObject.SetActive(!final);
        startOverButton.gameObject.SetActive(final);
        exitButton.gameObject.SetActive(final);
    }

    IEnumerator Paint()
    {
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / paintTime)
        {
            SetPaint(1f - Mathf.Pow(1f - t, 3f));   // fast stroke that slows at the end
            yield return null;
        }
        SetPaint(1f);
        yield return Fade(subtitle, 0.6f);
    }

    // 0 = nothing painted yet, 1 = the whole title showing
    void SetPaint(float amount)
    {
        float width = titleMask.rectTransform.rect.width + titleMask.softness.x;
        titleMask.padding = new Vector4(0f, 0f, width * (1f - amount), 0f);   // left, bottom, RIGHT, top
        title.characterSpacing = baseSpacing + 20f * (1f - amount);          // letters settle into place
        title.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.05f, 1f, amount);
    }

    void HideButtons()
    {
        buttons.alpha = 0f;
        buttons.interactable = false;
        buttons.blocksRaycasts = false;
    }

    IEnumerator ShowButtons()
    {
        yield return Fade(buttons, 0f, 1f, 0.6f);
        buttons.interactable = true;
        buttons.blocksRaycasts = true;
    }

    static IEnumerator Fade(CanvasGroup g, float from, float to, float time)
    {
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(time, 0.01f))
        {
            g.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }
        g.alpha = to;
    }

    static IEnumerator Fade(TMP_Text text, float time)
    {
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(time, 0.01f))
        {
            text.alpha = t;
            yield return null;
        }
        text.alpha = 1f;
    }
}
