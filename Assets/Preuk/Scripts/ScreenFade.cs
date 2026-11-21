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
        if (current == null) current = FindAnyObjectByType<ScreenFade>();   // one left over from another scene
        if (current == null) current = Build(from);
        if (current.running != null) current.StopCoroutine(current.running);
        current.running = current.StartCoroutine(current.Run(current.image.color.a, to, seconds, hold, removeWhenDone));
    }

    static ScreenFade Build(float alpha)
    {
        GameObject go = UIBuilder.NewOverlayCanvas("Screen Fade", null, UIBuilder.FadeOrder).gameObject;   // above the battle UI, dialogue and menus
        DontDestroyOnLoad(go);

        var fade = go.AddComponent<ScreenFade>();
        fade.image = UIBuilder.NewImage("Black", go.transform);   // never blocks clicks
        UIBuilder.Stretch(fade.image.rectTransform);
        fade.image.color = new Color(0f, 0f, 0f, alpha);
        return fade;
    }

    IEnumerator Run(float from, float to, float seconds, float hold, bool removeWhenDone)
    {
        if (hold > 0f) yield return new WaitForSecondsRealtime(hold);
        yield return Tween.Over(seconds, k => image.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, k)), unscaled: true, smooth: true);
        running = null;
        if (removeWhenDone)
        {
            if (current == this) current = null;
            Destroy(gameObject);
        }
    }
}
