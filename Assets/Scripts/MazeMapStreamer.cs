using System;
using System.Collections.Generic;
using UnityEngine;

public class MazeMapStreamer : MonoBehaviour
{
    [Header("Map")]
    [SerializeField] private int mapWidth = 20;
    [SerializeField] private int mapHeight = 30;
    [SerializeField] private int worldSeed = 12345;
    [Range(0.05f, 1f)]
    [SerializeField] private float fillPercent = 0.6f;
    [Min(0)]
    [SerializeField] private int loadRadius = 1;

    [Header("Maze Generation")]
    [SerializeField] private MazeGenerationSettings mazeSettings = new MazeGenerationSettings();
    [SerializeField] private Material wallMaterial;
    [SerializeField] private Renderer floorMaterialSource;
    [SerializeField] private Material floorMaterial;

    [Header("Scene References")]
    [SerializeField] private ArrowKeyMover player;

    private readonly Dictionary<Vector2Int, ProceduralMazeGenerator> loadedChunks =
        new Dictionary<Vector2Int, ProceduralMazeGenerator>();

    private Transform chunksRoot;
    private bool[,] occupiedSlots;
    private Vector2Int startCoordinate;
    private Vector2Int currentCoordinate;
    private bool hasCurrentCoordinate;

    private static readonly Vector2Int[] CardinalDirections =
    {
        new Vector2Int(0, 1),
        new Vector2Int(1, 0),
        new Vector2Int(0, -1),
        new Vector2Int(-1, 0),
    };

    private void Awake()
    {
        InitializeMap();
    }

    private void Update()
    {
        if (player == null || occupiedSlots == null)
        {
            return;
        }

        Vector2Int playerCoordinate = WorldToMapCoordinate(player.transform.position);
        if (!hasCurrentCoordinate || playerCoordinate != currentCoordinate)
        {
            currentCoordinate = ClampToMap(playerCoordinate);
            hasCurrentCoordinate = true;
            RefreshLoadedChunks();
        }
    }

    [ContextMenu("Regenerate Map")]
    public void InitializeMap()
    {
        mapWidth = Mathf.Max(1, mapWidth);
        mapHeight = Mathf.Max(1, mapHeight);
        fillPercent = Mathf.Clamp01(fillPercent);
        loadRadius = Mathf.Max(0, loadRadius);
        mazeSettings.Clamp();

        EnsureChunksRoot();
        ClearLoadedChunks();
        BuildOccupiedMap();

        currentCoordinate = startCoordinate;
        hasCurrentCoordinate = true;
        RefreshLoadedChunks();
        PositionPlayerAtStart();
    }

    private void EnsureChunksRoot()
    {
        if (chunksRoot != null)
        {
            return;
        }

        Transform existing = transform.Find("LoadedMazeChunks");
        if (existing != null)
        {
            chunksRoot = existing;
            return;
        }

        GameObject root = new GameObject("LoadedMazeChunks");
        chunksRoot = root.transform;
        chunksRoot.SetParent(transform, false);
    }

    private void ClearLoadedChunks()
    {
        loadedChunks.Clear();

        if (chunksRoot == null)
        {
            return;
        }

        for (int index = chunksRoot.childCount - 1; index >= 0; index--)
        {
            Destroy(chunksRoot.GetChild(index).gameObject);
        }
    }

    private void BuildOccupiedMap()
    {
        occupiedSlots = new bool[mapWidth, mapHeight];
        startCoordinate = new Vector2Int(mapWidth / 2, mapHeight / 2);
        occupiedSlots[startCoordinate.x, startCoordinate.y] = true;

        int totalSlots = mapWidth * mapHeight;
        int targetSlots = Mathf.Clamp(Mathf.RoundToInt(totalSlots * fillPercent), 1, totalSlots);
        System.Random random = new System.Random(worldSeed);
        List<Vector2Int> frontier = new List<Vector2Int>();

        AddFrontierNeighbors(startCoordinate, frontier);

        int occupiedCount = 1;
        while (occupiedCount < targetSlots && frontier.Count > 0)
        {
            int frontierIndex = random.Next(frontier.Count);
            Vector2Int coordinate = frontier[frontierIndex];
            frontier.RemoveAt(frontierIndex);

            if (IsOccupied(coordinate))
            {
                continue;
            }

            occupiedSlots[coordinate.x, coordinate.y] = true;
            occupiedCount++;
            AddFrontierNeighbors(coordinate, frontier);
        }
    }

    private void AddFrontierNeighbors(Vector2Int coordinate, List<Vector2Int> frontier)
    {
        for (int index = 0; index < CardinalDirections.Length; index++)
        {
            Vector2Int neighbor = coordinate + CardinalDirections[index];
            if (IsInsideMap(neighbor) && !IsOccupied(neighbor))
            {
                frontier.Add(neighbor);
            }
        }
    }

    private void RefreshLoadedChunks()
    {
        HashSet<Vector2Int> required = new HashSet<Vector2Int>();

        for (int y = currentCoordinate.y - loadRadius; y <= currentCoordinate.y + loadRadius; y++)
        {
            for (int x = currentCoordinate.x - loadRadius; x <= currentCoordinate.x + loadRadius; x++)
            {
                Vector2Int coordinate = new Vector2Int(x, y);
                if (!IsOccupied(coordinate))
                {
                    continue;
                }

                required.Add(coordinate);
                if (!loadedChunks.ContainsKey(coordinate))
                {
                    LoadChunk(coordinate);
                }
            }
        }

        List<Vector2Int> unloadList = new List<Vector2Int>();
        foreach (Vector2Int coordinate in loadedChunks.Keys)
        {
            if (!required.Contains(coordinate))
            {
                unloadList.Add(coordinate);
            }
        }

        for (int index = 0; index < unloadList.Count; index++)
        {
            UnloadChunk(unloadList[index]);
        }
    }

