using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Transform))]
public class Snake : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Grid2D grid;
    [SerializeField] private Transform segmentPrefab;
    [SerializeField] private FoodSpawner spawner;

    [Header("Movement (smooth)")]
    [SerializeField] private float baseStepDelay = 0.14f;
    [SerializeField] private float minStepDelay = 0.06f;
    [Range(0.90f, 1.00f)]
    [SerializeField] private float delayFactorPerSegment = 0.98f;

    [Header("Start")]
    [Min(1)][SerializeField] private int startLength = 3; // head counts as 1
    [SerializeField] private Vector2Int startGridPos = new Vector2Int(3, 3);
    [SerializeField] private bool wrapAround = false;

    // Grid state
    private Vector2Int head;
    private Vector2Int dir = Vector2Int.right;
    private Vector2Int nextDir;
    private bool isAlive = true;

    // Tick
    private float timer;

    // Tail: body[0] = segment right behind head (grid cells)
    private readonly List<Vector2Int> body = new();

    // Visuals: fixed order (0 behind head … tail). We DO NOT rotate these on a normal move.
    private readonly List<Transform> bodyObjs = new();

    // Lerp buffers (world)
    private Vector3 headFrom, headTo;
    private readonly List<Vector3> segFrom = new();
    private readonly List<Vector3> segTo = new();

    void Start()
    {
        if (!grid) { Debug.LogError("Snake: assign Grid2D"); enabled = false; return; }

        head = startGridPos;
        nextDir = dir;

        BuildInitialBody(startLength);

        headTo = grid.GridToWorld(head);
        headFrom = headTo;

        segFrom.Clear(); segTo.Clear();
        for (int i = 0; i < body.Count; i++)
        {
            var p = grid.GridToWorld(body[i]);
            segFrom.Add(p);
            segTo.Add(p);
            bodyObjs[i].position = p;
        }

        transform.position = headTo;

        if (spawner != null) spawner.Respawn(OccupiedCells());
    }

    void Update()
    {
        if (!isAlive) return;

        ReadInput();

        float stepDelay = GetCurrentStepDelay();

        timer += Time.deltaTime;
        float t = Mathf.Clamp01(timer / stepDelay);
        RenderLerp(t);

        while (timer >= stepDelay && isAlive)
        {
            timer -= stepDelay;
            DoStepAndSetupLerps();
            // in case speed changed with growth
            stepDelay = GetCurrentStepDelay();
            RenderLerp(0f);
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

    float GetCurrentStepDelay()
    {
        int segs = body.Count;
        float d = baseStepDelay * Mathf.Pow(delayFactorPerSegment, segs);
        return Mathf.Max(minStepDelay, d);
    }

    void DoStepAndSetupLerps()
    {
        // 1) freeze "from"
        headFrom = headTo;
        for (int i = 0; i < segFrom.Count; i++)
            segFrom[i] = segTo[i];

        // 2) next head cell...
        dir = nextDir;
        Vector2Int next = head + dir;

        if (wrapAround)
        {
            next.x = (next.x + grid.GetWidth()) % grid.GetWidth();
            next.y = (next.y + grid.GetHeight()) % grid.GetHeight();
        }
        else if (!grid.InBounds(next)) { Die("Hit wall"); return; }

        bool ate = (spawner != null && spawner.HasFood && spawner.CurrentFood == next);

        // 3) self collision (tail end vacates if not eating)
        Vector2Int tailWillVacate = (!ate && body.Count > 0) ? body[^1] : new Vector2Int(int.MinValue, int.MinValue);
        for (int i = 0; i < body.Count; i++)
        {
            if (body[i] == next && body[i] != tailWillVacate)
            { Die("Hit self"); return; }
        }

        // 4) move grid state
        Vector2Int prevHead = head;

        if (ate)
        {
            // grow on the GRID at the head (classic snake)
            body.Insert(0, prevHead);

            // --- NEW: spawn the VISUAL at the TAIL end ---
            int oldSegCount = segFrom.Count;
            Vector3 tailStart = (oldSegCount > 0) ? segFrom[oldSegCount - 1] : headFrom; // old tail's start this tick

            var segT = Instantiate(segmentPrefab, tailStart, Quaternion.identity, transform);
            bodyObjs.Add(segT);           // append at the end
            segFrom.Add(tailStart);       // doesn't move this tick
            segTo.Add(tailStart);
        }
        else if (body.Count > 0)
        {
            // shift grid cells (no visual rotation)
            body.RemoveAt(body.Count - 1);
            body.Insert(0, prevHead);
        }

        head = next;

        // 5) targets
        headTo = grid.GridToWorld(head);

        // make sure buffers match counts (safety, usually already correct)
        while (segFrom.Count < body.Count) { segFrom.Add(headFrom); segTo.Add(headFrom); }
        while (segFrom.Count > body.Count) { segFrom.RemoveAt(segFrom.Count - 1); segTo.RemoveAt(segTo.Count - 1); }

        if (body.Count > 0)
        {
            segTo[0] = headFrom;                 // neck goes to where head started
            for (int i = 1; i < body.Count; i++)
                segTo[i] = segFrom[i - 1];       // each seg follows predecessor's start
        }

        // 6) respawn food
        if (ate && spawner != null)
            spawner.Respawn(OccupiedCells());
    }


    void RenderLerp(float t)
    {
        transform.position = LerpAcrossWrap(headFrom, headTo, t);
        for (int i = 0; i < bodyObjs.Count; i++)
            bodyObjs[i].position = LerpAcrossWrap(segFrom[i], segTo[i], t);
    }

    void BuildInitialBody(int length)
    {
        int tailSegments = Mathf.Max(0, length - 1);

        body.Clear();
        foreach (var t in bodyObjs) if (t) Destroy(t.gameObject);
        bodyObjs.Clear();

        head = startGridPos;

        // lay tail behind the head along -dir
        List<Vector2Int> tmp = new();
        for (int i = 0; i < tailSegments; i++)
        {
            Vector2Int seg = head - dir * (i + 1);
            if (!wrapAround && !grid.InBounds(seg)) break;
            if (wrapAround)
            {
                seg.x = (seg.x + grid.GetWidth()) % grid.GetWidth();
                seg.y = (seg.y + grid.GetHeight()) % grid.GetHeight();
            }
            tmp.Add(seg);
        }
        tmp.Reverse(); // body[0] sits right behind the head
        body.AddRange(tmp);

        for (int i = 0; i < body.Count; i++)
        {
            var segT = Instantiate(segmentPrefab, grid.GridToWorld(body[i]), Quaternion.identity, transform);
            bodyObjs.Add(segT);
        }
    }
    Vector3 LerpAcrossWrap(Vector3 from, Vector3 to, float t)
    {
        float spanX = grid.GetWidth() * grid.GetCellSize();
        float spanZ = grid.GetHeight() * grid.GetCellSize();

        // Adjust 'to' so we interpolate across the short arc
        Vector3 toAdj = to;

        float dx = to.x - from.x;
        if (Mathf.Abs(dx) > spanX * 0.5f)
            toAdj.x += (dx > 0f) ? -spanX : spanX;

        float dz = to.z - from.z;
        if (Mathf.Abs(dz) > spanZ * 0.5f)
            toAdj.z += (dz > 0f) ? -spanZ : spanZ;

        // Interpolate toward adjusted target
        Vector3 p = Vector3.Lerp(from, toAdj, t);

        // Keep the in-flight point within the board bounds (looks nicer near edges)
        float minX = grid.GetOrigin().x;
        float maxX = grid.GetOrigin().x + spanX;
        float minZ = grid.GetOrigin().z;
        float maxZ = grid.GetOrigin().z + spanZ;

        if (p.x < minX) p.x += spanX; else if (p.x > maxX) p.x -= spanX;
        if (p.z < minZ) p.z += spanZ; else if (p.z > maxZ) p.z -= spanZ;

        // At the end of the tick, snap to the canonical 'to' (prevents 1-frame offset)
        if (t >= 0.999f) return to;

        return p;
    }

    void Die(string reason)
    {
        isAlive = false;
        timer = 0f;
        Debug.Log($"Snake died: {reason}");
    }

    public HashSet<Vector2Int> OccupiedCells()
    {
        var occ = new HashSet<Vector2Int> { head };
        for (int i = 0; i < body.Count; i++) occ.Add(body[i]);
        return occ;
    }

    public Vector2Int GetHead() => head;
    public IReadOnlyList<Vector2Int> GetBody() => body;
    public bool IsAlive() => isAlive;
}
