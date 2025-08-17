// Grid2D.cs
using UnityEngine;

[ExecuteAlways]
public class Grid2D : MonoBehaviour
{
    [Header("Layout")]
    public Vector3 origin = Vector3.zero;   // world position of bottom-left cell (x,z)
    public int width = 20;                  // cells in X
    public int height = 12;                 // cells in Z
    public float cellSize = 1f;             // world units per cell

    public Vector3 GridToWorld(Vector2Int gridPos)
    {
        float x = origin.x + (gridPos.x + 0.5f) * cellSize;
        float z = origin.z + (gridPos.y + 0.5f) * cellSize;
        return new Vector3(x, transform.position.y, z);
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
}
