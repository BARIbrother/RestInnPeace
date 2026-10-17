using System;

public enum Species
{
    Human,
    Werewolf
}

public enum Trait
{
    Unclean,
    NightOwl,
    Coward,
    Taciturn,
    DeepSleeper,
    Strong,
    Balding,
    Bold,
    Suspicious,
    Hoarse
}

public enum Facility
{
    ConfineLock,
    GuardLock,
    Wolfsbane
}

public enum TargetRule
{
    SameFloorHuman,
    CrossAdjacent,
    AnyHuman
}

// 낮 2 · 밤 2. 이전 단계로 돌아갈 수 없다. (처형은 회의 중 선택)
public enum DayPhase
{
    Recruit,
    Meeting,
    NightPrep
}

// 주인이 보유해 회의에서 제시·반박에 쓰는 단서.
public enum ClueKind
{
    Fur
}

public class SpeciesDefinition
{
    public readonly Species Id;
    public readonly string Name;
    public readonly string Trait;
    public readonly int NightSpeed;

    public SpeciesDefinition(Species id, string name, string trait, int nightSpeed)
    {
        Id = id;
        Name = name;
        Trait = trait;
        NightSpeed = nightSpeed;
    }
}

public class TraitDefinition
{
    public readonly Trait Id;
    public readonly string Name;
    public readonly string Effect;
    public readonly string Detail;

    public TraitDefinition(Trait id, string name, string effect, string detail)
    {
        Id = id;
        Name = name;
        Effect = effect;
        Detail = detail;
    }
}

public class FacilityDefinition
{
    public readonly Facility Id;
    public readonly string Name;
    public readonly string Description;
    public readonly string Detail;
    // 설치할 때 소모하는 신뢰.
    public readonly int Price;

    public FacilityDefinition(Facility id, string name, string description, string detail, int price)
    {
        Id = id;
        Name = name;
        Description = description;
        Detail = detail;
        Price = price;
    }
}

// 도감에 표시하는 단서 항목. 어떻게 발견되고 무엇이 원인일 수 있는지 설명한다.
public class ClueDefinition
{
    public readonly string Name;
    public readonly string Description;
    public readonly string Causes;

    public ClueDefinition(string name, string description, string causes)
    {
        Name = name;
        Description = description;
        Causes = causes;
    }
}

// 게임 내 텍스트에서 태그에 커서를 올리면 띄우는 설명. 도감에서도 이 설명을 우선한다. (Docs/Proposal/09_GuideDescription.md)
// 설명 안의 [표시 문구][원본 태그]는 화면에 [표시 문구]만 보이고, 원본 태그로 동작한다.
public class TooltipDefinition
{
    public readonly string Tag;
    public readonly string Description;

    public TooltipDefinition(string tag, string description)
    {
        Tag = tag;
        Description = description;
    }
}

public class FragmentDefinition<T>
{
    public readonly T Id;
    public readonly string Description;
    public readonly int Weight;
    public readonly Species[] Allowed;
    public readonly Species[] Forbidden;

    public FragmentDefinition(T id, string description, int weight, Species[] allowed, Species[] forbidden)
    {
        Id = id;
        Description = description;
        Weight = weight;
        Allowed = allowed;
        Forbidden = forbidden;
    }

    // 가능 몬스터가 적혀 있으면 그 목록만, 아니면 불가능 몬스터를 뺀 전부가 대상이다.
    public bool IsAvailableFor(Species species)
    {
        if (Allowed.Length > 0)
        {
            return Array.IndexOf(Allowed, species) >= 0;
        }

        return Array.IndexOf(Forbidden, species) < 0;
    }
}

public static class GameData
{
    public static readonly SpeciesDefinition[] Monsters =
    {
        new SpeciesDefinition(Species.Werewolf, "늑대인간", "없음", 100),
    };

