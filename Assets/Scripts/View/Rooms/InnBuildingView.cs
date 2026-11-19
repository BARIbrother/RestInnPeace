using System;
using System.Collections.Generic;
using UnityEngine;

// 시작 여관 단면(주인 방 + 손님 방 칸)을 RoomCell 프리팹으로 배치한다.
public class InnBuildingView : MonoBehaviour
{
    public const float CellGap = 0.1f;
    public const float FloorStride = 2.3f + CellGap;
    // 프리팹 기본값(주인 방 + 객실 2칸). Build 시 실제 배치에 맞춰 덮어쓴다.
    public const int DefaultColumns = 3;

    [SerializeField] RoomCellView roomCellPrefab;
    [SerializeField] RoomArtSet artSet;
    [SerializeField] Transform roomsRoot;
    [SerializeField] SpriteRenderer corridorLower;
    [SerializeField] SpriteRenderer corridorUpper;

    readonly List<RoomCellView> cells = new List<RoomCellView>();
    readonly List<SpriteRenderer> corridors = new List<SpriteRenderer>();
    RoomCellView ownerCell;
    RoomCellView selected;
    // 건물 맨 아래(0층)에 두는 로비 칸. 객실 선택과 배타적으로 선택된다.
    LobbyCellView lobbyCell;
    bool lobbySelected;
    int columns = DefaultColumns;
    float lobbyWidth;

    public RoomArtSet ArtSet => artSet;
    public RoomCellView Selected => selected;
    public bool LobbySelected => lobbySelected;
    public IReadOnlyList<RoomCellView> Cells => cells;
    public int Columns => columns;
    public float LobbyWidth => lobbyWidth;
    public int MaxFloor { get; private set; } = 2;

    public event Action<RoomCellView> RoomClicked;
    public event Action<LobbyCellView> LobbyClicked;

    public void Configure(RoomCellView prefab, RoomArtSet art, Transform root, SpriteRenderer lower, SpriteRenderer upper)
    {
        roomCellPrefab = prefab;
        artSet = art;
        roomsRoot = root;
        corridorLower = lower;
        corridorUpper = upper;
    }

    public void Build(IEnumerable<Room> rooms, int ownerLocks, int ownerLocksMax)
    {
        Clear();

        List<Room> roomList = new List<Room>(rooms);
        int maxGuestColumn = 0;
        int maxFloor = 1;
        foreach (Room room in roomList)
        {
            if (room.Column > maxGuestColumn)
            {
                maxGuestColumn = room.Column;
            }

            if (room.Floor > maxFloor)
            {
                maxFloor = room.Floor;
            }
        }

        // 주인 방(열 0) + 손님 객실 열.
        columns = Math.Max(DefaultColumns, maxGuestColumn + 1);
        MaxFloor = maxFloor;
        lobbyWidth = columns * RoomCellView.CellWidth + (columns - 1) * CellGap;

        BuildCorridors(maxFloor);

        ownerCell = SpawnCell();
        ownerCell.transform.localPosition = GetCellPosition(InnGame.OwnerFloor, InnGame.OwnerColumn);
        ownerCell.SetupOwner(InnGame.OwnerFloor, InnGame.OwnerColumn, artSet);
        ownerCell.SetOwnerLocks(ownerLocks, ownerLocksMax);
        ownerCell.Clicked += HandleClick;

        foreach (Room room in roomList)
        {
            RoomCellView cell = SpawnCell();
            cell.transform.localPosition = GetCellPosition(room.Floor, room.Column);
            cell.Bind(room, artSet, false);
            cell.Clicked += HandleClick;
            cells.Add(cell);
        }

        Transform parent = roomsRoot != null ? roomsRoot : transform;
        lobbyCell = LobbyCellView.Create(parent, artSet, lobbyWidth, RoomCellView.CellHeight);
        lobbyCell.transform.localPosition = GetLobbyPosition();
        lobbyCell.Bind(roomList, false);
        lobbyCell.Clicked += HandleLobbyClick;
    }

    public void Refresh(IEnumerable<Room> rooms, int ownerLocks, int ownerLocksMax)
    {
        if (ownerCell != null)
        {
            ownerCell.SetOwnerLocks(ownerLocks, ownerLocksMax);
            ownerCell.SetSelected(selected == ownerCell);
        }

        Dictionary<int, Room> byNumber = new Dictionary<int, Room>();
        foreach (Room room in rooms)
        {
            byNumber[room.Number] = room;
        }

        foreach (RoomCellView cell in cells)
        {
            if (byNumber.TryGetValue(cell.RoomNumber, out Room room))
            {
                cell.Bind(room, artSet, selected == cell);
            }
        }

        if (lobbyCell != null)
        {
            lobbyCell.Bind(rooms, lobbySelected);
        }
    }

