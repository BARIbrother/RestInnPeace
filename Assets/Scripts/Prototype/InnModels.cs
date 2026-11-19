using System.Collections.Generic;

public class Guest
{
    public int Id;
    public Species Species;
    public Job Job;
    // 직업 주장. 인간은 실제 직업과 같고, 괴물은 거짓일 수 있다.
    public Job ClaimedJob;
    // 플레이어가 클릭해 주장을 듣기 전까지는 직업 주장이 숨겨진다.
    public bool ClaimHeard;
    public int CheckInOrder;
    public bool IsOut;
    public bool HeardHallway;
    public bool HeardScream;
    public bool SurvivedAttack;
    public readonly List<string> NightSightings = new List<string>();
    public bool TalkedToday;
    public NightOrder PendingOrder = NightOrder.None;
    public int OrderRoomNumber;
    public int OrderGuestId;
    // 수색·보호 직업이 밤에 알아낸 요약. 아침에 기록으로 붙는다.
    public readonly List<string> PendingMorningNotes = new List<string>();
    public string Memo = "";
    public readonly List<string> Notes = new List<string>();
    // NameTable에서 뽑은 표시 이름. 비면 손님 #Id로 떨어진다.
    public string Name;

    public bool IsMonster => Species != Species.Human;
    public string DisplayName => string.IsNullOrEmpty(Name) ? $"손님 #{Id}" : Name;
}

public class Room
{
    public int Number;
    public int Floor;
    public int Column;
    public Guest Guest;
    public bool HasFur;
    public bool HasSmell;
    // 전날 밤 열쇠 수리공 등으로 잠긴 방. 낮 방 조사에서 제외된다.
    public bool DoorLocked;
    // 청소부가 볼 수 있는 약한 흔적.
    public bool HasFootprint;
    // 주인이 이 방을 조사해 내부를 본 적 있는지. 조사 전에는 닫힌 방 외벽만 보인다.
    public bool Inspected;

    public bool IsEmpty => Guest == null;
    public string DisplayName => $"{Number}호실";
}

public class MonsterState
{
    public Guest Monster;
    public int Stage = 1;
    public int Rest;
    public bool Attacked;
    public Guest Target;
    public bool Rampage;
}
