using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Something the player can use while exploring: a spell book now, a chest or a door later.
// Stand on it or next to it and press E, or click it. Click it from further away and the
// player walks over and uses it on arrival. A small [E] prompt floats above the nearest one in reach.
// To make a new kind: inherit from Interactable and write Interact() (SpellBook is an example).
public abstract class Interactable : MonoBehaviour
{
    [Header("Interact")]
    [Tooltip("How close the player must stand, in tiles. 1.5 = this tile and the 8 around it.")]
    public float reach = 1.5f;
    [Tooltip("Text after the [E], e.g. 'Pick up' or 'Open'.")]
    public string promptText = "Pick up";
    [Tooltip("How high above this object the prompt floats, in tiles.")]
    public float promptHeight = 1.3f;

    protected PlayerMovement player;
    bool pending;          // clicked from far away: use it once we arrive
    Renderer[] renderers;  // what you can click on

    static readonly List<Interactable> all = new List<Interactable>();
    static Interactable clicked;
    static int clickedFrame = -1;
    static Interactable nearestCache;
    static int nearestFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        all.Clear();
        clicked = null; clickedFrame = -1;
        nearestCache = null; nearestFrame = -1;
        shownFor = null;
    }

    // What happens when the player uses it
    protected abstract void Interact();

    // The words after [E]. Override to show more (SpellBook adds the spell's name).
    protected virtual string Prompt => promptText;

    protected virtual void OnEnable()
    {
        if (!all.Contains(this)) all.Add(this);
    }

    protected virtual void OnDisable()
    {
        all.Remove(this);
        if (shownFor == this) HidePrompt();
    }

    protected virtual void Start()
    {
        player = FindAnyObjectByType<PlayerMovement>();
        renderers = GetComponentsInChildren<Renderer>();
    }

    protected virtual void Update()
    {
        if (!CanAct)
        {
            pending = false;
            if (shownFor == this) HidePrompt();
            return;
        }

        // A click on something else cancels 'walk over and use it'
        bool clickNow = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        if (clickNow && !(clicked == this && clickedFrame == Time.frameCount)) pending = false;

        bool inReach = InReach();

        // Clicked earlier: use it once we've arrived
        if (pending)
        {
            if (inReach && !player.IsStepping)
            {
                player.StopWalking();
                Use();
                return;
            }
            if (!player.IsWalking && !inReach) pending = false;   // stopped short (keys, blocked): give up
        }

        // The prompt and the E key belong to the nearest one in reach
        if (Nearest() == this)
        {
            ShowPrompt(this);
            var kb = Keyboard.current;
            if (kb != null && kb.eKey.wasPressedThisFrame) Use();
        }
        else if (shownFor == this) HidePrompt();
    }

    protected virtual void LateUpdate()
    {
        if (shownFor == this) PlacePrompt();   // after the camera has moved
    }

    void Use()
    {
        pending = false;
        if (shownFor == this) HidePrompt();
        Interact();
    }

    bool CanAct => player != null && player.isActiveAndEnabled && GameState.CanMove && !PauseMenu.BlocksInput;

    // Is the player close enough? (ignores height)
    public bool InReach() => player != null && FlatDistance(player.transform.position) <= reach * player.tileSize + 0.01f;

    float FlatDistance(Vector3 p)
    {
        Vector3 me = transform.position;
        return new Vector2(p.x - me.x, p.z - me.z).magnitude;
    }

    static Interactable Nearest()
    {
        if (nearestFrame == Time.frameCount) return nearestCache;
        nearestFrame = Time.frameCount;
        nearestCache = null;
        float best = float.MaxValue;
        foreach (Interactable i in all)
        {
            if (i == null || !i.isActiveAndEnabled || !i.InReach()) continue;
            float d = i.FlatDistance(i.player.transform.position);
            if (d < best) { best = d; nearestCache = i; }
        }
        return nearestCache;
    }

    // ---------- Clicking ----------

    // PlayerMovement asks this first on every click. True = the click was on an interactable, which took it.
    public static bool TryHandleClick(Ray ray, Vector3 floorTile)
    {
        Interactable hit = null;
        float best = float.MaxValue;
        foreach (Interactable i in all)
        {
            if (i == null || !i.isActiveAndEnabled || i.player == null) continue;
            if (!i.UnderRay(ray, out float d) && !i.OnTile(floorTile, ray, out d)) continue;
            if (d < best) { best = d; hit = i; }
        }
        if (hit == null) return false;

        clicked = hit;
        clickedFrame = Time.frameCount;
        if (hit.InReach() && !hit.player.IsStepping) hit.Use();
        else
        {
            hit.pending = true;
            hit.WalkOver();
        }
        return true;
    }

    // Did the click land on the sprite? (a little bigger than the sprite, so it's easy to hit)
    bool UnderRay(Ray ray, out float distance)
    {
        distance = float.MaxValue;
        if (renderers == null) return false;
        bool any = false;
        foreach (Renderer r in renderers)
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            Bounds b = r.bounds;
            b.Expand(0.2f * player.tileSize);
            if (b.IntersectRay(ray, out float d) && d < distance) { distance = d; any = true; }
        }
        return any;
    }

    // Or on the floor tile it sits on?
    bool OnTile(Vector3 floorTile, Ray ray, out float distance)
    {
        distance = float.MaxValue;
        float half = 0.5f * player.tileSize;
        Vector3 me = transform.position;
        if (Mathf.Abs(floorTile.x - me.x) >= half || Mathf.Abs(floorTile.z - me.z) >= half) return false;
        distance = Vector3.Distance(ray.origin, floorTile);
        return true;
    }

    void WalkOver()
    {
        float ts = player.tileSize;
        Vector3 here = player.CurrentTile, me = transform.position;
        if (FlatDistance(here) <= reach * ts + 0.01f) { player.StopWalking(); return; }   // this step gets us there
        if (player.WalkTo(me)) return;

        // Our own tile can't be walked on (a chest, say): go next to it, nearest side first
        Vector3[] sides = { me + Vector3.forward * ts, me + Vector3.back * ts, me + Vector3.left * ts, me + Vector3.right * ts };
        System.Array.Sort(sides, (p, q) => (p - here).sqrMagnitude.CompareTo((q - here).sqrMagnitude));
        foreach (Vector3 s in sides)
            if (player.WalkTo(s)) return;
    }

    // ---------- The floating [E] prompt (one for the whole game, built the first time) ----------

    static Interactable shownFor;
    static RectTransform promptRect;
    static TextMeshProUGUI promptLabel;

    static void ShowPrompt(Interactable i)
    {
        if (promptLabel == null) BuildPrompt();
        shownFor = i;
        promptLabel.text = "<color=#E8BE63>[E]</color>  " + i.Prompt;
        if (!promptLabel.gameObject.activeSelf) promptLabel.gameObject.SetActive(true);
    }

    static void HidePrompt()
    {
        shownFor = null;
        if (promptLabel != null) promptLabel.gameObject.SetActive(false);
    }

    void PlacePrompt()
    {
        Camera cam = Camera.main;
        if (cam == null || promptRect == null) return;
        float ts = player != null ? player.tileSize : 1f;
        Vector3 screen = cam.WorldToScreenPoint(transform.position + Vector3.up * promptHeight * ts);
        promptLabel.enabled = screen.z > 0f;                       // behind the camera: hide
        promptRect.position = new Vector3(screen.x, screen.y, 0f); // overlay canvas: screen pixels
    }

    static void BuildPrompt()
    {
        var canvasGo = new GameObject("Interact Prompt", typeof(Canvas), typeof(CanvasScaler));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(canvasGo.transform, false);
        promptRect = (RectTransform)go.transform;
        promptRect.sizeDelta = new Vector2(500, 50);
        promptRect.pivot = new Vector2(0.5f, 0f);   // sits just above the point

        promptLabel = go.GetComponent<TextMeshProUGUI>();
        promptLabel.fontSize = 26;
        promptLabel.color = new Color32(217, 203, 176, 255);   // bone
        promptLabel.alignment = TextAlignmentOptions.Bottom;
        promptLabel.textWrappingMode = TextWrappingModes.NoWrap;
        promptLabel.raycastTarget = false;
        promptLabel.outlineWidth = 0.25f;                       // dark edge so it reads over anything
        promptLabel.outlineColor = new Color32(5, 4, 7, 255);
        go.SetActive(false);
    }
}
