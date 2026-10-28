using System.Collections.Generic;
using UnityEngine;

// OPTIONAL. Marks the area of one room with a box.
// If a fight starts inside this box, the battle grid stays inside it
// (stops the grid leaking through an open doorway into the next room).
// Without a Room, the grid just fills all the connected floor around the fight.
[RequireComponent(typeof(BoxCollider))]
public class Room : MonoBehaviour
{
    static readonly List<Room> all = new List<Room>();

    BoxCollider box;

    public Bounds Bounds => Box.bounds;
    BoxCollider Box => box != null ? box : (box = GetComponent<BoxCollider>());

    // Runs when the component is added: make the box a trigger that nothing hits
    void Reset()
    {
        Box.isTrigger = true;
        gameObject.layer = 2;   // Ignore Raycast
    }

    void OnEnable() => all.Add(this);
    void OnDisable() => all.Remove(this);

    // Is this world position inside the room (ignoring height)?
    public bool Contains(Vector3 pos)
    {
        Bounds b = Bounds;
        return pos.x >= b.min.x && pos.x <= b.max.x && pos.z >= b.min.z && pos.z <= b.max.z;
    }

    // The room at this position, or null if it isn't inside any Room
    public static Room At(Vector3 pos)
    {
        foreach (Room r in all)
            if (r.Contains(pos)) return r;
        return null;
    }

    // Dark red box in the Scene view
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.76f, 0.23f, 0.23f, 0.8f);
        Bounds b = Bounds;
        Gizmos.DrawWireCube(b.center, b.size);
    }
}
