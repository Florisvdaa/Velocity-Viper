// Snake.cs
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Transform))]
public class Snake : MonoBehaviour
{
    [Header("Refs")]
    public Grid2D grid;                 // assign Grid2D
    public Transform segmentPrefab;     // simple cube/capsule etc.
    public FoodSpawner spawner;         // assign FoodSpawner

    [Header("Movement")]
    public float stepDelay = 0.12f;     // seconds per step
    public Vector2Int startGridPos = new Vector2Int(3, 3);
    public bool wrapAround = false;

    // State
    Vector2Int head;            // head grid pos
    Vector2Int dir = Vector2Int.right;
    Vector2Int nextDir;
    float timer;
    bool isAlive = true;

    // Body (tail): body[0] is the segment right behind the head
    readonly List<Vector2Int> body = new();
    readonly List<Transform> bodyObjs = new();

    void Start()
    {
        if (grid == null) { Debug.LogError("Snake: assign Grid2D"); enabled = false; return; }
        head = startGridPos;
        nextDir = dir;
        transform.position = grid.GridToWorld(head);

        // Drop first food
        if (spawner != null) spawner.Respawn(OccupiedCells());
    }

    void Update()
    {
        if (!isAlive) return;

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
        Vector2Int input = Vector2Int.zero;
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) input = Vector2Int.up;
        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) input = Vector2Int.down;
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) input = Vector2Int.left;
        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) input = Vector2Int.right;

        if (input != Vector2Int.zero)
        {
            bool reverse = (input + dir) == Vector2Int.zero;
            if (!reverse) nextDir = input;
        }
    }

    void Step()
    {
        dir = nextDir;
        Vector2Int next = head + dir;

        // Wrap or walls
        if (wrapAround)
        {
            next.x = (next.x + grid.width) % grid.width;
            next.y = (next.y + grid.height) % grid.height;
        }
        else
        {
            if (!grid.InBounds(next)) { Die("Hit wall"); return; }
        }

        // Are we eating?
        bool ate = (spawner != null && spawner.HasFood && spawner.CurrentFood == next);

        // Self-collision check
        // Special case: if NOT eating, the last tail cell will vacate this tick,
        // so moving into that specific cell is okay.
        Vector2Int tailWillVacate = (!ate && body.Count > 0) ? body[^1] : new Vector2Int(int.MinValue, int.MinValue);
        for (int i = 0; i < body.Count; i++)
        {
            if (body[i] == next && body[i] != tailWillVacate)
            {
                Die("Hit self");
                return;
            }
        }

        // Move body:
        // previous head cell becomes the first tail cell (grow) or reused by last segment (no grow)
        Vector2Int prevHead = head;

        if (ate)
        {
            // GROW: add a new segment at prevHead
            body.Insert(0, prevHead);
            var segT = Instantiate(segmentPrefab, grid.GridToWorld(prevHead), Quaternion.identity);
            bodyObjs.Insert(0, segT);
        }
        else if (body.Count > 0)
        {
            // REUSE last segment: move it to prevHead and put it at the front
            int last = body.Count - 1;
            Vector2Int movedFrom = body[last];
            Transform segT = bodyObjs[last];

            body.RemoveAt(last);
            bodyObjs.RemoveAt(last);

            body.Insert(0, prevHead);
            bodyObjs.Insert(0, segT);

            segT.position = grid.GridToWorld(prevHead);
        }

        // Move head
        head = next;
        transform.position = grid.GridToWorld(head);

        // Ate? -> respawn food
        if (ate && spawner != null)
        {
            spawner.Respawn(OccupiedCells());
        }
    }

    void Die(string reason)
    {
        isAlive = false;
        Debug.Log($"Snake died: {reason}");
        // TODO: hook into your UI / restart logic.
    }

    // Helpers
    public HashSet<Vector2Int> OccupiedCells()
    {
        var occ = new HashSet<Vector2Int> { head };
        for (int i = 0; i < body.Count; i++) occ.Add(body[i]);
        return occ;
    }

    // Quick API if you need from other scripts
    public Vector2Int GetHead() => head;
    public IReadOnlyList<Vector2Int> GetBody() => body;
    public bool IsAlive() => isAlive;
}
