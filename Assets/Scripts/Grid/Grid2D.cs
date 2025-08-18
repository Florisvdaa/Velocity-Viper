using UnityEngine;

[ExecuteAlways]
public class Grid2D : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] private Vector3 origin = Vector3.zero;   // world position of bottom-left cell (x,z)
    [SerializeField] private int width = 20;                  // cells in X
    [SerializeField] private int height = 12;                 // cells in Z
    [SerializeField] private float cellSize = 1f;             // world units per cell

    [Header("Tiles (optional)")]
    public GameObject tilePrefab;           // leave null to auto-create Quads
    public Material tileMaterial;           // optional: applied to each tile renderer
    [Range(0.1f, 1.0f)] public float tileFill = 0.95f;  // <1 leaves thin grid lines
    public string tilesRootName = "Tiles";

    [Header("Build")]
    public bool buildTilesOnPlay = true;
    public bool rebuildOnParamChangeInPlay = false; 

    public Vector3 GridToWorld(Vector2Int gridPos)
    {
        float x = origin.x + (gridPos.x + 0.5f) * cellSize;
        float z = origin.z + (gridPos.y + 0.5f) * cellSize;
        return new Vector3(x, transform.position.y + .5f, z);
    }

    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        int gx = Mathf.FloorToInt((worldPos.x - origin.x) / cellSize);
        int gy = Mathf.FloorToInt((worldPos.z - origin.z) / cellSize);
        return new Vector2Int(gx, gy);
    }

    public bool InBounds(Vector2Int gridPos)
    {
        return gridPos.x >= 0 && gridPos.x < width && gridPos.y >= 0 && gridPos.y < height;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1, 1, 1, 0.25f);
        Vector3 bl = new Vector3(origin.x, transform.position.y, origin.z);
        Vector3 br = bl + new Vector3(width * cellSize, 0f, 0f);
        Vector3 tl = bl + new Vector3(0f, 0f, height * cellSize);
        Vector3 tr = bl + new Vector3(width * cellSize, 0f, height * cellSize);

        // border
        Gizmos.DrawLine(bl, br);
        Gizmos.DrawLine(br, tr);
        Gizmos.DrawLine(tr, tl);
        Gizmos.DrawLine(tl, bl);

        // verticals
        for (int x = 1; x < width; x++)
        {
            float wx = origin.x + x * cellSize;
            Gizmos.DrawLine(new Vector3(wx, transform.position.y, origin.z),
                            new Vector3(wx, transform.position.y, origin.z + height * cellSize));
        }
        // horizontals
        for (int y = 1; y < height; y++)
        {
            float wz = origin.z + y * cellSize;
            Gizmos.DrawLine(new Vector3(origin.x, transform.position.y, wz),
                            new Vector3(origin.x + width * cellSize, transform.position.y, wz));
        }
    }

    // ---------- Build hooks ----------
    void OnEnable()
    {
        // In editor (not playing), OnEnable runs a lot. Only auto-build in Play mode unless you want editor spam.
        if (Application.isPlaying && buildTilesOnPlay)
            RebuildTiles();
    }

    void Start()
    {
        // Some people disable/enable at runtime; Start ensures at least one build
        if (Application.isPlaying && buildTilesOnPlay)
            RebuildTiles();
    }

    [ContextMenu("Rebuild Tiles")]
    public void RebuildTiles()
    {
        // Grid
        Transform root = GetOrCreateTilesRoot();
        ClearChildren(root);

        // plane size: we’ll use a Quad (1x1 in local X/Y, faces +Z), rotate to lie on XZ
        // Unity Plane is 10x10; using Quad is simpler: scale by (cellSize * tileFill)
        Vector3 tileScale = new Vector3(cellSize * tileFill, 1f, cellSize * tileFill);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var cell = new Vector2Int(x, y);
                Vector3 pos = GridToWorld(cell);

                GameObject tileGO;
                if (tilePrefab != null)
                {
                    tileGO = (GameObject)Instantiate(tilePrefab, pos, Quaternion.identity, root);
                }
                else
                {
                    // Auto-create a Quad primitive (removes collider)
#if UNITY_EDITOR
                    tileGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    var col = tileGO.GetComponent<Collider>();
                    if (col != null)
                        DestroyImmediate(col);
                    tileGO.transform.SetParent(root, true);
                    tileGO.transform.position = pos;
#else
                    tileGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    var col = tileGO.GetComponent<Collider>();
                    if (col != null) Destroy(col);
                    tileGO.transform.SetParent(root, true);
                    tileGO.transform.position = pos;
#endif
                }

                tileGO.name = $"Tile_{x}_{y}";

                // Rotate Quad so it lies flat on XZ and faces upward (+Y)
                // A Quad faces +Z by default, so rotate +90° about X
                tileGO.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

                // Scale to cell size (leaving a small border if tileFill < 1)
                tileGO.transform.localScale = tileScale;

                // Optional material
                if (tileMaterial != null)
                {
                    var r = tileGO.GetComponent<MeshRenderer>();
                    if (r != null) r.sharedMaterial = tileMaterial;
                }
            }
        }
    }

    Transform GetOrCreateTilesRoot()
    {
        // Reuse existing container if present
        var existing = transform.Find(tilesRootName);
        if (existing != null) return existing;

        var go = new GameObject(tilesRootName);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go.transform;
    }

    void ClearChildren(Transform root)
    {
        // Delete all existing tiles
#if UNITY_EDITOR
        // In edit mode: immediate destroy to avoid “leaked” objects
        while (root.childCount > 0)
        {
            var c = root.GetChild(0);
            DestroyImmediate(c.gameObject);
        }
#else
        // In play mode: normal destroy
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Destroy(root.GetChild(i).gameObject);
        }
#endif
    }

    // Optional: auto-rebuild when params change in the editor
#if UNITY_EDITOR
    void OnValidate()
    {
        // Clamp basics
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);
        cellSize = Mathf.Max(0.001f, cellSize);

        // Rebuild only if a tiles root already exists (prevents spam creating objects on add)
        var root = transform.Find(tilesRootName);
        if (root != null)
        {
            RebuildTiles();
        }
    }
#endif
    public int GetHeight() => height;
    public int GetWidth() => width;
    public float GetCellSize () => cellSize;
    public Vector3 GetOrigin() => origin;
}
