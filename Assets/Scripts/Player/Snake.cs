using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Transform))]
public class Snake : MonoBehaviour
{ 
    [Header("Refs")]
    [SerializeField] private Grid2D grid;
    [SerializeField] private Transform segmentPrefab;
    [SerializeField] private Transform tailPrefab;
    [SerializeField] private FoodSpawner spawner;
    [SerializeField] private Transform headVisual;

    [Header("Movement (smooth)")]
    [SerializeField] private float baseStepDelay = 0.14f;
    [SerializeField] private float minStepDelay = 0.06f;
    [Range(0.90f, 1.00f)]
    [SerializeField] private float delayFactorPerSegment = 0.98f;

    [Header("Start")]
    [Min(1)][SerializeField] private int startLength = 3;         // head counts as 1
    [SerializeField] private Vector2Int startGridPos = new(3, 3);
    [SerializeField] private bool wrapAround = false;

    [Header("Death FX")]
    [Tooltip("Small burst for normal body pops.")]
    [SerializeField] private ParticleSystem explodeFxPrefab;
    [Tooltip("Optional bigger burst for the head. If null, we scale the small one.")]
    [SerializeField] private ParticleSystem headExplodeFxPrefab;
    [Tooltip("Base delay between each pop.")]
    [SerializeField] private float explodeInterval = 0.06f;
    [Tooltip("0.2 = ±20% random jitter around base delay.")]
    [Range(0f, 1f)][SerializeField] private float explodeIntervalJitter = 0.2f;
    [Tooltip("Scale multiplier for head burst when no dedicated head FX is set.")]
    [SerializeField] private float headExplodeScale = 1.8f;
    [Tooltip("Pause after the head burst before signaling game over etc.")]
    [SerializeField] private float headExplodeExtraDelay = 0.15f;
    [Tooltip("Destroy objects or just hide them.")]
    [SerializeField] private bool destroyPieces = true;

    // Grid state
    private Vector2Int head;
    private Vector2Int dir = Vector2Int.right;
    private Vector2Int nextDir;
    private bool isAlive = false;

    // Tick timer
    private float timer;

    // Body data: body[0] is the grid cell right behind the head
    private readonly List<Vector2Int> body = new();

    // Body visuals (same order as body)
    private readonly List<Transform> bodyObjs = new();

    // Lerp buffers (world space)
    private Vector3 headFrom, headTo;
    private readonly List<Vector3> segFrom = new();
    private readonly List<Vector3> segTo = new();

