using UnityEngine;

// Pairs each wall material with a see-through copy, for WallFader.
// Made by Claude's setup into Assets/Preuk/Resources/SeeThroughMaterials.asset; WallFader loads it by itself.
// The copies are real assets so the transparent shader is kept in builds.
public class SeeThroughMaterials : ScriptableObject
{
    public Material[] originals = new Material[0];
    public Material[] seeThrough = new Material[0];

    public Material Find(Material original)
    {
        for (int i = 0; i < originals.Length && i < seeThrough.Length; i++)
            if (originals[i] == original) return seeThrough[i];
        return null;
    }
}
