using UnityEngine;

public class HoverProb : MonoBehaviour
{
    [SerializeField] Transform prob;
    Vector3 currentPos;
    [SerializeField] Vector3 position;

    [SerializeField] float speed;

    // Update is called once per frame
    void Update()
    {
        if(!prob) return;
        currentPos = prob.position;
        float newCycle = Mathf.Sin(Time.time / speed);
        Vector3 newPosition = position * newCycle;
        prob.position = newPosition + currentPos;
    }
}
