using UnityEngine;

// Party members who are downed (or dead), and anyone knocked prone, lie on their side: the body sprite tips over,
// and their floating prop (Kairos's sword, Lucius's book) is put away. They stand back up when revived.
// CombatUnit adds this to party members by itself; nothing to set up.
public class DownedPose : MonoBehaviour
{
    [Tooltip("Degrees it tips over. 90 = flat on its side.")]
    public float angle = 90f;
    [Tooltip("Lying down, how high the middle of the body is above the floor (half the body's thickness).")]
    public float restHeight = 0.3f;
    [Tooltip("Seconds to fall over / get back up.")]
    public float fallTime = 0.25f;

    CombatUnit unit;
    SpriteAnimator body;
    SpriteRenderer sr;
    Vector3 bodyHome;
    Vector3 standCenter;   // middle of the sprite when standing, from our root
    float floorY;          // the feet's height, from our root
    PropSlot[] props;
    float t;               // 0 = standing, 1 = lying down

    void Awake()
    {
        unit = GetComponent<CombatUnit>();
        body = GetComponentInChildren<SpriteAnimator>(true);
        props = GetComponentsInChildren<PropSlot>(true);
        if (body == null) return;
        sr = body.GetComponent<SpriteRenderer>();
        bodyHome = body.transform.localPosition;
        if (sr != null) Measure();   // still standing here
    }

    void Measure()
    {
        standCenter = sr.bounds.center - transform.position;
        floorY = sr.bounds.min.y - transform.position.y;
    }

    // After SpriteAnimator (it only mirrors with scale, so the tilt stays ours)
    void LateUpdate()
    {
        if (unit == null || body == null || sr == null) return;
        // Monsters only lie down when knocked prone (a dead one is hidden; HitEffects plays its collapse)
        bool down = (unit.team == Team.Party && !unit.IsAlive) || (unit.IsAlive && unit.Has(StatusType.Prone));
        float before = t;
        t = Mathf.MoveTowards(t, down ? 1f : 0f, Time.deltaTime / Mathf.Max(fallTime, 0.01f));
        if (t == before && t == 0f) { Measure(); return; }   // standing, nothing to do

        // Tip over round the middle, then let the middle sink to just above the floor
        Transform b = body.transform;
        b.localRotation = Quaternion.Euler(0f, 0f, angle * t);
        b.localPosition = bodyHome;
        Vector3 lying = new Vector3(standCenter.x, floorY + restHeight, standCenter.z);
        Vector3 want = transform.position + Vector3.Lerp(standCenter, lying, t);
        b.position += want - sr.bounds.center;

        foreach (PropSlot p in props)
            if (p != null && p.gameObject.activeSelf == down) p.gameObject.SetActive(!down);
    }
}
