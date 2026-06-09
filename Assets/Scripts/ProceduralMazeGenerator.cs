using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class MazeGenerationSettings
{
    [Header("Maze Size")]
    public int columns = 8;
    public int rows = 8;
    public float cellSize = 2f;

    [Header("Dungeon Layout")]
    public int minRooms = 6;
    public int maxRooms = 12;
    public int minRoomWidth = 2;
    public int maxRoomWidth = 4;
    public int minRoomHeight = 2;
    public int maxRoomHeight = 4;
    [Range(0f, 1f)] public float loopChance = 0.2f;

    [Header("Wall Style")]
    public float wallThickness = 0.5f;
    public float wallHeight = 0.8f;

    public void Clamp()
    {
        columns = Mathf.Max(1, columns);
        rows = Mathf.Max(1, rows);
        cellSize = Mathf.Max(0.5f, cellSize);
        minRooms = Mathf.Max(1, minRooms);
        maxRooms = Mathf.Max(minRooms, maxRooms);
        minRoomWidth = Mathf.Clamp(minRoomWidth, 1, columns);
        maxRoomWidth = Mathf.Clamp(maxRoomWidth, minRoomWidth, columns);
        minRoomHeight = Mathf.Clamp(minRoomHeight, 1, rows);
        maxRoomHeight = Mathf.Clamp(maxRoomHeight, minRoomHeight, rows);
        wallThickness = Mathf.Max(0.1f, wallThickness);
        wallHeight = Mathf.Max(0.1f, wallHeight);
    }

    public float ChunkStrideX
    {
        get { return columns * cellSize; }
    }

    public float ChunkStrideZ
    {
        get { return rows * cellSize; }
    }

    public float TotalWidth
    {
        get { return columns * cellSize + wallThickness; }
    }

    public float TotalDepth
    {
        get { return rows * cellSize + wallThickness; }
    }
}

public struct MazeChunkEntrances
{
    public bool north;
    public bool east;
    public bool south;
    public bool west;
    public int northColumn;
    public int southColumn;
    public int eastRow;
    public int westRow;
}

public class ProceduralMazeGenerator : MonoBehaviour
{
    private sealed class DungeonRoom
    {
        public readonly RectInt bounds;

        public DungeonRoom(RectInt bounds)
        {
            this.bounds = bounds;
        }

        public Vector2Int CenterCell
        {
            get
            {
                return new Vector2Int(
                    bounds.xMin + bounds.width / 2,
                    bounds.yMin + bounds.height / 2);
            }
        }
    }

    private readonly struct RoomConnection
    {
        public readonly DungeonRoom from;
        public readonly DungeonRoom to;

        public RoomConnection(DungeonRoom from, DungeonRoom to)
        {
            this.from = from;
            this.to = to;
        }
    }

    [SerializeField] private bool generateOnAwake;
    [SerializeField] private MazeGenerationSettings settings = new MazeGenerationSettings();

    [Header("Standalone Seed")]
    [SerializeField] private bool useRandomSeed = true;
    [SerializeField] private int seed = 12345;
    [SerializeField] private int lastGeneratedSeed;

    [Header("Scene References")]
    [SerializeField] private Material wallMaterial;
    [SerializeField] private Material floorMaterial;
    [SerializeField] private ArrowKeyMover player;

    private Transform generatedRoot;
    private DungeonRoom startRoom;

    private static readonly Vector2Int[] Directions =
    {
        new Vector2Int(0, 1),
        new Vector2Int(1, 0),
        new Vector2Int(0, -1),
        new Vector2Int(-1, 0),
    };

    private void Awake()
    {
        if (generateOnAwake)
        {
            Generate();
        }
    }

    [ContextMenu("Generate Maze")]
    public void Generate()
    {
        int activeSeed = useRandomSeed ? Environment.TickCount : seed;
        lastGeneratedSeed = activeSeed;

        MazeChunkEntrances entrances = new MazeChunkEntrances
        {
            north = true,
            east = true,
            south = true,
            west = true,
            northColumn = settings.columns / 2,
            southColumn = settings.columns / 2,
            eastRow = settings.rows / 2,
            westRow = settings.rows / 2
        };

        GenerateChunk(settings, activeSeed, entrances, wallMaterial, floorMaterial, true);
        PositionPlayerAtStart();
    }

