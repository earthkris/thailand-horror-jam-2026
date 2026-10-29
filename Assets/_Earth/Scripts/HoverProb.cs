using UnityEngine;

// Floats a prop (Lucius's book, Kairos's sword) gently up and down.
//   Prob     = the prop to float.
//   Position = how high it floats (same numbers as before: 0.01 rises about 0.3 tiles at speed 0.5).
//   Speed    = slowness: one full up-and-down takes about 6 x Speed seconds.
// It floats up from where the prop starts and comes back down to it, never lower,
// so it can't sink through the floor, and it looks the same at any frame rate (web too).
public class HoverProb : MonoBehaviour
{
    [SerializeField] Transform prob;
    [SerializeField] Vector3 position;
    [SerializeField] float speed;

    Vector3 home;      // where the prop rests (the bottom of the float)
    float startTime;
    bool ready;

    void Start()
    {
        if (!prob) return;
        home = prob.localPosition;
        startTime = Time.time;
        ready = true;
    }

    void Update()
    {
        if (!prob || !ready) return;
        float s = Mathf.Max(speed, 0.01f);
        float t = (Time.time - startTime) / s;
        // The old version added Position every frame (at ~60 fps); this is the same height, done properly
        float height = s * 60f * (1f - Mathf.Cos(t));
        prob.localPosition = home + position * height;
    }
}