    public static readonly TraitDefinition[] Traits =
    {
        new TraitDefinition(Trait.Unclean, "불결함", "같은 층의 방에게 [냄새]를 풍김",
            "매일 밤 같은 층의 모든 방(빈 방 포함)에 [냄새]가 밴다."),
        new TraitDefinition(Trait.NightOwl, "저녁형 인간", "낮에 스스로 방에서 나올 확률이 20%로 더 낮음. 로비로 부르면 응함. 밤에 [복도를 돌아다님]",
            "'로비로 부르기'에는 응한다.\n매일 밤 자기 층에서 [복도를 돌아다님]. 감금 자물쇠로 막을 수 있다."),
        new TraitDefinition(Trait.Coward, "겁쟁이", "같은 층에 [복도를 돌아다님] 또는 자기 방 [문 앞을 서성임]이 있던 밤에 [비명을 지름]",
            "비명을 지른 본인은 '어젯밤 일'에서 비명을 들었다고 말하지 않는다."),
        new TraitDefinition(Trait.Taciturn, "과묵함", "밤에 일어난 일을 이야기하지 않음",
            "'어젯밤 일': 무슨 일이 있었든 항상 아무 소리도 못 들었다고 답한다. 습격을 당했어도 마찬가지다.\n숙면가나 실제로 아무 일도 없었던 손님과 말로는 구별되지 않는다."),
        new TraitDefinition(Trait.DeepSleeper, "숙면가", "밤에 이벤트([복도를 돌아다님], [비명을 지름], [문 앞을 서성임])를 인지하지 못 함",
            "'어젯밤 일': 항상 아무 소리도 못 들었다고 답한다.\n실제로 아무 소리도 없었던 손님과 말로는 구별되지 않는다."),
        new TraitDefinition(Trait.Strong, "괴력", "인간일 경우 50% 확률로 괴물에게 공격받고도 생존. 괴물일 경우 방 자물쇠 무시하고 공격 가능",
            "살아남으면 그 밤 [비명을 지름]이 일어나고, 다음 날 '어젯밤 일'에서 습격당했다고 말한다."),
        new TraitDefinition(Trait.Balding, "탈모", "매일 밤 자기 방에 [털]을 떨어트림",
            "털은 자기 방에만 떨어진다."),
        new TraitDefinition(Trait.Bold, "대담함", "자기 방에 [문 앞을 서성임] 발생 시 문을 열고 당사자의 얼굴을 확인함",
            "'어젯밤 일': 자기 방 앞에 서 있던 손님의 이름을 말한다."),
        new TraitDefinition(Trait.Suspicious, "의심 많음", "낮 회의(로비)에 참가한 사람 중 무작위 한 명을 집어 밤에 [문 앞을 서성임]",
            "그날 로비에 나왔던 손님(자신 제외) 중 1명의 방 앞에서 서성인다. 감금 자물쇠로 막을 수 있다.\n'어젯밤 일': 같은 층에서 돌아다닌 사람을 봤거나, 서성인 방의 주인이 비명을 질렀다고 말할 수 있다."),
        new TraitDefinition(Trait.Hoarse, "목이 쉼", "[비명을 지름] 불가",
            "괴물에게 공격받아 죽어도 [비명을 지름]이 일어나지 않는다."),
    };

    public static readonly FacilityDefinition[] Facilities =
    {
        new FacilityDefinition(Facility.ConfineLock, "녹슨 감금 자물쇠", "하루 동안 밤에 그 방에서 나가는 것 방지",
            "저녁형 인간의 [복도를 돌아다님]과 괴물의 패턴 진행을 그날 밤 막는다.\n방 안에서 일어나는 일(늑대인간의 털 등)은 막지 못한다.", 1),
        new FacilityDefinition(Facility.GuardLock, "녹슨 방범 자물쇠", "하루 동안 밤에 그 방을 향한 공격 시도 방지. 특성에 따라 무시될 수 있음",
            "공격을 막으면 아침에 [문을 긁은 자국]이 발견된다.\n괴력을 가진 괴물은 이 자물쇠를 무시한다.", 1),
        new FacilityDefinition(Facility.Wolfsbane, "투구꽃 향수", "방에 설치하면 그 방 손님을 향한 패턴을 하루 막아 줌",
            "정보원이 알려 준 '효과적인 대응'에 해당할 때만 효과가 있다.\n공격 직전에 막았다면 아침에 [문을 긁은 자국]이 발견된다.", 2),
    };