    public void GenerateChunk(
        MazeGenerationSettings generationSettings,
        int activeSeed,
        MazeChunkEntrances entrances,
        Material resolvedWallMaterial,
        Material resolvedFloorMaterial,
        bool createFloor)
    {
        settings = generationSettings;
        settings.Clamp();
        lastGeneratedSeed = activeSeed;

        EnsureGeneratedRoot();
        ClearGeneratedObjects();

        bool[,] horizontalWalls = new bool[settings.rows + 1, settings.columns];
        bool[,] verticalWalls = new bool[settings.rows, settings.columns + 1];

        InitializeWalls(horizontalWalls, verticalWalls);
        GenerateDungeon(horizontalWalls, verticalWalls, new System.Random(activeSeed));
        CarveMapEntrances(horizontalWalls, verticalWalls, entrances);

        if (createFloor)
        {
            BuildFloor(resolvedFloorMaterial);
        }

        BuildHorizontalRuns(horizontalWalls, resolvedWallMaterial);
        BuildVerticalRuns(verticalWalls, resolvedWallMaterial);
    }

    public Vector3 GetStartPosition()
    {
        Vector2Int startCell = startRoom != null
            ? startRoom.CenterCell
            : new Vector2Int(settings.columns / 2, settings.rows / 2);

        return CellCenterToLocalPosition(startCell);
    }

    private void EnsureGeneratedRoot()
    {
        if (generatedRoot != null)
        {
            return;
        }

        Transform existing = transform.Find("GeneratedMaze");
        if (existing != null)
        {
            generatedRoot = existing;
            return;
        }

        GameObject root = new GameObject("GeneratedMaze");
        generatedRoot = root.transform;
        generatedRoot.SetParent(transform, false);
    }

    private void ClearGeneratedObjects()
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

    private void GenerateDungeon(bool[,] horizontalWalls, bool[,] verticalWalls, System.Random random)
    {
        List<DungeonRoom> rooms = new List<DungeonRoom>();
        List<RoomConnection> connections = new List<RoomConnection>();

        CreateRoomLayout(rooms, connections, random);
        CarveRooms(rooms, horizontalWalls, verticalWalls);
        CarveConnections(connections, horizontalWalls, verticalWalls, random);
        AddLoopConnections(rooms, connections, horizontalWalls, verticalWalls, random);
    }

    private void CreateRoomLayout(
        List<DungeonRoom> rooms,
        List<RoomConnection> connections,
        System.Random random)
    {
        int firstWidth = random.Next(settings.minRoomWidth, settings.maxRoomWidth + 1);
        int firstHeight = random.Next(settings.minRoomHeight, settings.maxRoomHeight + 1);
        RectInt firstBounds = new RectInt(
            Mathf.Max(0, (settings.columns - firstWidth) / 2),
            Mathf.Max(0, (settings.rows - firstHeight) / 2),
            firstWidth,
            firstHeight);

        DungeonRoom firstRoom = new DungeonRoom(firstBounds);
        rooms.Add(firstRoom);
        startRoom = firstRoom;

        int targetRooms = random.Next(settings.minRooms, settings.maxRooms + 1);
        int attempts = Mathf.Max(100, targetRooms * 40);

        while (rooms.Count < targetRooms && attempts > 0)
        {
            attempts--;
            DungeonRoom anchor = rooms[random.Next(rooms.Count)];
            Vector2Int direction = Directions[random.Next(Directions.Length)];
            int width = random.Next(settings.minRoomWidth, settings.maxRoomWidth + 1);
            int height = random.Next(settings.minRoomHeight, settings.maxRoomHeight + 1);

            RectInt candidateBounds = CreateAdjacentRoomBounds(anchor.bounds, direction, width, height, random);
            if (!IsInsideGrid(candidateBounds) || OverlapsAnyRoom(candidateBounds, rooms))
            {
                continue;
            }

            DungeonRoom room = new DungeonRoom(candidateBounds);
            rooms.Add(room);
            connections.Add(new RoomConnection(anchor, room));
        }
    }

