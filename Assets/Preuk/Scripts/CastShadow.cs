using UnityEngine;
using UnityEngine.Rendering;

// SpriteRenderer hides its shadow options in the Inspector,
// so we switch them on from code when the game starts.
[RequireComponent(typeof(SpriteRenderer))]
public class CastShadow : MonoBehaviour
{
    void Awake()
    {
        var sr = GetComponent<SpriteRenderer>();
        sr.shadowCastingMode = ShadowCastingMode.On;
        sr.receiveShadows = false; // unlit sprites can't show shadows on themselves anyway
    }
}