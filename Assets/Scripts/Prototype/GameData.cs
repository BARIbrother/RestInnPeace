using System;

public enum Species
{
    Human,
    Werewolf
}

public enum JobCategory
{
    Hunter,
    Search,
    Protect,
    Other
}

public enum Job
{
    Informant,
    Scout,
    DogHandler,
    Cleaner,
    Locksmith,
    Pharmacist,
    Butler
}

public enum NightOrder
{
    None,
    LockRoom,
    PerfumeRoom,
    WatchRoom,
    CleanRoom,
    RememberGuest,
    AskMonsterInfo
}

public enum Facility
{
    ConfineLock,
    GuardLock,
    Wolfsbane
}

// 늑대인간 회차 랜덤: 표적 슬롯 (타깃 위치 규칙)
public enum TargetRule
{
    SameFloorHuman,
    CrossAdjacent,
    AnyHuman
}

// 늑대인간 회차 랜덤: 예고 슬롯 (2단계 변형)
public enum OmenTrait
{
    TerritoryMark,
    DoorHunter,
    SilentApproach
}

public enum DayPhase
{
    Gather,
    Focus
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

public class JobDefinition
{
    public readonly Job Id;
    public readonly JobCategory Category;
    public readonly string Name;
    public readonly string Feature;
    public readonly string Request;

    public JobDefinition(Job id, JobCategory category, string name, string feature, string request)
    {
        Id = id;
        Category = category;
        Name = name;
        Feature = feature;
        Request = request;
    }
}

public class FacilityDefinition
{
    public readonly Facility Id;
    public readonly string Name;
    public readonly string Description;
    public readonly string Detail;
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

public class TargetRuleDefinition
{
    public readonly TargetRule Id;
    public readonly string Name;
    public readonly string Effect;
    public readonly string HunterHint;

    public TargetRuleDefinition(TargetRule id, string name, string effect, string hunterHint)
    {
        Id = id;
        Name = name;
        Effect = effect;
        HunterHint = hunterHint;
    }
}

public class OmenTraitDefinition
{
    public readonly OmenTrait Id;
    public readonly string Name;
    public readonly string Effect;
    public readonly string HunterHint;

    public OmenTraitDefinition(OmenTrait id, string name, string effect, string hunterHint)
    {
        Id = id;
        Name = name;
        Effect = effect;
        HunterHint = hunterHint;
    }
}

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

public static class GameData
{
    public static readonly SpeciesDefinition[] Monsters =
    {
        new SpeciesDefinition(Species.Werewolf, "늑대인간", "인간으로 위장.", 100),
    };

    public static readonly JobDefinition[] Jobs =
    {
        new JobDefinition(Job.Informant, JobCategory.Hunter, "정보원",
            "괴물 정보를 알고 있다.",
            "아직 알아내지 않은 몬스터 정보 1개를 알려 준다."),
        new JobDefinition(Job.Scout, JobCategory.Search, "정찰병",
            "밤에 자기 층에서 일어난 일들을 파악할 수 있다. 단 누가 했는지는 모른다.",
            "없음 (확정되면 아침마다 자기 층 요약을 보고한다)"),
        new JobDefinition(Job.DogHandler, JobCategory.Search, "경비견 조련사",
            "자기 방 앞 [문 앞을 서성임]만 정확히 감지한다.",
            "지정한 방 앞을 하룻밤 감시한다."),
        new JobDefinition(Job.Cleaner, JobCategory.Search, "청소부",
            "주인 조사에 안 잡히는 약한 흔적([발자국] 등)을 자기 층에서만 본다.",
            "지정한 방을 청소해 단서를 제거하거나 회수한다."),
        new JobDefinition(Job.Locksmith, JobCategory.Protect, "열쇠 수리공",
            "매일 밤 자기 방문을 잠근다.",
            "지정한 방 하나의 방문을 잠근다."),
        new JobDefinition(Job.Pharmacist, JobCategory.Protect, "약제사",
            "자기 방에 강한 냄새를 퍼뜨린다. 같은 층 [냄새]를 덮어 읽기 어렵게 한다.",
            "지정한 방에 하룻밤 향을 뿌려 괴물 표적에서 피하게 한다."),
        new JobDefinition(Job.Butler, JobCategory.Other, "집사",
            "사람의 밤 행동을 기억할 수 있다.",
            "낮에 사람 1명을 기억시키면 밤에 그 사람이 움직였는지 보고한다."),
    };

