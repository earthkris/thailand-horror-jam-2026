using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Building blocks for the bits of UI that are made in code
// (dice banner, toasts, the [E] prompt, tutorial hints, the goddess, the skip button, the screen fade).
public static class UIBuilder
{
    // Every code-built screen is laid out for this size and scales with the real screen
    public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

    // Draw order (higher = on top). The pause menu's own canvas sits at 100; the goddess picks hers in the Inspector.
    public const int HintOrder = 40;    // toasts, the [E] prompt, tutorial hints: above the game UI
    public const int DiceOrder = 50;    // the dice banner
    public const int SkipOrder = 100;   // the cutscene SKIP button
    public const int FadeOrder = 200;   // the black screen fade: above everything

    // A new full-screen overlay canvas. clickable = it has buttons (or should eat clicks).
    public static Canvas NewOverlayCanvas(string name, Transform parent, int sortingOrder, bool clickable = false)
    {
        var go = new GameObject(name, typeof(RectTransform));
        if (parent != null) go.transform.SetParent(parent, false);
        Canvas canvas = MakeOverlayCanvas(go, sortingOrder);
        if (clickable) go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    // Turns an existing object into a full-screen overlay canvas
    public static Canvas MakeOverlayCanvas(GameObject go, int sortingOrder)
    {
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    public static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    // A white image (tint it with .color). It ignores clicks unless raycastTarget is on.
    public static Image NewImage(string name, Transform parent, Sprite sprite = null, bool raycastTarget = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = raycastTarget;
        return image;
    }

    // A text that never eats clicks. Place it with its rectTransform.
    public static TextMeshProUGUI NewText(string name, Transform parent, float size, Color color,
                                          TextAlignmentOptions alignment, bool wrap = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    // A dark edge, so floating text reads over anything
    public static void AddOutline(TMP_Text text)
    {
        text.outlineWidth = 0.25f;
        text.outlineColor = Palette.Ink;
    }

    // Fill the whole parent
    // Run 'onEnter' when the mouse goes over 'target' and 'onExit' when it leaves (button hover colours)
    public static void AddHover(GameObject target, System.Action onEnter, System.Action onExit)
    {
        var trigger = target.GetComponent<UnityEngine.EventSystems.EventTrigger>();
        if (trigger == null) trigger = target.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        void On(UnityEngine.EventSystems.EventTriggerType type, System.Action act)
        {
            var e = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = type };
            e.callback.AddListener(_ => act());
            trigger.triggers.Add(e);
        }
        On(UnityEngine.EventSystems.EventTriggerType.PointerEnter, onEnter);
        On(UnityEngine.EventSystems.EventTriggerType.PointerExit, onExit);
    }

    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }
}