    public void Select(RoomCellView cell)
    {
        lobbySelected = false;
        if (lobbyCell != null)
        {
            lobbyCell.SetSelected(false);
        }

        if (selected != null)
        {
            selected.SetSelected(false);
        }

        selected = cell;
        if (selected != null)
        {
            selected.SetSelected(true);
        }
    }

    public void SelectLobby()
    {
        Select(null);
        lobbySelected = true;
        if (lobbyCell != null)
        {
            lobbyCell.SetSelected(true);
        }
    }

    public static Vector3 GetLobbyPosition()
    {
        return new Vector3(0f, -FloorStride, 0f);
    }

    public void SelectOwner()
    {
        Select(ownerCell);
    }

    public void SelectRoom(Room room)
    {
        if (room == null)
        {
            Select(null);
            return;
        }

        foreach (RoomCellView cell in cells)
        {
            if (cell.RoomNumber == room.Number)
            {
                Select(cell);
                return;
            }
        }

        Select(null);
    }

    // 직업 행동 대상을 고르는 동안, 행동 주인을 제외한 방을 강조한다.
    public void SetOrderHighlight(Guest actor, bool enabled)
    {
        foreach (RoomCellView cell in cells)
        {
            bool highlight = enabled && actor != null &&
                             (cell.BoundRoom == null || cell.BoundRoom.Guest != actor);
            cell.SetOrderHighlight(highlight);
        }

        if (ownerCell != null)
        {
            ownerCell.SetOrderHighlight(false);
        }
    }

    public static Vector3 GetCellPosition(int floor, int column)
    {
        float x = column * (RoomCellView.CellWidth + CellGap);
        float y = (floor - 1) * FloorStride;
        return new Vector3(x, y, 0f);
    }

    Vector3 GetCorridorPosition(int floor)
    {
        // 복도 스프라이트 피벗은 아래 가운데라서 건물 가로 중앙에 둔다.
        Vector3 cell = GetCellPosition(floor, 0);
        return new Vector3(cell.x + lobbyWidth * 0.5f, cell.y - 0.55f, 0.1f);
    }

    void BuildCorridors(int maxFloor)
    {
        if (corridorLower != null)
        {
            corridorLower.enabled = false;
        }

        if (corridorUpper != null)
        {
            corridorUpper.enabled = false;
        }

        if (artSet == null || artSet.Corridor == null)
        {
            return;
        }

        Transform parent = roomsRoot != null ? roomsRoot : transform;
        Vector3 corridorScale = new Vector3(lobbyWidth / RoomCellView.CellWidth, 1f, 1f);
        for (int floor = 1; floor <= maxFloor; floor++)
        {
            GameObject go = new GameObject($"Corridor{floor}");
            go.transform.SetParent(parent, false);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = artSet.Corridor;
            renderer.sortingOrder = -1;
            renderer.transform.localPosition = GetCorridorPosition(floor);
            renderer.transform.localScale = corridorScale;
            corridors.Add(renderer);
        }
    }

    RoomCellView SpawnCell()
    {
        Transform parent = roomsRoot != null ? roomsRoot : transform;
        return Instantiate(roomCellPrefab, parent);
    }

    void HandleClick(RoomCellView cell)
    {
        Select(cell);
        RoomClicked?.Invoke(cell);
    }

    void HandleLobbyClick(LobbyCellView lobby)
    {
        SelectLobby();
        LobbyClicked?.Invoke(lobby);
    }

    void Clear()
    {
        if (lobbyCell != null)
        {
            lobbyCell.Clicked -= HandleLobbyClick;
            Destroy(lobbyCell.gameObject);
            lobbyCell = null;
        }

        lobbySelected = false;

        foreach (RoomCellView cell in cells)
        {
            if (cell != null)
            {
                cell.Clicked -= HandleClick;
                Destroy(cell.gameObject);
            }
        }

        cells.Clear();

        if (ownerCell != null)
        {
            ownerCell.Clicked -= HandleClick;
            Destroy(ownerCell.gameObject);
            ownerCell = null;
        }

        foreach (SpriteRenderer corridor in corridors)
        {
            if (corridor != null)
            {
                Destroy(corridor.gameObject);
            }
        }

        corridors.Clear();
        selected = null;
    }
}