    private RectInt CreateAdjacentRoomBounds(
        RectInt anchor,
        Vector2Int direction,
        int width,
        int height,
        System.Random random)
    {
        if (direction == Vector2Int.up)
        {
            int x = RandomOverlappingStart(anchor.xMin, anchor.xMax, width, random);
            return new RectInt(x, anchor.yMax, width, height);
        }

        if (direction == Vector2Int.down)
        {
            int x = RandomOverlappingStart(anchor.xMin, anchor.xMax, width, random);
            return new RectInt(x, anchor.yMin - height, width, height);
        }

        if (direction == Vector2Int.right)
        {
            int y = RandomOverlappingStart(anchor.yMin, anchor.yMax, height, random);
            return new RectInt(anchor.xMax, y, width, height);
        }

        int leftY = RandomOverlappingStart(anchor.yMin, anchor.yMax, height, random);
        return new RectInt(anchor.xMin - width, leftY, width, height);
    }

    private int RandomOverlappingStart(int anchorMin, int anchorMax, int size, System.Random random)
    {
        int min = anchorMin - size + 1;
        int max = anchorMax - 1;
        return random.Next(min, max + 1);
    }

    private bool IsInsideGrid(RectInt bounds)
    {
        return bounds.xMin >= 0
            && bounds.yMin >= 0
            && bounds.xMax <= settings.columns
            && bounds.yMax <= settings.rows;
    }

    private bool OverlapsAnyRoom(RectInt bounds, List<DungeonRoom> rooms)
    {
        foreach (DungeonRoom room in rooms)
        {
            if (bounds.Overlaps(room.bounds))
            {
                return true;
            }
        }

        return false;
    }

    private void CarveRooms(List<DungeonRoom> rooms, bool[,] horizontalWalls, bool[,] verticalWalls)
    {
        foreach (DungeonRoom room in rooms)
        {
            RectInt bounds = room.bounds;

            for (int row = bounds.yMin; row < bounds.yMax; row++)
            {
                for (int column = bounds.xMin + 1; column < bounds.xMax; column++)
                {
                    verticalWalls[row, column] = false;
                }
            }

            for (int row = bounds.yMin + 1; row < bounds.yMax; row++)
            {
                for (int column = bounds.xMin; column < bounds.xMax; column++)
                {
                    horizontalWalls[row, column] = false;
                }
            }
        }
    }

    private void CarveConnections(
        List<RoomConnection> connections,
        bool[,] horizontalWalls,
        bool[,] verticalWalls,
        System.Random random)
    {
        foreach (RoomConnection connection in connections)
        {
            CarveConnection(connection.from, connection.to, horizontalWalls, verticalWalls, random);
        }
    }

    private void AddLoopConnections(
        List<DungeonRoom> rooms,
        List<RoomConnection> connections,
        bool[,] horizontalWalls,
        bool[,] verticalWalls,
        System.Random random)
    {
        for (int firstIndex = 0; firstIndex < rooms.Count; firstIndex++)
        {
            for (int secondIndex = firstIndex + 1; secondIndex < rooms.Count; secondIndex++)
            {
                DungeonRoom first = rooms[firstIndex];
                DungeonRoom second = rooms[secondIndex];

                if (!AreRoomsAdjacent(first.bounds, second.bounds)
                    || HasConnection(connections, first, second)
                    || random.NextDouble() > settings.loopChance)
                {
                    continue;
                }

                connections.Add(new RoomConnection(first, second));
                CarveConnection(first, second, horizontalWalls, verticalWalls, random);
            }
        }
    }

    private bool HasConnection(List<RoomConnection> connections, DungeonRoom first, DungeonRoom second)
    {
        foreach (RoomConnection connection in connections)
        {
            if ((connection.from == first && connection.to == second)
                || (connection.from == second && connection.to == first))
            {
                return true;
            }
        }

        return false;
    }

