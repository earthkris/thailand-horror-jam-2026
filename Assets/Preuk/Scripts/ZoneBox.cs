using UnityEngine;

// A flat box on the floor that story scripts watch for the player walking into
// (BossIntro, PathBlocker, DialogueZone). size: x = width, y = depth (world Z). Height is ignored.
public static class ZoneBox
{
    static readonly Color GizmoColor = new Color(1f, 0.85f, 0.2f, 0.9f);   // yellow

    public static bool Contains(Vector3 centre, Vector2 size, Vector3 p) =>
        Mathf.Abs(p.x - centre.x) <= size.x * 0.5f + 0.01f && Mathf.Abs(p.z - centre.z) <= size.y * 0.5f + 0.01f;

    // The yellow outline in the Scene view (filled = a faint yellow floor too)
    public static void DrawGizmo(Vector3 centre, Vector2 size, bool filled = false)
    {
        Vector3 at = centre + Vector3.up * 0.1f;
        Vector3 box = new Vector3(size.x, 0.2f, size.y);
        if (filled)
        {
            Gizmos.color = new Color(GizmoColor.r, GizmoColor.g, GizmoColor.b, 0.25f);
            Gizmos.DrawCube(at, box);
        }
        Gizmos.color = GizmoColor;
        Gizmos.DrawWireCube(at, box);
    }
}
