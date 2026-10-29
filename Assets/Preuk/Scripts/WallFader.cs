using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Front walls turn see-through once a character is past them, so the near wall of a room never blocks the view.
//   Front wall = a tall wall running left-right across the screen (much wider in X than deep in Z).
//   Crates, chairs, pillars and side walls (running into the screen) are never touched.
//   'Past it' = the character stands further into the screen (higher Z) than the wall, within View Depth.
// The see-through look comes from SeeThroughMaterials (Resources). Parts with no see-through copy
// (e.g. torches with their own shader) are hidden instead. Walls still block movement.
// Watches the party and whoever the camera follows (the unit whose turn it is in a fight).
// Put it on the Main Camera.
[RequireComponent(typeof(Camera))]
public class WallFader : MonoBehaviour
{
    [Tooltip("How solid a see-through wall is. 0 = gone, 1 = solid.")]
    [Range(0f, 1f)] public float wallOpacity = 0.25f;
    [Tooltip("Which layers count as walls (Obstacles).")]
    public LayerMask wallLayers = 1 << 6;
    [Tooltip("Fade front walls up to this many tiles in front of a character.")]
    public float viewDepth = 10f;
    [Tooltip("Fade front walls up to this many tiles to the left/right of a character.")]
    public float viewWidth = 14f;
    [Tooltip("Only things at least this tall count as walls (crates, chairs and tables stay).")]
    public float minWallHeight = 2f;
    [Tooltip("A wall must be this many times wider than it is deep (pillars stay).")]
    public float minWidthToDepth = 1.5f;

    class Part
    {
        public Renderer renderer;
        public Material[] solid;          // the wall's own materials
        public Material[] faded;          // see-through copies, or null = just hide it
        public ShadowCastingMode shadows;
    }

    class Wall
    {
        public Bounds bounds;
        public List<Part> parts = new List<Part>();
        public bool faded;
    }

    readonly List<Wall> frontWalls = new List<Wall>();
    readonly List<Transform> watched = new List<Transform>();
    readonly Dictionary<Material, Material> liveCopies = new Dictionary<Material, Material>();   // see-through asset -> this run's copy
    CameraController follow;
    SeeThroughMaterials set;
    float appliedOpacity = -1f;
    [System.NonSerialized] bool built;   // false again after a script reload, so the lists get rebuilt

    void Awake() => follow = GetComponent<CameraController>();

    void Start() => Build();

    void Build()
    {
        built = true;
        if (follow == null) follow = GetComponent<CameraController>();
        set = Resources.Load<SeeThroughMaterials>("SeeThroughMaterials");
        FindFrontWalls();
        watched.Clear();
        foreach (CombatUnit u in FindObjectsByType<CombatUnit>(FindObjectsSortMode.None))
            if (u.team == Team.Party) watched.Add(u.transform);
    }

    // Every wall object once: its size from its colliders, its look from its renderers (torches on it too)
    void FindFrontWalls()
    {
        frontWalls.Clear();
        var seen = new HashSet<GameObject>();
        foreach (Collider c in FindObjectsByType<Collider>(FindObjectsSortMode.None))
        {
            if (c.isTrigger || (wallLayers.value & (1 << c.gameObject.layer)) == 0) continue;
            GameObject go = c.gameObject;
            if (!seen.Add(go)) continue;

            Bounds b = c.bounds;
            foreach (Collider other in go.GetComponents<Collider>()) if (!other.isTrigger) b.Encapsulate(other.bounds);
            if (b.size.y < minWallHeight) continue;                  // crates, chairs, tables, books
            if (b.size.x < b.size.z * minWidthToDepth) continue;     // side walls and pillars

            var w = new Wall { bounds = b };
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
                w.parts.Add(new Part { renderer = r, solid = r.sharedMaterials, faded = SeeThroughFor(r.sharedMaterials), shadows = r.shadowCastingMode });
            if (w.parts.Count > 0) frontWalls.Add(w);
        }
    }

    Material[] SeeThroughFor(Material[] solid)
    {
        if (set == null) return null;
        var result = new Material[solid.Length];
        for (int i = 0; i < solid.Length; i++)
        {
            Material asset = solid[i] != null ? set.Find(solid[i]) : null;
            if (asset == null) return null;   // no copy for this one: hide the part instead
            if (!liveCopies.TryGetValue(asset, out Material copy))
            {
                copy = new Material(asset);    // a copy, so changing opacity never edits the asset
                liveCopies[asset] = copy;
            }
            result[i] = copy;
        }
        return result;
    }

    void LateUpdate()
    {
        if (!built) Build();
        if (!Mathf.Approximately(appliedOpacity, wallOpacity)) ApplyOpacity();

        Transform focus = follow != null ? follow.target : null;
        foreach (Wall w in frontWalls)
        {
            bool fade = false;
            foreach (Transform t in watched)
                if (t != null && t.gameObject.activeInHierarchy && IsPast(t.position, w.bounds)) { fade = true; break; }
            if (!fade && focus != null && !watched.Contains(focus) && IsPast(focus.position, w.bounds)) fade = true;
            if (fade != w.faded) SetFaded(w, fade);
        }
    }

    bool IsPast(Vector3 p, Bounds b)
    {
        float behind = p.z - b.max.z;   // how far into the screen the character is beyond the wall
        if (behind <= 0f || behind > viewDepth) return false;
        return p.x > b.min.x - viewWidth && p.x < b.max.x + viewWidth;
    }

    void SetFaded(Wall w, bool fade)
    {
        w.faded = fade;
        foreach (Part p in w.parts)
        {
            if (p.renderer == null) continue;
            if (!fade)
            {
                p.renderer.sharedMaterials = p.solid;
                p.renderer.shadowCastingMode = p.shadows;
            }
            else if (p.faded != null && wallOpacity > 0.001f)
            {
                p.renderer.sharedMaterials = p.faded;                      // see-through
                p.renderer.shadowCastingMode = p.shadows;
            }
            else
                p.renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;   // just hidden, shadow stays
        }
    }

    void ApplyOpacity()
    {
        appliedOpacity = wallOpacity;
        foreach (Material m in liveCopies.Values)
        {
            Color c = m.GetColor("_BaseColor");
            c.a = wallOpacity;
            m.SetColor("_BaseColor", c);
        }
        foreach (Wall w in frontWalls) if (w.faded) SetFaded(w, true);   // 0 opacity switches to 'hidden'
    }

    void OnDisable()
    {
        foreach (Wall w in frontWalls) if (w.faded) SetFaded(w, false);
    }

    void OnDestroy()
    {
        foreach (Material m in liveCopies.Values) if (m != null) Destroy(m);
        liveCopies.Clear();
    }
}
