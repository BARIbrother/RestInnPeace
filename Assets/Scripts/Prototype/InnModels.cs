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
    // 전날 밤 [복도를 돌아다님]으로 본 손님. 회의 주장 재료.
    public readonly List<Guest> RoamersSeen = new List<Guest>();
    // 대화는 신뢰 없이 손님 1명당 하루 1회만 할 수 있다.
    public bool TalkedToday;
    // 집중 심문에서 맞히거나 검증해 특성이 확정되었는지. 확정된 특성은 다시 맞힐 수 없다.
    public bool TraitKnown;
    // 손님들이 이 손님을 얼마나 의심하는지. 최대치면 처형 요구·처형 가능.
    public int Suspicion;
    // 게임 종료 공개용. 예: 생존 / 처형됨 / 피살
    public string Fate = "생존";
    public string Memo = "";
    public readonly List<string> Notes = new List<string>();
    // 회차 동안 이 손님이 밤에 한 행동. 종료 화면에 공개.
    public readonly List<string> NightActionLog = new List<string>();

    public bool IsMonster => Species != Species.Human;
    public string DisplayName => $"손님 #{Id}";
}

// 회의 테이블에 올라온 주장. 한 번 올라오면 재사용되지 않는다.
public class MeetingClaim
{
    public Guest Accuser;
    public Guest Target;
    public string Text;
    public bool Refuted;
    public bool IsHostFalse;
}

// 조사·밤 제거로 확보한 단서. 회의에서 제시·반박에 소비한다.
public class PlayerClue
{
    public ClueKind Kind;
    public int SourceRoomNumber;
    public int DayFound;

    public string DisplayName
    {
        get
        {
            string kind = GameData.GetClueKindName(Kind);
            if (SourceRoomNumber > 0)
            {
                return $"{kind} ({SourceRoomNumber}호, {DayFound}일)";
            }

            return $"{kind} ({DayFound}일)";
        }
    }
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
    // 밤 예약: 괴물 행동 전에 적용.
    public bool ReserveRemoveFur;
    public bool ReservePlantFur;

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