    public static readonly ClueDefinition[] Clues =
    {
        new ClueDefinition("[복도를 돌아다님]",
            "주인은 직접 알 수 없다.\n같은 층 손님은 '어젯밤 일'에서 복도에 누가 돌아다녔다고 말한다. (숙면가, 돌아다닌 본인 제외)\n밤에 밖에 나와 있던 손님은 같은 층에서 돌아다닌 사람의 이름을 말할 수 있다.",
            "저녁형 인간, 괴물의 패턴"),
        new ClueDefinition("[문 앞을 서성임]",
            "주인은 직접 알 수 없다.\n대담함 손님은 서성인 사람의 얼굴을 확인하고, 겁쟁이 손님은 [비명을 지름].\n같은 층을 돌아다니던 손님은 누가 누구 방 앞에 서 있었는지 말할 수 있다.",
            "의심 많음, 괴물의 패턴"),
        new ClueDefinition("[비명을 지름]",
            "밤 기록에 '비명 소리'가 남는다.\n그 방을 둘러싼 8칸(대각선 포함) 손님은 '어젯밤 일'에서 비명을 들었다고 말한다. (숙면가 제외, 비명을 지른 본인은 못 들음)\n목이 쉼 손님은 비명을 지르지 않는다.",
            "겁쟁이, 괴물의 공격"),
        new ClueDefinition("[털]",
            "밤사이 방에 떨어지며, 발견하기 전까지 계속 남아 있다. '방 조사'로 발견하면 치워져 사라진다.\n손님이 방을 나와 있거나 빈 방일 때 '방 조사'로만 발견할 수 있다.",
            "탈모(자기 방), 괴물의 패턴"),
        new ClueDefinition("[냄새]",
            "밤사이 방에 배어 다음 날 낮 동안 남아 있다.\n'방 조사'로 악취를 발견할 수 있다.",
            "불결함(같은 층 모든 방)"),
        new ClueDefinition("[문을 긁은 자국]",
            "아침 보고에 나온다. 그 방을 노린 괴물의 시도가 설비에 막혔다는 뜻이다.",
            "방범 자물쇠, 효과적인 대응 설비"),
        new ClueDefinition("[시체 발견]",
            "아침 보고에 나온다. 괴물의 공격이 성공한 방이며, 그 밤 [비명을 지름]도 함께 일어난다.",
            "괴물의 공격"),
    };

    public static readonly FragmentDefinition<TargetRule>[] TargetFragments =
    {
        new FragmentDefinition<TargetRule>(TargetRule.SameFloorHuman, "같은 층 무작위 인간", 0, new Species[0], new Species[0]),
        new FragmentDefinition<TargetRule>(TargetRule.CrossAdjacent, "상하좌우 방 1칸", 0, new Species[0], new Species[0]),
        new FragmentDefinition<TargetRule>(TargetRule.AnyHuman, "무작위 1명", 0, new Species[0], new Species[0]),
    };

    // firstRest: 1단계 뒤 휴식 일수, lurk: 2단계에서 [털] 대신 [문 앞을 서성임]
    public static readonly FragmentDefinition<(int firstRest, bool lurk)>[] ProcessFragments =
    {
        new FragmentDefinition<(int firstRest, bool lurk)>((1, false),
            "1단계: 타깃 방 앞에서 [복도를 돌아다님], 1일 휴식 / 2단계: 타깃 방에 [털], 1일 휴식 / 3단계: 타깃 공격, 성공 시 [비명을 지름] 후 사망",
            0, new[] { Species.Werewolf }, new Species[0]),
        new FragmentDefinition<(int firstRest, bool lurk)>((2, false),
            "1단계: 타깃 방 앞에서 [복도를 돌아다님], 2일 휴식 / 2단계: 타깃 방에 [털], 1일 휴식 / 3단계: 타깃 공격, 성공 시 [비명을 지름] 후 사망",
            0, new[] { Species.Werewolf }, new Species[0]),
        new FragmentDefinition<(int firstRest, bool lurk)>((1, true),
            "1단계: 타깃 방 앞에서 [복도를 돌아다님], 1일 휴식 / 2단계: 타깃 방 [문 앞을 서성임], 1일 휴식 / 3단계: 타깃 공격, 성공 시 [비명을 지름] 후 사망",
            0, new[] { Species.Werewolf }, new Species[0]),
        new FragmentDefinition<(int firstRest, bool lurk)>((2, true),
            "1단계: 타깃 방 앞에서 [복도를 돌아다님], 2일 휴식 / 2단계: 타깃 방 [문 앞을 서성임], 1일 휴식 / 3단계: 타깃 공격, 성공 시 [비명을 지름] 후 사망",
            0, new[] { Species.Werewolf }, new Species[0]),
    };