    public static readonly FacilityDefinition[] Facilities =
    {
        new FacilityDefinition(Facility.ConfineLock, "녹슨 감금 자물쇠", "하루 동안 밤에 그 방에서 나가는 것 방지",
            "괴물의 패턴 진행과 손님의 밤 외출을 그날 밤 막는다.\n방 안에서 일어나는 일(털 등)은 막지 못한다.", 1),
        new FacilityDefinition(Facility.GuardLock, "녹슨 방범 자물쇠", "하루 동안 밤에 그 방을 향한 공격 시도 방지",
            "공격을 막으면 아침에 [문을 긁은 자국]이 발견된다.", 1),
        new FacilityDefinition(Facility.Wolfsbane, "투구꽃 향수", "방에 설치하면 그 방 손님을 향한 패턴을 하루 막아 줌",
            "이번 회차의 효과적인 대응이 투구꽃 향수일 때 통한다.\n공격 직전에 막았다면 아침에 [문을 긁은 자국]이 발견된다.", 2),
    };

    public static readonly ClueDefinition[] Clues =
    {
        new ClueDefinition("[복도를 돌아다님]",
            "주인은 직접 알 수 없다. 같은 층 손님의 '어젯밤 일'이나 정찰병 보고로 알 수 있다.",
            "괴물의 패턴, 손님의 밤 외출"),
        new ClueDefinition("[문 앞을 서성임]",
            "주인은 직접 알 수 없다. 경비견 조련사·증언으로 알 수 있다.",
            "괴물의 패턴"),
        new ClueDefinition("[비명을 지름]",
            "밤 기록에 '비명 소리'가 남는다. 주변 손님은 '어젯밤 일'에서 말할 수 있다.",
            "괴물의 공격"),
        new ClueDefinition("[털]",
            "방 조사로 발견하면 치워져 사라진다. 잠긴 방은 조사할 수 없다. 하루 한 방만 직접 조사한다.",
            "괴물의 패턴"),
        new ClueDefinition("[발자국]",
            "주인 조사에는 안 잡힌다. 청소부가 자기 층에서만 본다.",
            "복도를 돌아다닌 흔적"),
        new ClueDefinition("[냄새]",
            "방 조사로 악취를 발견할 수 있다. 약제사 층에서는 덮일 수 있다.",
            "약제사, 기타"),
        new ClueDefinition("[문을 긁은 자국]",
            "아침 보고에 나온다. 그 방을 노린 괴물의 시도가 문 잠금이나 약제사 향에 막혔다는 뜻이다.",
            "문 잠금, 약제사 향"),
        new ClueDefinition("[시체 발견]",
            "아침 보고에 나온다. 괴물의 공격이 성공한 방이다.",
            "괴물의 공격"),
    };

    public static readonly TargetRuleDefinition[] TargetRules =
    {
        new TargetRuleDefinition(TargetRule.SameFloorHuman, "같은 층 집착",
            "같은 층 무작위 인간을 타깃으로 삼는다.",
            "같은 층의 인간을 노린다"),
        new TargetRuleDefinition(TargetRule.CrossAdjacent, "인접 추적",
            "상하좌우 1칸 방의 무작위 인간을 타깃으로 삼는다.",
            "이웃한 방의 인간을 노린다"),
        new TargetRuleDefinition(TargetRule.AnyHuman, "배회 사냥",
            "여관 전체에서 무작위 인간을 타깃으로 삼는다.",
            "여관의 인간을 가리지 않고 노린다"),
    };

    public static readonly OmenTraitDefinition[] OmenTraits =
    {
        new OmenTraitDefinition(OmenTrait.TerritoryMark, "영역 표시",
            "2단계: 타깃 방에 [털]",
            "털을 남기는 짐승이다"),
        new OmenTraitDefinition(OmenTrait.DoorHunter, "문 앞의 사냥꾼",
            "2단계: 타깃 방 [문 앞을 서성임]",
            "문 앞에서 서성인다"),
        new OmenTraitDefinition(OmenTrait.SilentApproach, "침묵의 접근",
            "2단계: 단서 없음. 막힌 공격만 [문을 긁은 자국]",
            "다가와도 흔적을 거의 남기지 않는다"),
    };

