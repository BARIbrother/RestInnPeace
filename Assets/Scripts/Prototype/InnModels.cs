using System.Collections.Generic;

public class Guest
{
    public int Id;
    public Species Species;
    public Trait Trait;
    public int CheckInOrder;
    public bool IsOut;
    public bool HeardHallway;
    public bool HeardScream;
    public bool SurvivedAttack;
    // 밤에 누군가를 직접 보거나 겪어서 '어젯밤 일'에서 이름을 들어 말할 수 있는 대사.
    public readonly List<string> NightSightings = new List<string>();
    // 대화는 신뢰 없이 손님 1명당 하루 1회만 할 수 있다.
    public bool TalkedToday;
    // 집중 심문에서 맞히거나 검증해 특성이 확정되었는지. 확정된 특성은 다시 맞힐 수 없다.
    public bool TraitKnown;
    public string Memo = "";
    public readonly List<string> Notes = new List<string>();

    public bool IsMonster => Species != Species.Human;
    public string DisplayName => $"손님 #{Id}";
}

public class Room
{
    public int Number;
    public int Floor;
    public int Column;
    public Guest Guest;
    public readonly HashSet<Facility> Installed = new HashSet<Facility>();
    public bool HasFur;
    public bool HasSmell;

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
    // 타깃 범위에 인간이 없어 여관 전체에서 타깃을 고른 상태. 다음 1단계에서 범위를 다시 확인한다.
    public bool Rampage;
}