    public static readonly FragmentDefinition<Facility>[] CounterFragments =
    {
        new FragmentDefinition<Facility>(Facility.Wolfsbane, "투구꽃 향수", 0, new[] { Species.Werewolf }, new Species[0]),
    };

    public static readonly FragmentDefinition<int>[] ExtraFragments =
    {
        new FragmentDefinition<int>(0, "없음", 0, new Species[0], new Species[0]),
    };

    public static readonly TooltipDefinition[] Tooltips =
    {
        new TooltipDefinition("[복도를 돌아다님]", "그 층의 사람들이 움직임을 느낍니다."),
        new TooltipDefinition("[비명을 지름]", "당신과 주변 8칸의 사람들이 비명을 듣습니다."),
        new TooltipDefinition("[문 앞을 서성임]", "방 앞에서 탐색합니다. 예민한 사람은 이를 알아차릴 수 있습니다."),
        new TooltipDefinition("불결함", "같은 층의 방에 냄새가 풍깁니다."),
        new TooltipDefinition("저녁형 인간", "낮에는 방에서 잘 나오지 않고, 밤에 [복도를 돌아다닙니다][복도를 돌아다님]."),
        new TooltipDefinition("겁쟁이", "같은 층 복도나 자기 방 앞에서 인기척이 느껴지면 [비명을 지릅니다][비명을 지름]."),
        new TooltipDefinition("대담함", "자기 방 앞에서 [서성이는][문 앞을 서성임] 사람이 있으면 문을 열어 얼굴을 확인합니다."),
        new TooltipDefinition("의심 많음", "낮에 로비에서 본 사람 중 한 명의 방 앞을 밤에 [서성입니다][문 앞을 서성임]."),
        new TooltipDefinition("목이 쉼", "[비명을 지를][비명을 지름] 수 없습니다."),
        new TooltipDefinition("과묵함", "밤에 일어난 일을 말하지 않습니다."),
        new TooltipDefinition("괴력", "인간일 경우 괴물에게 공격받아도 생존할 확률이 생깁니다. 괴물일 경우 문에 걸린 자물쇠를 무시합니다."),
        new TooltipDefinition("탈모", "매일 밤 방안에 털을 떨어트립니다."),
        new TooltipDefinition("녹슨 감금 자물쇠", "설치하면 그 방의 사람은 그날 밤 방에서 나오지 못하게 합니다."),
        new TooltipDefinition("녹슨 방범 자물쇠", "설치하면 그날 밤 동안 그 사람의 방에 들어가지 못하게 합니다."),
        new TooltipDefinition("투구꽃 향수", "늑대인간이 싫어하는 향으로 그 방을 향한 접근을 하루 막습니다."),
        new TooltipDefinition("늑대인간", "인간의 탈을 쓴 늑대. 그들은 더 이상 보름달을 필요로 하지 않습니다."),
    };

    public static string GetTooltip(string tag)
    {
        return Array.Find(Tooltips, t => t.Tag == tag)?.Description;
    }

    public static SpeciesDefinition GetSpecies(Species species)
    {
        return Array.Find(Monsters, m => m.Id == species);
    }

    public static string GetName(Species species)
    {
        return species == Species.Human ? "인간" : GetSpecies(species).Name;
    }

    public static FacilityDefinition GetFacility(Facility facility)
    {
        return Array.Find(Facilities, f => f.Id == facility);
    }

    public static string GetPhaseName(DayPhase phase)
    {
        switch (phase)
        {
            case DayPhase.Recruit:
                return "손님 모집";
            case DayPhase.Meeting:
                return "회의";
            default:
                return "밤 준비";
        }
    }

