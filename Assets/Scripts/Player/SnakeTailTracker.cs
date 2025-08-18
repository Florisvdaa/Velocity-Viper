using UnityEngine;

public class SnakeTailTracker : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Snake snake;     // The Snake script reference
    [SerializeField] private Transform target; // The Transform to set (e.g. an empty GameObject)

    [Header("Settings")]
    [Tooltip("0 = segment right behind head, 1 = second, etc.")]
    [SerializeField] private int segmentIndex = 2; // example: 2 = 3rd segment

    void Update()
    {
        if (snake == null || target == null) return;

        Transform seg = snake.GetBodyTransformAt(segmentIndex);
        if (seg != null)
            target.position = seg.position;
    }
}
