using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// A full-screen black fade, on top of everything. Builds itself; one at a time.
//   ScreenFade.FadeOut(0.6f)   game -> black (stays black until a FadeIn)
//   ScreenFade.FadeIn(1.2f)    black -> game, then cleans itself up
public class ScreenFade : MonoBehaviour
{
    public static bool Quitting { get; private set; }

    static ScreenFade current;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Quitting = false;
        current = null;
        Application.quitting -= OnQuit;
        Application.quitting += OnQuit;
    }

    static void OnQuit() => Quitting = true;

    public static void FadeIn(float seconds, float holdBlack = 0.15f) => Play(1f, 0f, seconds, holdBlack, true);
    public static void FadeOut(float seconds) => Play(0f, 1f, seconds, 0f, false);

    Image image;
    Coroutine running;

    // 'from' is only used when no fade is on screen; otherwise it carries on from where it is
    static void Play(float from, float to, float seconds, float hold, bool removeWhenDone)
    {
        if (current == null) current = Build(from);
        if (current.running != null) current.StopCoroutine(current.running);
        current.running = current.StartCoroutine(current.Run(current.image.color.a, to, seconds, hold, removeWhenDone));
    }

    static ScreenFade Build(float alpha)
    {
        var go = new GameObject("Screen Fade", typeof(Canvas), typeof(CanvasScaler));
        DontDestroyOnLoad(go);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;   // above the battle UI, dialogue and menus

        var img = new GameObject("Black", typeof(RectTransform), typeof(Image));
        img.transform.SetParent(go.transform, false);
        var rt = (RectTransform)img.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        var fade = go.AddComponent<ScreenFade>();
        fade.image = img.GetComponent<Image>();
        fade.image.raycastTarget = false;   // never blocks clicks
        fade.image.color = new Color(0f, 0f, 0f, alpha);
        return fade;
    }

    IEnumerator Run(float from, float to, float seconds, float hold, bool removeWhenDone)
    {
        if (hold > 0f) yield return new WaitForSecondsRealtime(hold);
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(seconds, 0.01f))
        {
            float k = t * t * (3f - 2f * t);   // smooth start and end
            image.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, k));
            yield return null;
        }
        image.color = new Color(0f, 0f, 0f, to);
        running = null;
        if (removeWhenDone)
        {
            if (current == this) current = null;
            Destroy(gameObject);
        }
    }
}