    // Docs/Guest/NameTable.md 와 같은 순서. 회차마다 손님 수만큼 중복 없이 뽑는다.
    public static readonly string[] GuestNames =
    {
        "에드거", "아서", "빅터", "월터",
        "해럴드", "올리버", "줄리안", "펠릭스",
        "콘래드", "빈센트", "시어도어", "마일스",
        "에이드리언", "라이어널", "기디언", "로언",
        "트리스탄", "매그너스", "바질", "휴고",
        "엘레너", "클라라", "바이올렛", "아이리스",
        "마거릿", "에블린", "아가사", "베아트리스",
        "실비아", "헬레나", "다프네", "오필리아",
        "조세핀", "애들레이드", "릴리안", "코델리아",
        "아멜리아", "이디스", "플로렌스", "그레이스",
        "노라", "헤이즐", "루비", "펄",
        "아이비", "로즈", "앨리스", "마사",
    };

    public static readonly TooltipDefinition[] Tooltips =
    {
        new TooltipDefinition("[복도를 돌아다님]", "그 층의 사람들이 움직임을 느낀다."),
        new TooltipDefinition("[비명을 지름]", "당신과 주변 8칸의 사람들이 비명을 듣는다."),
        new TooltipDefinition("[문 앞을 서성임]", "방 앞에서 탐색한다. 예민한 사람은 이를 알아차릴 수 있다."),
        new TooltipDefinition("[털]", "방에 떨어진 짐승의 털이다. 방 조사나 청소부로 발견할 수 있다."),
        new TooltipDefinition("[발자국]", "약한 흔적이다. 청소부만 자기 층에서 본다."),
        new TooltipDefinition("정보원", "괴물 정보를 알려 줄 수 있는 헌터 직업이다."),
        new TooltipDefinition("정찰병", "자기 층의 밤 사건 종류를 파악한다."),
        new TooltipDefinition("경비견 조련사", "문 앞을 서성이는 사람을 감지한다."),
        new TooltipDefinition("청소부", "약한 흔적을 보고 방을 청소할 수 있다."),
        new TooltipDefinition("열쇠 수리공", "방문을 잠글 수 있다."),
        new TooltipDefinition("약제사", "향으로 괴물의 표적을 빗나가게 할 수 있다."),
        new TooltipDefinition("집사", "지정한 사람이 밤에 움직였는지 기억한다."),
        new TooltipDefinition("녹슨 감금 자물쇠", "설치하면 그 방의 사람은 그날 밤 방에서 나오지 못하게 한다."),
        new TooltipDefinition("녹슨 방범 자물쇠", "설치하면 그날 밤 동안 그 사람의 방에 들어가지 못하게 한다."),
        new TooltipDefinition("투구꽃 향수", "늑대인간이 싫어하는 향으로 그 방을 향한 접근을 하루 막는다."),
        new TooltipDefinition("늑대인간", "인간의 탈을 쓴 늑대."),
        new TooltipDefinition("영역 표시", "타깃 방에 [털]을 남긴다."),
        new TooltipDefinition("문 앞의 사냥꾼", "타깃 방 앞을 서성인다. [문 앞을 서성임]."),
        new TooltipDefinition("침묵의 접근", "준비 단계에 흔적이 거의 없다. 막히면 [문을 긁은 자국]만 남는다."),
        new TooltipDefinition("같은 층 집착", "같은 층의 인간을 노린다."),
        new TooltipDefinition("인접 추적", "상하좌우 이웃 방의 인간을 노린다."),
        new TooltipDefinition("배회 사냥", "여관 전체에서 인간을 고른다."),
    };

    public static TargetRuleDefinition GetTargetRule(TargetRule rule)
    {
        return Array.Find(TargetRules, t => t.Id == rule);
    }

    public static OmenTraitDefinition GetOmen(OmenTrait omen)
    {
        return Array.Find(OmenTraits, o => o.Id == omen);
    }

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