    public static string GetClueKindName(ClueKind kind)
    {
        switch (kind)
        {
            case ClueKind.Fur:
                return "[털]";
            default:
                return "단서";
        }
    }
}

public static class NightLogText
{
    public const string Scream = "누군가의 비명 소리가 들립니다...";
    public const string Rampage = "괴물의 분노가 느껴집니다...";
}

public static class ChatText
{
    public const string HeardHallway = "어제 복도에서 누가 돌아다니더라고.";
    public const string HeardScream = "어제 비명 소리가 들렸어.";
    // {0}: [문 앞을 서성임] 당사자
    public const string BoldSawLurker = "어제 {0}이 내 방 앞에 서 있었어. 바로 문 열고 무슨 짓이냐고 따졌지.";
    // {0}: 말하는 사람을 제외하고 [복도를 돌아다님]을 실행한 사람
    public const string SawRoamer = "어젯밤에 밖을 좀 둘러보는데.. {0}을 봤었어.";
    // {0}: [문 앞을 서성임] 당사자, {1}: 대상 방 주인
    public const string SawLurker = "어젯밤에 밖을 좀 둘러보는데.. {0}이 {1} 방 앞에 서 있더라고. 무슨 짓을 했는지는 모르겠다.";
    // {0}: [문 앞을 서성임] 대상 방 주인
    public const string LurkScream = "어제는 {0} 방문 앞에 좀 서 있었는데, 갑자기 비명을 지르더라니까.. 깜짝 놀랐어.";
    public const string HeardNothing = "어젯밤..? 글쎄, 아무 소리도 안 들리던데.";
    public const string SurvivedAttack = "죽을 뻔 했다고!! 어떤 놈이 내 방 문을 열고 쳐들어왔어! 다행히 목숨은 건졌지만.. 불안해서 미칠 것 같아..";

    // 회의 주장. 지목만. {0}=speaker, {1}=target / H1·털 제시는 {0}=target
    public const string ClaimRoamer = "{0}: 어젯밤에 말이죠… {1}이(가) 복도를 돌아다니는 걸 봤어요.";
    public const string ClaimHostFalse = "(주인) {0}… 아무래도 수상합니다. 증거도 있어요.";
    // {0}=target. 보유 [털] 단서를 제시하며 지목
    public const string ClaimFurPresent = "(주인) 이 털을 보십시오. {0}의 소행이 분명합니다.";
    // 어젯밤 일. 자기 방 [털] (탈모로 직접 떨어뜨린 경우는 말하지 않음)
    public const string TalkFoundFur = "제 방바닥에… 털이 떨어져 있었어요. 누가 들어온 거 아닌가요?";
}

public static class GameBalance
{
    public const int TargetDay = 15;
    public const int StartRooms = 4;
    public const float OutingChance = 0.7f;
    public const float NightOwlOutingChance = 0.2f;
    public const float MonsterChance = 0.15f;
    public const int MinMonsters = 1;
    public const int MaxMonsters = 1;
    public const int OwnerRoomLocks = 2;
    public const int DailyInterrogations = 1;
    public const int StartTrust = 8;
    public const int InspectCost = 1;
    public const int CallCost = 1;
    public const int MoveCost = 1;
    public const int VerifyCost = 2;
    public const int ExecuteCost = 6;
    public const int GuessReward = 3;
    public const int GuessPenalty = 2;
    public const int DefenseReward = 2;
    public const int QuietNightReward = 1;
    public const int DeathPenalty = 2;
    public const int InformantFirstDay = 2;
    public const int InformantInterval = 2;
    public const float StrongSurviveChance = 0.5f;
    // 살인마 피벗: 의심·회의·처형 (임시값)
    public const int MaxSuspicion = 5;
    public const int RejectDemandPenalty = 3;
    // 괴물 처형 시 순증가분. 처형 비용을 쓴 뒤 ExecuteCost+이 값만큼 돌려 순 +MonsterExecuteTrustGain이 된다.
    public const int MonsterExecuteTrustGain = 5;
    public const int DailyRefutes = 3;
    // 거짓 증거: 새 주장으로 올리기 / 반박에 쓰기 (임시)
    public const int FalseClaimCost = 2;
    public const int RefuteCost = 1;
}