    private void LoadChunk(Vector2Int coordinate)
    {
        GameObject chunk = new GameObject("MazeChunk_" + coordinate.x + "_" + coordinate.y);
        chunk.transform.SetParent(chunksRoot, false);
        chunk.transform.localPosition = GetChunkLocalPosition(coordinate);

        ProceduralMazeGenerator generator = chunk.AddComponent<ProceduralMazeGenerator>();
        generator.GenerateChunk(
            mazeSettings,
            GetChunkSeed(coordinate),
            BuildEntrances(coordinate),
            wallMaterial,
            ResolveFloorMaterial(),
            true);

        loadedChunks.Add(coordinate, generator);
    }

    private void UnloadChunk(Vector2Int coordinate)
    {
        ProceduralMazeGenerator generator;
        if (!loadedChunks.TryGetValue(coordinate, out generator))
        {
            return;
        }

        if (generator != null)
        {
            Destroy(generator.gameObject);
        }

        loadedChunks.Remove(coordinate);
    }

    private MazeChunkEntrances BuildEntrances(Vector2Int coordinate)
    {
        MazeChunkEntrances entrances = new MazeChunkEntrances();

        Vector2Int north = coordinate + Vector2Int.up;
        Vector2Int east = coordinate + Vector2Int.right;
        Vector2Int south = coordinate + Vector2Int.down;
        Vector2Int west = coordinate + Vector2Int.left;

        entrances.north = IsOccupied(north);
        entrances.east = IsOccupied(east);
        entrances.south = IsOccupied(south);
        entrances.west = IsOccupied(west);

        entrances.northColumn = GetSharedColumnDoor(coordinate, north);
        entrances.southColumn = GetSharedColumnDoor(south, coordinate);
        entrances.eastRow = GetSharedRowDoor(coordinate, east);
        entrances.westRow = GetSharedRowDoor(west, coordinate);

        return entrances;
    }

    private int GetSharedColumnDoor(Vector2Int southCoordinate, Vector2Int northCoordinate)
    {
        int hash = Hash(worldSeed, southCoordinate.x, southCoordinate.y, northCoordinate.x, northCoordinate.y, 17);
        return (hash & int.MaxValue) % mazeSettings.columns;
    }

    private int GetSharedRowDoor(Vector2Int westCoordinate, Vector2Int eastCoordinate)
    {
        int hash = Hash(worldSeed, westCoordinate.x, westCoordinate.y, eastCoordinate.x, eastCoordinate.y, 31);
        return (hash & int.MaxValue) % mazeSettings.rows;
    }

    private Material ResolveFloorMaterial()
    {
        if (floorMaterial != null)
        {
            return floorMaterial;
        }

        return floorMaterialSource != null ? floorMaterialSource.sharedMaterial : null;
    }

    private void PositionPlayerAtStart()
    {
        if (player == null)
        {
            return;
        }

        ProceduralMazeGenerator startChunk;
        if (!loadedChunks.TryGetValue(startCoordinate, out startChunk) || startChunk == null)
        {
            return;
        }

        player.WarpTo(startChunk.transform.TransformPoint(startChunk.GetStartPosition()));
    }

    private Vector3 GetChunkLocalPosition(Vector2Int coordinate)
    {
        return new Vector3(
            (coordinate.x - startCoordinate.x) * mazeSettings.ChunkStrideX,
            0f,
            (coordinate.y - startCoordinate.y) * mazeSettings.ChunkStrideZ);
    }

    private Vector2Int WorldToMapCoordinate(Vector3 worldPosition)
    {
        Vector3 localPosition = transform.InverseTransformPoint(worldPosition);
        int x = Mathf.RoundToInt(localPosition.x / mazeSettings.ChunkStrideX) + startCoordinate.x;
        int y = Mathf.RoundToInt(localPosition.z / mazeSettings.ChunkStrideZ) + startCoordinate.y;
        return new Vector2Int(x, y);
    }

    private Vector2Int ClampToMap(Vector2Int coordinate)
    {
        return new Vector2Int(
            Mathf.Clamp(coordinate.x, 0, mapWidth - 1),
            Mathf.Clamp(coordinate.y, 0, mapHeight - 1));
    }

    private bool IsInsideMap(Vector2Int coordinate)
    {
        return coordinate.x >= 0
            && coordinate.y >= 0
            && coordinate.x < mapWidth
            && coordinate.y < mapHeight;
    }

    private bool IsOccupied(Vector2Int coordinate)
    {
        return IsInsideMap(coordinate) && occupiedSlots[coordinate.x, coordinate.y];
    }

    private int GetChunkSeed(Vector2Int coordinate)
    {
        return Hash(worldSeed, coordinate.x, coordinate.y, 0, 0, 53);
    }

    private static int Hash(int seed, int a, int b, int c, int d, int salt)
    {
        unchecked
        {
            int hash = seed;
            hash = hash * 397 ^ a;
            hash = hash * 397 ^ b;
            hash = hash * 397 ^ c;
            hash = hash * 397 ^ d;
            hash = hash * 397 ^ salt;
            return hash;
        }
    }
}
