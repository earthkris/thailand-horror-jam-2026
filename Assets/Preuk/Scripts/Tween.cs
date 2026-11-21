using System.Collections;
using UnityEngine;

// Small animation helper for coroutines:  yield return Tween.Over(0.5f, t => thing.alpha = t);
public static class Tween
{
    // Calls step(0..1) every frame for 'seconds', and ends on exactly step(1).
    //   unscaled: keeps running while the game is frozen (menus, hit-stop).
    //   smooth:   eases in and out instead of moving at a steady pace.
    public static IEnumerator Over(float seconds, System.Action<float> step, bool unscaled = false, bool smooth = false)
    {
        seconds = Mathf.Max(seconds, 0.01f);
        for (float t = 0f; t < 1f; t += (unscaled ? Time.unscaledDeltaTime : Time.deltaTime) / seconds)
        {
            step(smooth ? Smooth(t) : t);
            yield return null;
        }
        step(1f);
    }

    // Slow start, slow end
    public static float Smooth(float t) => t * t * (3f - 2f * t);
}
