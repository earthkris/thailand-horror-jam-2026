using UnityEngine;

// Switches this object off as soon as the game starts, so it only exists while you build the scene.
// Used on the Directional Light: the editor stays lit, the game is dark (torches and carried lights only).
// BossVictory makes its own sun for the daylight ending.
public class EditorOnly : MonoBehaviour
{
    void Awake()
    {
        gameObject.SetActive(false);
    }
}
