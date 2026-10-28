using UnityEngine;
using UnityEngine.InputSystem;

public class GameStateTester : MonoBehaviour
{
    // Update is called once per frame
    void Update()
    {
        var kb = Keyboard.current;
        if (kb.mKey.wasPressedThisFrame) Toggle(GameMode.Menu);
        if (kb.eKey.wasPressedThisFrame) Toggle(GameMode.Dialogue);
    }

    void Toggle(GameMode mode)
    {
        GameState.Current = (GameState.Current == mode) ? GameMode.Exploring : mode;
        Debug.Log("Game mode: " + GameState.Current);
    }
}