    public static JobDefinition GetJob(Job job)
    {
        return Array.Find(Jobs, j => j.Id == job);
    }

    public static string GetCategoryName(JobCategory category)
    {
        switch (category)
        {
            case JobCategory.Hunter:
                return "헌터";
            case JobCategory.Search:
                return "수색";
            case JobCategory.Protect:
                return "보호";
            default:
                return "기타";
        }
    }

    public static FacilityDefinition GetFacility(Facility facility)
    {
        return Array.Find(Facilities, f => f.Id == facility);
    }

    public static string GetPhaseName(DayPhase phase)
    {
        return phase == DayPhase.Gather ? "소집·조사" : "집중 심문";
    }
}

public static class NightLogText
{
    public const string Scream = "누군가의 비명 소리가 들린다...";
    public const string Rampage = "괴물의 분노가 느껴진다...";
}

public static class ChatText
{
    public const string HeardHallway = "어제 복도에서 누가 돌아다니더라고.";
    public const string HeardScream = "어제 비명 소리가 들렸어.";
    public const string BoldSawLurker = "어제 {0}이 내 방 앞에 서 있었어. 바로 문 열고 무슨 짓이냐고 따졌지.";
    public const string SawRoamer = "어젯밤에 밖을 좀 둘러보는데.. {0}을 봤었어.";
    public const string SawLurker = "어젯밤에 밖을 좀 둘러보는데.. {0}이 {1} 방 앞에 서 있더라고. 무슨 짓을 했는지는 모르겠다.";
    public const string LurkScream = "어제는 {0} 방문 앞에 좀 서 있었는데, 갑자기 비명을 지르더라니까.. 깜짝 놀랐어.";
    public const string HeardNothing = "어젯밤..? 글쎄, 아무 소리도 안 들리던데.";
    public const string SurvivedAttack = "죽을 뻔 했다고!! 어떤 놈이 내 방 문을 열고 쳐들어왔어! 다행히 목숨은 건졌지만.. 불안해서 미칠 것 같아..";
}

public enum Difficulty
{
    VeryEasy,
    Normal,
    Hard
}

public readonly struct DifficultyConfig
{
    public readonly Difficulty Id;
    public readonly string Name;
    public readonly string Summary;
    public readonly int Floors;
    public readonly int RoomsPerFloor;
    public readonly int MinMonsters;
    public readonly int MaxMonsters;

    public int RoomCount => Floors * RoomsPerFloor;

    public DifficultyConfig(Difficulty id, string name, string summary, int floors, int roomsPerFloor, int minMonsters, int maxMonsters)
    {
        Id = id;
        Name = name;
        Summary = summary;
        Floors = floors;
        RoomsPerFloor = roomsPerFloor;
        MinMonsters = minMonsters;
        MaxMonsters = maxMonsters;
    }
}

public static class GameBalance
{
    public const int TargetDay = 15;
    public const float OutingChance = 0.7f;
    public const float MonsterChance = 0.15f;
    public const int OwnerRoomLocks = 2;
    public const int DailyJobActions = 1;
    public const int DailyExecutions = 1;
    // 괴물이 직업 주장을 거짓으로 할 확률 (임시)
    public const float MonsterLieChance = 0.5f;
    public const int MinHunter = 1;
    public const int MinSearch = 1;
    public const int MinProtect = 1;
    public const int MinOther = 0;

    public static readonly DifficultyConfig[] Difficulties =
    {
        new DifficultyConfig(Difficulty.VeryEasy, "아주 쉬움", "객실 2×2, 괴물 1명", 2, 2, 1, 1),
        new DifficultyConfig(Difficulty.Normal, "보통", "2층·층당 객실 3개, 괴물 1명", 2, 3, 1, 1),
        new DifficultyConfig(Difficulty.Hard, "어려움", "객실 3×3, 괴물 2명", 3, 3, 2, 2)
    };

    public static DifficultyConfig Get(Difficulty difficulty)
    {
        foreach (DifficultyConfig config in Difficulties)
        {
            if (config.Id == difficulty)
            {
                return config;
            }
        }

        return Difficulties[0];
    }
}
