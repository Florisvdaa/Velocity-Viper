using System.Collections.Generic;
using UnityEngine;

public class FoodSpawner : MonoBehaviour
{
    public Grid2D grid;              // assign Grid2D
    public Transform foodPrefab;     // small cube/sphere
    public bool HasFood { get; private set; }
    public Vector2Int CurrentFood { get; private set; }

    Transform currentFoodObj;

    public void Respawn(HashSet<Vector2Int> blocked)
    {
        if (grid == null || foodPrefab == null)
        {
            Debug.LogError("FoodSpawner: assign Grid2D and foodPrefab");
            return;
        }

        // Build list of all free cells
        List<Vector2Int> free = new();
        for (int x = 0; x < grid.GetWidth(); x++)
        {
            for (int y = 0; y < grid.GetHeight(); y++)
            {
                var p = new Vector2Int(x, y);
                if (blocked == null || !blocked.Contains(p))
                    free.Add(p);
            }
        }

        if (free.Count == 0)
        {
            Debug.Log("No free cells left: you win?");
            HasFood = false;
            if (currentFoodObj != null) Destroy(currentFoodObj.gameObject);
            return;
        }

        // Pick random
        var choice = free[Random.Range(0, free.Count)];
        CurrentFood = choice;
        HasFood = true;

        // Spawn or move existing
        var world = grid.GridToWorld(choice);
        if (currentFoodObj == null)
            currentFoodObj = Instantiate(foodPrefab, world, Quaternion.identity);
        else
            currentFoodObj.position = world;
    }
}
