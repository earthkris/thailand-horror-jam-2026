using UnityEngine;
using UnityEngine.EventSystems;

// Tells the menu when the mouse is over one of its options (to show that option's description).
// ChoiceMenu adds this by itself; you don't need to add it anywhere.
public class MenuHover : MonoBehaviour, IPointerEnterHandler
{
    public System.Action onEnter;

    public void OnPointerEnter(PointerEventData eventData) => onEnter?.Invoke();
}
