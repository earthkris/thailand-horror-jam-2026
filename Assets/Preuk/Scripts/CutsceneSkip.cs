using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;

// Lets the player skip Earth's intro video: a SKIP button in the corner, or Space / Enter.
// Skipping jumps the video to its end, so VidPlayer finishes the normal way
// (video canvas off, back to exploring, first dialogue). If the video never got going,
// this does the same ending itself.
// Put it on the same object as the VideoPlayer and VidPlayer.
[RequireComponent(typeof(VideoPlayer))]
public class CutsceneSkip : MonoBehaviour
{
    [Tooltip("The canvas showing the video (hidden when skipped).")]
    public GameObject videoCanvas;
    [Tooltip("Seconds before the SKIP button appears.")]
    public float showAfter = 0.5f;
    [Tooltip("Dialogue to start after the video (dialogueManager list number).")]
    public int dialogueAfter = 0;

    public Color textColor = new Color32(217, 203, 176, 255);    // bone
    public Color hoverColor = new Color32(232, 190, 99, 255);    // candle gold

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
        if (videoCanvas == null) videoCanvas = FindVideoCanvas();
    }

    // A canvas next to us (Earth's VideoCanvas), or any canvas with 'Video' in its name
    GameObject FindVideoCanvas()
    {
        if (transform.parent != null)
            foreach (Transform t in transform.parent)
                if (t != transform && t.GetComponent<Canvas>() != null) return t.gameObject;
        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (c.isRootCanvas && c.name.Contains("Video")) return c.gameObject;
        return null;
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

    // The same ending as VidPlayer
    void Finish()
    {
        if (!gameObject.activeInHierarchy) return;
        video.Stop();
        GameState.Current = GameMode.Exploring;
        if (videoCanvas != null) videoCanvas.SetActive(false);
        if (dialogueManager.Instance != null) dialogueManager.Instance.TriggerDialogue(dialogueAfter);
        gameObject.SetActive(false);
    }

    // ---------- The button (made in code, on top of the video) ----------

    void BuildButton()
    {
        ui = new GameObject("Skip Cutscene", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        ui.transform.SetParent(transform, false);
        var canvas = ui.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = ui.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var btn = new GameObject("Skip", typeof(RectTransform), typeof(Image), typeof(Button));
        btn.transform.SetParent(ui.transform, false);
        var rt = (RectTransform)btn.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);   // bottom-right
        rt.anchoredPosition = new Vector2(-40f, 40f);
        rt.sizeDelta = new Vector2(230f, 60f);

        var bg = btn.GetComponent<Image>();
        bg.color = new Color(0.04f, 0.03f, 0.05f, 0.65f);
        var button = btn.GetComponent<Button>();
        button.targetGraphic = bg;
        button.onClick.AddListener(Skip);

        var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(btn.transform, false);
        var lrt = (RectTransform)label.transform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.sizeDelta = Vector2.zero;
        var t = label.GetComponent<TextMeshProUGUI>();
        t.text = "SKIP  <size=60%><alpha=#99>SPACE</size>";
        t.fontSize = 30;
        t.characterSpacing = 6;
        t.alignment = TextAlignmentOptions.Center;
        t.color = textColor;
        t.raycastTarget = false;

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
