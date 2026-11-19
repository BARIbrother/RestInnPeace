using UnityEngine;

// 방 셀에 꽂는 스프라이트 묶음. 플레이스홀더를 나중에 구매 에셋으로 갈아끼울 때 이 에셋만 바꾸면 된다.
[CreateAssetMenu(menuName = "Rest Inn Peace/Room Art Set", fileName = "RoomArtSet")]
public class RoomArtSet : ScriptableObject
{
    public Sprite Back;
    // 조사 전 내부를 가리는 닫힌 방 외벽.
    public Sprite Facade;
    public Sprite Floor;
    public Sprite SideShadow;
    public Sprite Bed;
    public Sprite Nightstand;
    public Sprite DoorClosed;
    public Sprite DoorOpen;
    public Sprite Lock;
    public Sprite ClueFur;
    public Sprite ClueSmell;
    public Sprite ClueFootprint;
    public Sprite ClueScratch;
    public Sprite SelectionFrame;
    public Sprite GuestSilhouette;
    public Sprite OwnerDesk;
    public Sprite Corridor;
}
