using UnityEngine;

// One square of the battle grid.
// Its visual is a flat glow on the floor that is hidden until something highlights it.
public class Tile
{
    public int x;
    public int y;
    public bool walkable = true;
    public GameObject visual;

    readonly Renderer rend;
    Material tint;   // our own copy of the glow material, made the first time this tile lights up

    public Tile(int x, int y, GameObject visual)
    {
        this.x = x;
        this.y = y;
        this.visual = visual;
        rend = visual.GetComponent<Renderer>();
        Hide();
    }

    // Light up this tile. strength 0-1 = how bright the glow is.
    public void Show(Color color, float strength)
    {
        if (tint == null) tint = rend.material;
        color.a = strength;
        tint.color = color;
        rend.enabled = true;
    }

    public void Hide()
    {
        rend.enabled = false;
    }

    // Removes the glow and its material copy (a copy isn't cleaned up with the object by itself)
    public void Destroy()
    {
        if (tint != null) Object.Destroy(tint);
        if (visual != null) Object.Destroy(visual);
    }

    // ---------- The glow itself (also used for the exploring click target) ----------

    static Material glowMaterial;

    // Assets/Preuk/Resources/TileGlow.mat (transparent, additive = looks like light). Null if it's missing.
    public static Material GlowMaterial =>
        glowMaterial != null ? glowMaterial : (glowMaterial = Resources.Load<Material>("TileGlow"));

    // A flat square of glow lying on the floor, 'size' across (no collider, no shadows)
    public static GameObject CreateGlow(string name, float size)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // lie flat, facing up
        go.transform.localScale = Vector3.one * size;

        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = GlowMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go;
    }
}
