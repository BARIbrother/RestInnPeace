using UnityEngine;

// 방 셀 프리팹 조립 결과를 바로 확인하기 위한 데모. 프로토타입 OnGUI와 동시에 뜨지 않도록 이 오브젝트가 있으면 그쪽 부트스트랩을 막는다.
public class InnRoomDemoBootstrap : MonoBehaviour
{
    [SerializeField] InnBuildingView building;
    [SerializeField] bool spawnSampleGuests = true;

    InnGame game;

    public void SetBuilding(InnBuildingView view, bool sampleGuests = true)
    {
        building = view;
        spawnSampleGuests = sampleGuests;
    }

    void Awake()
    {
        if (building == null)
        {
            building = FindAnyObjectByType<InnBuildingView>();
        }

        if (building == null)
        {
            Debug.LogError("InnBuildingView가 필요합니다.");
            return;
        }

        game = new InnGame();
        if (!spawnSampleGuests)
        {
            foreach (Room room in game.Rooms)
            {
                room.Guest = null;
            }
        }
        else if (game.Rooms.Count > 0 && game.Rooms[0].Guest != null)
        {
            // 문 열림 / 단서 표시를 한눈에 확인하려고 첫 방만 외출·단서 샘플을 켠다.
            game.Rooms[0].Guest.IsOut = true;
            if (game.Rooms.Count > 1)
            {
                game.Rooms[1].HasFur = true;
                game.Rooms[1].DoorLocked = true;
            }

            if (game.Rooms.Count > 2)
            {
                game.Rooms[2].HasSmell = true;
            }
        }

        building.Build(game.Rooms, game.OwnerLocks, GameBalance.OwnerRoomLocks);
        building.RoomClicked += OnRoomClicked;

        EnsureCamera();
    }

    void OnDestroy()
    {
        if (building != null)
        {
            building.RoomClicked -= OnRoomClicked;
        }
    }

    void OnRoomClicked(RoomCellView cell)
    {
        if (cell.IsOwnerRoom)
        {
            Debug.Log($"선택: 주인 방 (자물쇠 {game.OwnerLocks}/{GameBalance.OwnerRoomLocks})");
            return;
        }

        Debug.Log($"선택: {cell.RoomNumber}호실 / 손님={(cell.BoundRoom?.Guest != null ? cell.BoundRoom.Guest.DisplayName : "없음")}");
    }

    static void EnsureCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject go = new GameObject("Main Camera");
            cam = go.AddComponent<Camera>();
            go.tag = "MainCamera";
        }

        cam.orthographic = true;
        cam.orthographicSize = 3.2f;
        cam.transform.position = new Vector3(3.2f, 1.6f, -10f);
        cam.backgroundColor = new Color(0.07f, 0.05f, 0.04f);
        cam.clearFlags = CameraClearFlags.SolidColor;
    }
}
