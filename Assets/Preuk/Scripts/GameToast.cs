using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A short message near the top of the screen, e.g. "Lucius learned Bless".
// Call GameToast.Show("big line", "small line") from any script. It builds itself the first time.
public class GameToast : MonoBehaviour
{
    static GameToast instance;

    public Color titleColor = new Color32(232, 190, 99, 255);   // candle gold
    public Color textColor = new Color32(217, 203, 176, 255);   // bone
    [Tooltip("Seconds the message stays fully visible.")]
    public float showFor = 3f;
    public float fadeTime = 0.4f;

    CanvasGroup group;
    TextMeshProUGUI title, text;
    Coroutine running;

    public static void Show(string heading, string line)
    {
        if (instance == null)
        {
            instance = new GameObject("Game Toast").AddComponent<GameToast>();
            instance.Build();
        }
        if (instance.running != null) instance.StopCoroutine(instance.running);
        instance.running = instance.StartCoroutine(instance.Play(heading, line));
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    IEnumerator Play(string heading, string line)
    {
        title.text = heading;
        text.text = line;
        yield return Fade(group.alpha, 1f);
        yield return new WaitForSecondsRealtime(showFor);
        yield return Fade(1f, 0f);
        running = null;
    }

    IEnumerator Fade(float from, float to)
    {
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / fadeTime)
        {
            group.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }
        group.alpha = to;
    }

    // ---------- Made in code, so there's nothing to set up ----------

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;   // above the game UI, below the pause menu
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;   // never eats clicks
        group.interactable = false;

        title = MakeText("Title", 40, titleColor, -150f, 60f);
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 4;
        text = MakeText("Text", 24, textColor, -205f, 80f);
        text.textWrappingMode = TextWrappingModes.Normal;
    }

    TextMeshProUGUI MakeText(string objName, float size, Color color, float y, float height)
    {
        var go = new GameObject(objName, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);   // top centre
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(900f, height);
        rt.anchoredPosition = new Vector2(0f, y);

        var t = go.GetComponent<TextMeshProUGUI>();
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Top;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        t.outlineWidth = 0.25f;                     // dark edge so it reads over anything
        t.outlineColor = new Color32(5, 4, 7, 255);
        return t;
    }
}
