using UnityEngine;

[RequireComponent(typeof(Transform))]
public class GridMover : MonoBehaviour
{
    public Grid2D grid;               // assign in Inspector
    [Header("Movement")]
    public float stepDelay = 0.12f;   // seconds per grid step
    public Vector2Int startGridPos = new Vector2Int(3, 3);
    public bool wrapAround = false;   // if true, exits one side and enters the other

    Vector2Int gridPos;
    Vector2Int dir = Vector2Int.right;       // default start direction
    Vector2Int nextDir;                      // buffered input (applies on next step)
    float timer;

    void Start()
    {
        if (grid == null)
        {
            Debug.LogError("GridMover: please assign a Grid2D.");
            enabled = false;
            return;
        }

        gridPos = startGridPos;
        nextDir = dir;
        SnapToWorld();
    }

    void Update()
    {
        ReadInput();

        timer += Time.deltaTime;
        if (timer >= stepDelay)
        {
            timer -= stepDelay;
            Step();
        }
    }

    void ReadInput()
    {
        // Raw, no smoothing
        Vector2Int input = Vector2Int.zero;

        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) input = Vector2Int.up;
        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) input = Vector2Int.down;
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) input = Vector2Int.left;
        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) input = Vector2Int.right;

        if (input != Vector2Int.zero)
        {
            // disallow instant reverse (snake rule). Reverse = same axis, opposite sign.
            bool reverse = (input + dir) == Vector2Int.zero;
            if (!reverse) nextDir = input;
        }
    }

    void Step()
    {
        // apply buffered direction at step time (feels crisp)
        dir = nextDir;

        Vector2Int next = gridPos + dir;

        if (wrapAround)
        {
            // torus wrap
            next.x = (next.x + grid.GetWidth()) % grid.GetWidth();
            next.y = (next.y + grid.GetHeight()) % grid.GetHeight();
        }
        else
        {
            // stop at walls
            if (!grid.InBounds(next))
                return; // hit a wall; you can trigger death or end here
        }

        gridPos = next;
        SnapToWorld();
    }

    void SnapToWorld()
    {
        transform.position = grid.GridToWorld(gridPos);
    }

    // Handy if you need current grid position outside
    public Vector2Int GetGridPos() => gridPos;
    public Vector2Int GetDir() => dir;
}