    private bool AreRoomsAdjacent(RectInt first, RectInt second)
    {
        bool verticalNeighbors = (first.yMax == second.yMin || second.yMax == first.yMin)
            && RangesOverlap(first.xMin, first.xMax, second.xMin, second.xMax);

        bool horizontalNeighbors = (first.xMax == second.xMin || second.xMax == first.xMin)
            && RangesOverlap(first.yMin, first.yMax, second.yMin, second.yMax);

        return verticalNeighbors || horizontalNeighbors;
    }

    private bool RangesOverlap(int firstMin, int firstMax, int secondMin, int secondMax)
    {
        return Mathf.Max(firstMin, secondMin) < Mathf.Min(firstMax, secondMax);
    }

    private void CarveConnection(
        DungeonRoom firstRoom,
        DungeonRoom secondRoom,
        bool[,] horizontalWalls,
        bool[,] verticalWalls,
        System.Random random)
    {
        RectInt first = firstRoom.bounds;
        RectInt second = secondRoom.bounds;

        if (first.yMax == second.yMin || second.yMax == first.yMin)
        {
            int wallRow = first.yMax == second.yMin ? first.yMax : second.yMax;
            int minColumn = Mathf.Max(first.xMin, second.xMin);
            int maxColumn = Mathf.Min(first.xMax, second.xMax);
            int doorColumn = random.Next(minColumn, maxColumn);
            horizontalWalls[wallRow, doorColumn] = false;
            return;
        }

        if (first.xMax == second.xMin || second.xMax == first.xMin)
        {
            int wallColumn = first.xMax == second.xMin ? first.xMax : second.xMax;
            int minRow = Mathf.Max(first.yMin, second.yMin);
            int maxRow = Mathf.Min(first.yMax, second.yMax);
            int doorRow = random.Next(minRow, maxRow);
            verticalWalls[doorRow, wallColumn] = false;
        }
    }

    private void CarveMapEntrances(
        bool[,] horizontalWalls,
        bool[,] verticalWalls,
        MazeChunkEntrances entrances)
    {
        if (startRoom == null)
        {
            return;
        }

        if (entrances.north)
        {
            int column = Mathf.Clamp(entrances.northColumn, 0, settings.columns - 1);
            CarvePathBetweenCells(startRoom.CenterCell, new Vector2Int(column, settings.rows - 1), horizontalWalls, verticalWalls);
            horizontalWalls[settings.rows, column] = false;
        }

        if (entrances.south)
        {
            int column = Mathf.Clamp(entrances.southColumn, 0, settings.columns - 1);
            CarvePathBetweenCells(startRoom.CenterCell, new Vector2Int(column, 0), horizontalWalls, verticalWalls);
            horizontalWalls[0, column] = false;
        }

        if (entrances.east)
        {
            int row = Mathf.Clamp(entrances.eastRow, 0, settings.rows - 1);
            CarvePathBetweenCells(startRoom.CenterCell, new Vector2Int(settings.columns - 1, row), horizontalWalls, verticalWalls);
            verticalWalls[row, settings.columns] = false;
        }

        if (entrances.west)
        {
            int row = Mathf.Clamp(entrances.westRow, 0, settings.rows - 1);
            CarvePathBetweenCells(startRoom.CenterCell, new Vector2Int(0, row), horizontalWalls, verticalWalls);
            verticalWalls[row, 0] = false;
        }
    }

    private void CarvePathBetweenCells(
        Vector2Int from,
        Vector2Int to,
        bool[,] horizontalWalls,
        bool[,] verticalWalls)
    {
        Vector2Int current = from;

        while (current.x != to.x)
        {
            Vector2Int next = new Vector2Int(current.x + Math.Sign(to.x - current.x), current.y);
            CarveBetweenCells(current, next, horizontalWalls, verticalWalls);
            current = next;
        }

        while (current.y != to.y)
        {
            Vector2Int next = new Vector2Int(current.x, current.y + Math.Sign(to.y - current.y));
            CarveBetweenCells(current, next, horizontalWalls, verticalWalls);
            current = next;
        }
    }

