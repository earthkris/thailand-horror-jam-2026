using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;

// Lets the player skip Earth's intro video: a SKIP button in the corner, or Space / Enter.
// Skipping jumps the video to its end, so VidPlayer finishes the normal way
// (video canvas off, back to exploring, first dialogue). If the video never got going,
// VidPlayer is told to finish right away.
// Every VidPlayer gets one by itself; nothing to set up in the scene.
[RequireComponent(typeof(VideoPlayer), typeof(VidPlayer))]
public class CutsceneSkip : MonoBehaviour
{
    [Tooltip("Seconds before the SKIP button appears.")]
    public float showAfter = 0.5f;

    public Color textColor = Palette.Bone;
    public Color hoverColor = Palette.CandleGold;

    VideoPlayer video;
    GameObject ui;
    float showAt;
    bool skipping;
    bool finished;   // the video reached its end (watched or skipped)

    [Tooltip("Seconds the game fades in from black after the video.")]
    public float fadeInTime = 1.2f;

    // Every cutscene (VidPlayer) gets a skip button by itself: nothing to set up in the scene
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AttachToCutscenes()
    {
        foreach (VidPlayer vp in FindObjectsByType<VidPlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (vp.GetComponent<CutsceneSkip>() == null) vp.gameObject.AddComponent<CutsceneSkip>();
    }

    void Awake()
    {
        video = GetComponent<VideoPlayer>();
        video.loopPointReached += _ => finished = true;
    }

    void OnEnable() => showAt = Time.unscaledTime + showAfter;

    // VidPlayer (or Finish below) switches this object off when the video is done: fade the game in
    void OnDisable()
    {
        if ((finished || skipping) && Application.isPlaying && !ScreenFade.Quitting)
            ScreenFade.FadeIn(fadeInTime);
    }

    void Update()
    {
        if (skipping || Time.unscaledTime < showAt) return;
        if (ui == null) BuildButton();

        var kb = Keyboard.current;
        if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
            Skip();
    }

    public void Skip()
    {
        if (skipping) return;
        skipping = true;
        if (ui != null) ui.SetActive(false);

        if (video.isPrepared && video.length > 0.3)
        {
            video.time = video.length - 0.1;   // VidPlayer's 'video finished' runs from here
            if (!video.isPlaying) video.Play();
            StartCoroutine(FinishIfStuck(1.5f));
        }
        else Finish();
    }

    // Still here after the jump? Do the ending ourselves.
    IEnumerator FinishIfStuck(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        Finish();
    }

    // The same ending as a video that played to its end
    void Finish()
    {
        if (!gameObject.activeInHierarchy) return;
        video.Stop();
        GetComponent<VidPlayer>().Finish();
    }

    // ---------- The button (made in code, on top of the video) ----------

    void BuildButton()
    {
        ui = UIBuilder.NewOverlayCanvas("Skip Cutscene", transform, UIBuilder.SkipOrder, clickable: true).gameObject;

        Image bg = UIBuilder.NewImage("Skip", ui.transform, raycastTarget: true);
        bg.color = new Color(0.04f, 0.03f, 0.05f, 0.65f);
        RectTransform rt = bg.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);   // bottom-right
        rt.anchoredPosition = new Vector2(-40f, 40f);
        rt.sizeDelta = new Vector2(230f, 60f);

        GameObject btn = bg.gameObject;
        var button = btn.AddComponent<Button>();
        button.targetGraphic = bg;
        button.onClick.AddListener(Skip);

        TextMeshProUGUI t = UIBuilder.NewText("Label", btn.transform, 30, textColor, TextAlignmentOptions.Center, wrap: true);
        UIBuilder.Stretch(t.rectTransform);
        t.text = "SKIP  <size=60%><alpha=#99>SPACE</size>";
        t.characterSpacing = 6;

        // Text turns gold on hover
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.2f, 1.1f, 0.9f);
        button.colors = colors;
        var hover = btn.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        AddHover(hover, UnityEngine.EventSystems.EventTriggerType.PointerEnter, t, hoverColor);
        AddHover(hover, UnityEngine.EventSystems.EventTriggerType.PointerExit, t, textColor);
    }

    static void AddHover(UnityEngine.EventSystems.EventTrigger trigger, UnityEngine.EventSystems.EventTriggerType type, TextMeshProUGUI text, Color color)
    {
        var entry = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => text.color = color);
        trigger.triggers.Add(entry);
    }
}
