using System;
using System.Collections.Generic;
using UnityEngine;

public class ProceduralMazeGenerator : MonoBehaviour
{
    [Header("Maze Size")]
    [SerializeField] private int columns = 8;
    [SerializeField] private int rows = 8;
    [SerializeField] private float cellSize = 2f;

    [Header("Wall Style")]
    [SerializeField] private float wallThickness = 0.5f;
    [SerializeField] private float wallHeight = 0.8f;
    [SerializeField] private Material wallMaterial;

    [Header("Seed")]
    [SerializeField] private bool useRandomSeed = true;
    [SerializeField] private int seed = 12345;
    [SerializeField] private int lastGeneratedSeed;

    [Header("Scene References")]
    [SerializeField] private Transform floor;
    [SerializeField] private Renderer floorRenderer;
    [SerializeField] private ArrowKeyMover player;

    private Transform generatedRoot;

    private static readonly Vector2Int[] Directions =
    {
        new Vector2Int(0, 1),
        new Vector2Int(1, 0),
        new Vector2Int(0, -1),
        new Vector2Int(-1, 0),
    };

    private void Awake()
    {
        Generate();
    }

    [ContextMenu("Generate Maze")]
    public void Generate()
    {
        columns = Mathf.Max(1, columns);
        rows = Mathf.Max(1, rows);
        cellSize = Mathf.Max(0.5f, cellSize);
        wallThickness = Mathf.Max(0.1f, wallThickness);
        wallHeight = Mathf.Max(0.1f, wallHeight);

        int activeSeed = useRandomSeed ? Environment.TickCount : seed;
        lastGeneratedSeed = activeSeed;
        System.Random random = new System.Random(activeSeed);

        EnsureGeneratedRoot();
        ClearGeneratedWalls();

        Material resolvedMaterial = wallMaterial != null
            ? wallMaterial
            : floorRenderer != null
                ? floorRenderer.sharedMaterial
                : null;

        bool[,] horizontalWalls = new bool[rows + 1, columns];
        bool[,] verticalWalls = new bool[rows, columns + 1];
        InitializeWalls(horizontalWalls, verticalWalls);
        CarveMaze(horizontalWalls, verticalWalls, random);

        ResizeFloor();
        BuildHorizontalRuns(horizontalWalls, resolvedMaterial);
        BuildVerticalRuns(verticalWalls, resolvedMaterial);
        PositionPlayerAtStart();
    }

    private void EnsureGeneratedRoot()
    {
        if (generatedRoot != null)
        {
            return;
        }

        Transform existing = transform.Find("GeneratedWalls");
        if (existing != null)
        {
            generatedRoot = existing;
            return;
        }

        GameObject root = new GameObject("GeneratedWalls");
        generatedRoot = root.transform;
        generatedRoot.SetParent(transform, false);
    }

    private void ClearGeneratedWalls()
    {
        if (generatedRoot == null)
        {
            return;
        }

        for (int index = generatedRoot.childCount - 1; index >= 0; index--)
        {
            Destroy(generatedRoot.GetChild(index).gameObject);
        }
    }

    private void InitializeWalls(bool[,] horizontalWalls, bool[,] verticalWalls)
    {
        for (int row = 0; row < horizontalWalls.GetLength(0); row++)
        {
            for (int column = 0; column < horizontalWalls.GetLength(1); column++)
            {
                horizontalWalls[row, column] = true;
            }
        }

        for (int row = 0; row < verticalWalls.GetLength(0); row++)
        {
            for (int column = 0; column < verticalWalls.GetLength(1); column++)
            {
                verticalWalls[row, column] = true;
            }
        }
    }