    private void CarveBetweenCells(
        Vector2Int first,
        Vector2Int second,
        bool[,] horizontalWalls,
        bool[,] verticalWalls)
    {
        if (second.x > first.x)
        {
            verticalWalls[first.y, second.x] = false;
        }
        else if (second.x < first.x)
        {
            verticalWalls[first.y, first.x] = false;
        }
        else if (second.y > first.y)
        {
            horizontalWalls[second.y, first.x] = false;
        }
        else if (second.y < first.y)
        {
            horizontalWalls[first.y, first.x] = false;
        }
    }

    private void BuildFloor(Material material)
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.SetParent(generatedRoot, false);
        floor.transform.localPosition = new Vector3(0f, -0.5f, 0f);
        floor.transform.localScale = new Vector3(settings.TotalWidth, 1f, settings.TotalDepth);

        Renderer renderer = floor.GetComponent<Renderer>();
        if (renderer != null && material != null)
        {
            renderer.sharedMaterial = material;
        }
    }

    private void BuildHorizontalRuns(bool[,] horizontalWalls, Material material)
    {
        float totalWidth = settings.TotalWidth;
        float totalDepth = settings.TotalDepth;
        float leftEdge = -totalWidth * 0.5f;
        float bottomEdge = -totalDepth * 0.5f;

        for (int row = 0; row < settings.rows + 1; row++)
        {
            int column = 0;
            while (column < settings.columns)
            {
                if (!horizontalWalls[row, column])
                {
                    column++;
                    continue;
                }

                int startColumn = column;
                while (column < settings.columns && horizontalWalls[row, column])
                {
                    column++;
                }

                int runLength = column - startColumn;
                float length = runLength * settings.cellSize + settings.wallThickness;
                float x = leftEdge + startColumn * settings.cellSize + (runLength * settings.cellSize * 0.5f);
                float z = bottomEdge + row * settings.cellSize;

                CreateWallSegment(
                    "HWall_" + row + "_" + startColumn,
                    new Vector3(x, settings.wallHeight * 0.5f, z),
                    new Vector3(length, settings.wallHeight, settings.wallThickness),
                    material);
            }
        }
    }

    private void BuildVerticalRuns(bool[,] verticalWalls, Material material)
    {
        float totalWidth = settings.TotalWidth;
        float totalDepth = settings.TotalDepth;
        float leftEdge = -totalWidth * 0.5f;
        float bottomEdge = -totalDepth * 0.5f;

        for (int column = 0; column < settings.columns + 1; column++)
        {
            int row = 0;
            while (row < settings.rows)
            {
                if (!verticalWalls[row, column])
                {
                    row++;
                    continue;
                }

                int startRow = row;
                while (row < settings.rows && verticalWalls[row, column])
                {
                    row++;
                }

                int runLength = row - startRow;
                float length = runLength * settings.cellSize + settings.wallThickness;
                float x = leftEdge + column * settings.cellSize;
                float z = bottomEdge + startRow * settings.cellSize + (runLength * settings.cellSize * 0.5f);

                CreateWallSegment(
                    "VWall_" + column + "_" + startRow,
                    new Vector3(x, settings.wallHeight * 0.5f, z),
                    new Vector3(settings.wallThickness, settings.wallHeight, length),
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

    private Vector3 CellCenterToLocalPosition(Vector2Int cell)
    {
        float leftEdge = -settings.TotalWidth * 0.5f;
        float bottomEdge = -settings.TotalDepth * 0.5f;

        return new Vector3(
            leftEdge + cell.x * settings.cellSize + settings.cellSize * 0.5f,
            0f,
            bottomEdge + cell.y * settings.cellSize + settings.cellSize * 0.5f);
    }

    private void PositionPlayerAtStart()
    {
        if (player == null)
        {
            return;
        }

        player.WarpTo(transform.TransformPoint(GetStartPosition()));
    }
}