    // Unity

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
            bodyObjs[i].position = p; // set initial positions
        }

        transform.position = headTo;

        if (spawner != null) spawner.Respawn(OccupiedCells());

        UpdateHeadRotation(); // head only; body has no rotation
        InitTailRotationOnce();
    }

    public void StartMovement() => isAlive = true;

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

            // speed may change with length
            stepDelay = GetCurrentStepDelay();
            RenderLerp(0f);
        }
    }

    // Input / Movement

    private void ReadInput()
    {
        Vector2Int input = Vector2Int.zero;
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) input = Vector2Int.up;
        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) input = Vector2Int.down;
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) input = Vector2Int.left;
        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) input = Vector2Int.right;

        if (input != Vector2Int.zero)
        {
            // ignore reversing into yourself
            bool reverse = (input + dir) == Vector2Int.zero;
            if (!reverse) nextDir = input;
        }
    }

    private float GetCurrentStepDelay()
    {
        int segs = body.Count;
        float d = baseStepDelay * Mathf.Pow(delayFactorPerSegment, segs);
        return Mathf.Max(minStepDelay, d);
    }

    private void DoStepAndSetupLerps()
    {
        // freeze current positions for lerp
        headFrom = headTo;
        for (int i = 0; i < segFrom.Count; i++)
            segFrom[i] = segTo[i];

        // compute next head cell
        dir = nextDir;
        UpdateHeadRotation();
        Vector2Int next = head + dir;

        // wrap/clip
        if (wrapAround)
        {
            next.x = (next.x + grid.GetWidth()) % grid.GetWidth();
            next.y = (next.y + grid.GetHeight()) % grid.GetHeight();
        }
        else if (!grid.InBounds(next)) { Die("Hit wall"); return; }

        bool ate = (spawner != null && spawner.HasFood && spawner.CurrentFood == next);

        // self collision (tail end vacates if not eating)
        Vector2Int tailWillVacate = (!ate && body.Count > 0) ? body[^1] : new(int.MinValue, int.MinValue);
        for (int i = 0; i < body.Count; i++)
        {
            if (body[i] == next && body[i] != tailWillVacate)
            { Die("Hit self"); return; }
        }

        // update grid state
        Vector2Int prevHead = head;

        if (ate)
        {
            // grow at head
            body.Insert(0, prevHead);

            // visuals: turn last tail into mid segment
            int oldSegCount = segFrom.Count;
            if (oldSegCount > 0) ReplaceBodyVisualAt(oldSegCount - 1, segmentPrefab);

            // spawn new tail visual where the old tail started (or head start if length==1)
            Vector3 tailStart = (oldSegCount > 0) ? segFrom[oldSegCount - 1] : headFrom;
            var tailObj = Instantiate(tailPrefab != null ? tailPrefab : segmentPrefab,
                                      tailStart, Quaternion.identity, transform);

            bodyObjs.Add(tailObj);
            segFrom.Add(tailStart);
            segTo.Add(tailStart);

            GameManager.Instance.AddScore();
        }
        else if (body.Count > 0)
        {
            // shift body forward
            body.RemoveAt(body.Count - 1);
            body.Insert(0, prevHead);
        }

        head = next;

        // set new lerp targets
        headTo = grid.GridToWorld(head);

        // keep buffers sized
        while (segFrom.Count < body.Count) { segFrom.Add(headFrom); segTo.Add(headFrom); }
        while (segFrom.Count > body.Count) { segFrom.RemoveAt(segFrom.Count - 1); segTo.RemoveAt(segTo.Count - 1); }

        if (body.Count > 0)
        {
            segTo[0] = headFrom;                 // neck goes to where head started
            for (int i = 1; i < body.Count; i++)
                segTo[i] = segFrom[i - 1];       // others follow predecessor's start
        }

        // respawn food
        if (ate && spawner != null) spawner.Respawn(OccupiedCells());
    }

    private void ReplaceBodyVisualAt(int index, Transform prefab)
    {
        if (!prefab || index < 0 || index >= bodyObjs.Count) return;
        var old = bodyObjs[index];
        if (!old) return;

        var newObj = Instantiate(prefab, old.position, old.rotation, transform);
        bodyObjs[index] = newObj;
        Destroy(old.gameObject);
    }

    private void UpdateHeadRotation()
    {
        // rotate HEAD only (no body rotation)
        if (!headVisual) return;
        Vector3 fwd = new(dir.x, 0f, dir.y);     // grid (x,y) -> world (x,z)
        if (fwd.sqrMagnitude > 0.0f)
            headVisual.rotation = Quaternion.LookRotation(fwd, Vector3.up);
    }
    // Sets the initial rotation of the tail once at spawn
    private void InitTailRotationOnce()
    {
        int tailIdx = bodyObjs.Count - 1;
        if (tailIdx < 0) return;

        // tail looks toward its predecessor (or the head if only 1 segment)
        Vector3 nextTarget = (tailIdx > 0) ? segFrom[tailIdx - 1] : headFrom;
        Vector3 fwd = ForwardAcrossWrap(segFrom[tailIdx], nextTarget);

        if (fwd.sqrMagnitude > 1e-6f)
            bodyObjs[tailIdx].rotation = Quaternion.LookRotation(fwd, Vector3.up);
    }


    private void RenderLerp(float t)
    {
        // move head
        transform.position = LerpAcrossWrap(headFrom, headTo, t);

        // move body (no rotation for mid segments)
        for (int i = 0; i < bodyObjs.Count; i++)
            bodyObjs[i].position = LerpAcrossWrap(segFrom[i], segTo[i], t);

        // rotate ONLY the tail (last segment) 
        int tailIdx = bodyObjs.Count - 1;
        if (tailIdx >= 0) // safety
        {
            Vector3 fwd = ForwardAcrossWrap(segFrom[tailIdx], segTo[tailIdx]);
            if (fwd.sqrMagnitude > 1e-6f) // avoid NaN rotation if no movement
            {
                bodyObjs[tailIdx].rotation = Quaternion.LookRotation(fwd, Vector3.up);
            }
        }
    }


    private void BuildInitialBody(int length)
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
        tmp.Reverse(); // body[0] sits right behind head
        body.AddRange(tmp);

        for (int i = 0; i < body.Count; i++)
        {
            var prefab = (i == body.Count - 1) ? tailPrefab : segmentPrefab;
            if (!prefab) prefab = segmentPrefab; // fallback
            var segT = Instantiate(prefab, grid.GridToWorld(body[i]), Quaternion.identity, transform);
            bodyObjs.Add(segT);
        }
    }

    // Wrap helper

    private Vector3 LerpAcrossWrap(Vector3 from, Vector3 to, float t)
    {
        float spanX = grid.GetWidth() * grid.GetCellSize();
        float spanZ = grid.GetHeight() * grid.GetCellSize();

        // choose the short path across wrap
        Vector3 toAdj = to;
        float dx = to.x - from.x;
        if (Mathf.Abs(dx) > spanX * 0.5f) toAdj.x += (dx > 0f) ? -spanX : spanX;
        float dz = to.z - from.z;
        if (Mathf.Abs(dz) > spanZ * 0.5f) toAdj.z += (dz > 0f) ? -spanZ : spanZ;

        // interpolate
        Vector3 p = Vector3.Lerp(from, toAdj, t);

        // keep point within board bounds during flight
        float minX = grid.GetOrigin().x, maxX = minX + spanX;
        float minZ = grid.GetOrigin().z, maxZ = minZ + spanZ;
        if (p.x < minX) p.x += spanX; else if (p.x > maxX) p.x -= spanX;
        if (p.z < minZ) p.z += spanZ; else if (p.z > maxZ) p.z -= spanZ;

        // snap at the end of the tick
        if (t >= 0.999f) return to;
        return p;
    }
    // Forward vector from -> to, adjusted for wrap-around edges
    private Vector3 ForwardAcrossWrap(Vector3 from, Vector3 to)
    {
        float spanX = grid.GetWidth() * grid.GetCellSize();
        float spanZ = grid.GetHeight() * grid.GetCellSize();

        Vector3 toAdj = to;

        float dx = to.x - from.x;
        if (Mathf.Abs(dx) > spanX * 0.5f)
            toAdj.x += (dx > 0f) ? -spanX : spanX;

        float dz = to.z - from.z;
        if (Mathf.Abs(dz) > spanZ * 0.5f)
            toAdj.z += (dz > 0f) ? -spanZ : spanZ;

        return toAdj - from;
    }


    // Death & Explosions

    private void Die(string reason)
    {
        FeedbackManager.Instance.CameraShake().PlayFeedbacks();

        isAlive = false;  // Update() stops driving movement
        timer = 0f;

        Debug.Log($"Snake died: {reason}");
        StartCoroutine(ExplodeRoutine());
    }

    private IEnumerator ExplodeRoutine()
    {
        // Build a randomized explosion order by copying and shuffling the transforms.
        var order = new List<Transform>(bodyObjs);
        Shuffle(order);

        // Pop each body segment in the randomized order.
        foreach (var seg in order)
        {
            if (seg == null) continue;

            // Find current index (lists shrink during the loop).
            int i = bodyObjs.IndexOf(seg);
            if (i >= 0)
            {
                SpawnFx(seg.position, big: false);

                if (destroyPieces) Destroy(seg.gameObject);
                else seg.gameObject.SetActive(false);

                // Keep data in sync.
                body.RemoveAt(i);
                bodyObjs.RemoveAt(i);
                if (i < segFrom.Count) { segFrom.RemoveAt(i); segTo.RemoveAt(i); }
            }

            yield return new WaitForSeconds(GetExplodeDelay());
        }

        // Big head pop at the end.
        SpawnFx(transform.position, big: true);
        if (headVisual) headVisual.gameObject.SetActive(false);

        if (headExplodeExtraDelay > 0f)
            yield return new WaitForSeconds(headExplodeExtraDelay);

        // Game over hook:
        // GameManager.Instance.OnSnakeDied();
    }

    // Fisher–Yates shuffle
    private static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private float GetExplodeDelay()
    {
        if (explodeIntervalJitter <= 0f) return Mathf.Max(0f, explodeInterval);
        float min = explodeInterval * (1f - explodeIntervalJitter);
        float max = explodeInterval * (1f + explodeIntervalJitter);
        return Mathf.Max(0f, Random.Range(min, max));
    }

    private void SpawnFx(Vector3 pos, bool big)
    {
        ParticleSystem prefab = big && headExplodeFxPrefab != null ? headExplodeFxPrefab : explodeFxPrefab;
        if (!prefab) return;

        var fx = Instantiate(prefab, pos, Quaternion.identity);

        // scale up the small burst for the head if needed
        if (big && headExplodeFxPrefab == null && headExplodeScale > 1f)
            fx.transform.localScale *= headExplodeScale;

        var main = fx.main;
        Destroy(fx.gameObject, main.duration + main.startLifetime.constantMax + 0.1f);
    }

    // Public helpers

    public HashSet<Vector2Int> OccupiedCells()
    {
        var occ = new HashSet<Vector2Int> { head };
        for (int i = 0; i < body.Count; i++) occ.Add(body[i]);
        return occ;
    }

    public Transform GetBodyTransformAt(int indexFromHead)
    {
        if (indexFromHead < 0 || indexFromHead >= bodyObjs.Count) return null;
        return bodyObjs[indexFromHead];
    }

    public void ChangeWrap(bool wrap) => wrapAround = wrap;

    public Vector2Int GetHead() => head;
    public IReadOnlyList<Vector2Int> GetBody() => body;
    public bool IsAlive() => isAlive;
}