    private void CarveMaze(bool[,] horizontalWalls, bool[,] verticalWalls, System.Random random)
    {
        bool[,] visited = new bool[rows, columns];
        Stack<Vector2Int> stack = new Stack<Vector2Int>();
        Vector2Int start = new Vector2Int(0, 0);

        visited[start.y, start.x] = true;
        stack.Push(start);

        while (stack.Count > 0)
        {
            Vector2Int current = stack.Peek();
            List<Vector2Int> availableDirections = new List<Vector2Int>();

            foreach (Vector2Int direction in Directions)
            {
                int nextColumn = current.x + direction.x;
                int nextRow = current.y + direction.y;

                if (nextColumn < 0 || nextColumn >= columns || nextRow < 0 || nextRow >= rows)
                {
                    continue;
                }

                if (!visited[nextRow, nextColumn])
                {
                    availableDirections.Add(direction);
                }
            }

            if (availableDirections.Count == 0)
            {
                stack.Pop();
                continue;
            }

            Vector2Int directionToCarve = availableDirections[random.Next(availableDirections.Count)];
            Vector2Int next = current + directionToCarve;

            if (directionToCarve == Vector2Int.up)
            {
                horizontalWalls[current.y + 1, current.x] = false;
            }
            else if (directionToCarve == Vector2Int.down)
            {
                horizontalWalls[current.y, current.x] = false;
            }
            else if (directionToCarve == Vector2Int.right)
            {
                verticalWalls[current.y, current.x + 1] = false;
            }
            else if (directionToCarve == Vector2Int.left)
            {
                verticalWalls[current.y, current.x] = false;
            }

            visited[next.y, next.x] = true;
            stack.Push(next);
        }
    }

    private void ResizeFloor()
    {
        if (floor == null)
        {
            return;
        }

        float totalWidth = GetTotalWidth();
        float totalDepth = GetTotalDepth();

        floor.position = new Vector3(0f, -0.5f, 0f);
        floor.localScale = new Vector3(totalWidth, 1f, totalDepth);
    }

    private void BuildHorizontalRuns(bool[,] horizontalWalls, Material material)
    {
        float totalWidth = GetTotalWidth();
        float totalDepth = GetTotalDepth();
        float leftEdge = -totalWidth * 0.5f;
        float bottomEdge = -totalDepth * 0.5f;

        for (int row = 0; row < rows + 1; row++)
        {
            int column = 0;
            while (column < columns)
            {
                if (!horizontalWalls[row, column])
                {
                    column++;
                    continue;
                }

                int startColumn = column;
                while (column < columns && horizontalWalls[row, column])
                {
                    column++;
                }

                int runLength = column - startColumn;
                float length = runLength * cellSize + wallThickness;
                float x = leftEdge + startColumn * cellSize + (runLength * cellSize * 0.5f);
                float z = bottomEdge + row * cellSize;

                CreateWallSegment(
                    $"HWall_{row}_{startColumn}",
                    new Vector3(x, wallHeight * 0.5f, z),
                    new Vector3(length, wallHeight, wallThickness),
                    material);
            }
        }
    }

    private void BuildVerticalRuns(bool[,] verticalWalls, Material material)
    {
        float totalWidth = GetTotalWidth();
        float totalDepth = GetTotalDepth();
        float leftEdge = -totalWidth * 0.5f;
        float bottomEdge = -totalDepth * 0.5f;

        for (int column = 0; column < columns + 1; column++)
        {
            int row = 0;
            while (row < rows)
            {
                if (!verticalWalls[row, column])
                {
                    row++;
                    continue;
                }

                int startRow = row;
                while (row < rows && verticalWalls[row, column])
                {
                    row++;
                }

                int runLength = row - startRow;
                float length = runLength * cellSize + wallThickness;
                float x = leftEdge + column * cellSize;
                float z = bottomEdge + startRow * cellSize + (runLength * cellSize * 0.5f);

                CreateWallSegment(
                    $"VWall_{column}_{startRow}",
                    new Vector3(x, wallHeight * 0.5f, z),
                    new Vector3(wallThickness, wallHeight, length),
                    material);
            }
        }
    }

    private void CreateWallSegment(string objectName, Vector3 position, Vector3 scale, Material material)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = objectName;
        wall.transform.SetParent(generatedRoot, false);
        wall.transform.localPosition = position;
        wall.transform.localScale = scale;

        Renderer renderer = wall.GetComponent<Renderer>();
        if (renderer != null && material != null)
        {
            renderer.sharedMaterial = material;
        }
    }

    private void PositionPlayerAtStart()
    {
        if (player == null)
        {
            return;
        }

        float totalWidth = GetTotalWidth();
        float totalDepth = GetTotalDepth();
        float leftEdge = -totalWidth * 0.5f;
        float bottomEdge = -totalDepth * 0.5f;

        Vector3 startPosition = new Vector3(
            leftEdge + cellSize * 0.5f,
            0f,
            bottomEdge + cellSize * 0.5f);

        player.WarpTo(startPosition);
    }

    private float GetTotalWidth()
    {
        return columns * cellSize + wallThickness;
    }

    private float GetTotalDepth()
    {
        return rows * cellSize + wallThickness;
    }
}
