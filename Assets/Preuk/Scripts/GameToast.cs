using System.Collections;
using TMPro;
using UnityEngine;

// A short message near the top of the screen, e.g. "Lucius learned Bless".
// Call GameToast.Show("big line", "small line") from any script. It builds itself the first time.
public class GameToast : MonoBehaviour
{
    static GameToast instance;

    public Color titleColor = Palette.CandleGold;
    public Color textColor = Palette.Bone;
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

    IEnumerator Fade(float from, float to) =>
        Tween.Over(fadeTime, t => group.alpha = Mathf.Lerp(from, to, t), unscaled: true);

    // ---------- Made in code, so there's nothing to set up ----------

    void Build()
    {
        UIBuilder.MakeOverlayCanvas(gameObject, UIBuilder.HintOrder);   // above the game UI, below the pause menu

        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;   // never eats clicks
        group.interactable = false;

        title = MakeText("Title", 40, titleColor, -150f, 60f);
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 4;
        text = MakeText("Text", 24, textColor, -205f, 80f, wrap: true);
    }

    TextMeshProUGUI MakeText(string objName, float size, Color color, float y, float height, bool wrap = false)
    {
        TextMeshProUGUI t = UIBuilder.NewText(objName, transform, size, color, TextAlignmentOptions.Top, wrap);
        UIBuilder.AddOutline(t);
        RectTransform rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);   // top centre
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(900f, height);
        rt.anchoredPosition = new Vector2(0f, y);
        return t;
    }
}
