using UnityEngine;

// Keeps a panel at its design width, but never wider than its parent minus a margin.
// At a big UI Size the screen is fewer UI units wide, so a panel that fits at 100% can run off the edges.
// Anchor the panel to the middle (left and right anchors both 0.5); its children can stretch inside it.
[RequireComponent(typeof(RectTransform))]
public class MaxWidth : MonoBehaviour
{
    [Tooltip("Width at 100% UI Size, in UI units.")]
    public float width = 1420f;
    [Tooltip("Space kept free on each side when the parent is narrower than Width.")]
    public float margin = 50f;

    void OnEnable() => Fit();
    void LateUpdate() => Fit();   // UI Size can change while the panel is open

    void Fit()
    {
        var rt = (RectTransform)transform;
        if (!(rt.parent is RectTransform parent)) return;
        float w = Mathf.Min(width, parent.rect.width - 2f * margin);
        if (!Mathf.Approximately(rt.sizeDelta.x, w)) rt.sizeDelta = new Vector2(w, rt.sizeDelta.y);
    }
}
