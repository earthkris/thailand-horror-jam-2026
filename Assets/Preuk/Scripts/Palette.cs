using UnityEngine;

// The game's colours, in one place. Used as the default for colour fields in the Inspector
// (a value already set in a scene or prefab keeps its own colour) and for UI built in code.
public static class Palette
{
    public static readonly Color32 Bone = new Color32(217, 203, 176, 255);         // text, the party
    public static readonly Color32 CandleGold = new Color32(232, 190, 99, 255);    // highlights, good news
    public static readonly Color32 Blood = new Color32(194, 59, 59, 255);          // enemies, bad news
    public static readonly Color32 Ink = new Color32(5, 4, 7, 255);                // dark edge round floating text
}
