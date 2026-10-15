using System;
using System.Collections.Generic;
using System.Linq;

public enum GameResult
{
    Playing,
    Won,
    Lost
}

public class InnGame
{
    public const int OwnerFloor = 1;
    public const int OwnerColumn = 0;

    static readonly (int floor, int column)[] RoomSlots =
    {
        (1, 1), (1, 2), (2, 1), (2, 2)
    };

    readonly Random random = new Random();
    readonly List<MonsterState> monsterStates = new List<MonsterState>();
    readonly List<string> hiddenFragments = new List<string>();
    int nextGuestId = 1;
    int nextCheckInOrder = 1;
    TargetRule targetRule;
    Facility counterFacility;
    int firstStageRest;
    bool lurkAtSecondStage;

    public readonly List<Room> Rooms = new List<Room>();
    public readonly List<string> RevealedFragments = new List<string>();
    public readonly List<string> Log = new List<string>();
    public readonly List<string> NightLog = new List<string>();
    public readonly List<string> MorningReport = new List<string>();
    public string Memo = "";

    public Species RunSpecies { get; private set; }
    public int Day { get; private set; }
    public DayPhase Phase { get; private set; }
    public int Interrogations { get; private set; }
    public int OwnerLocks { get; private set; }
    public int Trust { get; private set; }
    public GameResult Result { get; private set; }
    public string ResultMessage { get; private set; }

    public bool CanEndDay => Result == GameResult.Playing && Phase == DayPhase.Prepare;
    public bool CanAdvancePhase => Result == GameResult.Playing && Phase != DayPhase.Prepare;

    public InnGame()
    {
        Day = 1;
        OwnerLocks = GameBalance.OwnerRoomLocks;
        Trust = GameBalance.StartTrust;
        Result = GameResult.Playing;

        foreach ((int floor, int column) slot in RoomSlots.Take(GameBalance.StartRooms))
        {
            Rooms.Add(new Room { Floor = slot.floor, Column = slot.column, Number = slot.floor * 100 + slot.column });
        }

        AssignFragments();

        List<Guest> starters = CreateGuests(Rooms.Count);
        for (int i = 0; i < Rooms.Count; i++)
        {
            starters[i].CheckInOrder = nextCheckInOrder++;
            Rooms[i].Guest = starters[i];
        }

        foreach (Guest monster in starters.Where(g => g.IsMonster))
        {
            monsterStates.Add(new MonsterState { Monster = monster });
        }

        Report($"이번 회차에 등장하는 괴물: {GameData.GetName(RunSpecies)}");
        StartDay();
    }

    #region 낮 행동

    public void AdvancePhase()
    {
        if (!CanAdvancePhase)
        {
            return;
        }

        Phase++;
        AddLog($"{Day}일차 낮: {GameData.GetPhaseName(Phase)} 단계로 넘어갔다.");
    }

    // 대화는 '어젯밤 일'만 있으므로 밤을 한 번도 지나지 않은 1일차에는 할 수 없다.
    public bool CanTalk(Room room)
    {
        return Phase == DayPhase.Talk && room.Guest != null && room.Guest.IsOut && !room.Guest.TalkedToday && Day > 1;
    }

    public void Talk(Room room)
    {
        if (!CanTalk(room))
        {
            return;
        }

        Guest guest = room.Guest;
        guest.TalkedToday = true;
        AddNote(guest, $"{Day}일차 어젯밤 일: \"{GetLastNightLine(guest)}\"");
    }

    public bool CanLureOut(Room room)
    {
        return Phase == DayPhase.Gather && room.Guest != null && !room.Guest.IsOut && Trust >= GameBalance.CallCost;
    }

    public void LureOut(Room room)
    {
        if (!CanLureOut(room) || !SpendTrust(GameBalance.CallCost))
        {
            return;
        }

        Guest guest = room.Guest;
        guest.IsOut = true;
        AddNote(guest, $"{Day}일차 로비로 부르기: 로비로 나왔다.");
    }

    public bool CanInspect(Room room)
    {
        return Phase == DayPhase.Gather && Trust >= GameBalance.InspectCost && (room.Guest == null || room.Guest.IsOut);
    }

    public void Inspect(Room room)
    {
        if (!CanInspect(room) || !SpendTrust(GameBalance.InspectCost))
        {
            return;
        }

        List<string> clues = new List<string>();
        if (room.HasFur)
        {
            clues.Add("방바닥에 털이 떨어져 있다.");
            room.HasFur = false;
        }

        if (room.HasSmell)
        {
            clues.Add("방 안에 악취가 배어 있다.");
        }

        string result = clues.Count == 0 ? "특별한 흔적이 없다." : string.Join(" ", clues);
        if (room.Guest != null)
        {
            AddNote(room.Guest, $"{Day}일차 {room.DisplayName} 조사: {result}");
        }
        else
        {
            AddLog($"{Day}일차 {room.DisplayName} 조사: {result}");
        }
    }

    public bool CanMove(Room from)
    {
        return Phase == DayPhase.Prepare && from.Guest != null && Trust >= GameBalance.MoveCost;
    }

    public bool Move(Room from, Room to)
    {
        if (!CanMove(from) || from == to || !SpendTrust(GameBalance.MoveCost))
        {
            return false;
        }

        Guest moving = from.Guest;
        Guest other = to.Guest;
        from.Guest = other;
        to.Guest = moving;
        AddLog(other == null
            ? $"{moving.DisplayName}을(를) {from.DisplayName}에서 {to.DisplayName}(으)로 옮겼다."
            : $"{moving.DisplayName}({from.DisplayName})과(와) {other.DisplayName}({to.DisplayName})의 방을 바꿨다.");
        return true;
    }

    public bool CanInstall(Room room, Facility facility)
    {
        return Phase == DayPhase.Prepare && room.Guest != null && Trust >= GameData.GetFacility(facility).Price && !room.Installed.Contains(facility);
    }

    public void Install(Room room, Facility facility)
    {
        if (!CanInstall(room, facility))
        {
            return;
        }

        FacilityDefinition definition = GameData.GetFacility(facility);
        SpendTrust(definition.Price);
        room.Installed.Add(facility);
        AddLog($"신뢰 {definition.Price}을(를) 써서 {definition.Name}을(를) {room.DisplayName}에 설치했다.");
    }

    public bool CanInterrogate(Room room)
    {
        return Phase == DayPhase.Talk && room.Guest != null && room.Guest.IsOut && !room.Guest.TraitKnown && Interrogations > 0;
    }

    // 집중 심문: 특성을 맞히면 확정되고 신뢰가 오르며, 틀리면 신뢰가 내려간다.
    public void GuessTrait(Room room, Trait trait)
    {
        if (!CanInterrogate(room))
        {
            return;
        }

        Interrogations--;
        Guest guest = room.Guest;
        string name = GameData.Traits.First(t => t.Id == trait).Name;
        if (guest.Trait == trait)
        {
            guest.TraitKnown = true;
            ChangeTrust(GameBalance.GuessReward);
            AddNote(guest, $"{Day}일차 집중 심문: 특성이 {name}임을 맞혔다. 신뢰 +{GameBalance.GuessReward}");
        }
        else
        {
            ChangeTrust(-GameBalance.GuessPenalty);
            AddNote(guest, $"{Day}일차 집중 심문: 특성이 {name}이(가) 아니었다. 신뢰 -{GameBalance.GuessPenalty}");
        }
    }

    public bool CanVerifyTrait(Room room)
    {
        return CanInterrogate(room) && Trust >= GameBalance.VerifyCost;
    }

    // 집중 심문: 신뢰를 내고 고른 특성을 가졌는지 아닌지만 판정한다. 정답을 알려 주지 않으며 맞히기 보상·벌점은 없다.
    public void VerifyTrait(Room room, Trait trait)
    {
        if (!CanVerifyTrait(room) || !SpendTrust(GameBalance.VerifyCost))
        {
            return;
        }

        Interrogations--;
        Guest guest = room.Guest;
        string name = GameData.Traits.First(t => t.Id == trait).Name;
        if (guest.Trait == trait)
        {
            guest.TraitKnown = true;
            AddNote(guest, $"{Day}일차 집중 심문: 검증 결과 특성이 {name}이(가) 맞다.");
        }
        else
        {
            AddNote(guest, $"{Day}일차 집중 심문: 검증 결과 특성이 {name}이(가) 아니다.");
        }
    }

    public bool CanExecute(Room room)
    {
        return Phase == DayPhase.Prepare && room.Guest != null && Trust >= GameBalance.ExecuteCost;
    }

    // 괴물이면 쓴 신뢰를 돌려받는다. 인간이면 신뢰를 잃고 그 인간이 사망한다.
    public void Execute(Room room)
    {
        if (!CanExecute(room) || !SpendTrust(GameBalance.ExecuteCost))
        {
            return;
        }

        Guest guest = room.Guest;
        room.Guest = null;
        if (guest.IsMonster)
        {
            ChangeTrust(GameBalance.ExecuteCost);
            Report($"{guest.DisplayName}을(를) 처형했다. 정체는 {GameData.GetName(guest.Species)}이었다. 쓴 신뢰를 돌려받았다.");
        }
        else
        {
            Report($"{guest.DisplayName}을(를) 처형했다. 정체는 인간이었다...");
        }

        if (!Rooms.Any(r => r.Guest != null && r.Guest.IsMonster))
        {
            Result = GameResult.Won;
            ResultMessage = "괴물을 모두 처형했다.";
        }
        else if (!Rooms.Any(r => r.Guest != null && !r.Guest.IsMonster))
        {
            Result = GameResult.Lost;
            ResultMessage = "여관에 살아 있는 인간이 없다.";
        }
    }

    #endregion

    #region 밤·아침

    public void EndDay()
    {
        if (!CanEndDay)
        {
            return;
        }

        NightLog.Clear();
        MorningReport.Clear();
        int guestsBefore = Rooms.Count(r => r.Guest != null);
        RunNight();

        if (Rooms.Count(r => r.Guest != null) == guestsBefore)
        {
            ChangeTrust(GameBalance.QuietNightReward);
            Report($"사망자 없이 아침을 맞았다. 신뢰 +{GameBalance.QuietNightReward}");
        }

        if (!Rooms.Any(r => r.Guest != null && !r.Guest.IsMonster))
        {
            Result = GameResult.Lost;
            ResultMessage = "여관에 살아 있는 인간이 없다.";
            return;
        }

        if (Day >= GameBalance.TargetDay)
        {
            Result = GameResult.Won;
            ResultMessage = $"{GameBalance.TargetDay}일을 버텼다.";
            return;
        }

        Day++;
        StartDay();
    }

    void RunNight()
    {
        // [복도를 돌아다님]·[문 앞을 서성임]은 주인의 밤 로그에 남지 않고, 손님의 '어젯밤 일'로만 드러난다.
        List<(Guest actor, int floor)> roamers = new List<(Guest actor, int floor)>();
        List<(Guest actor, Room room, Guest owner)> lurkers = new List<(Guest actor, Room room, Guest owner)>();
        List<Room> screamRooms = new List<Room>();
        List<Guest> rampageMonsters = new List<Guest>();
        List<Guest> lobbyGuests = Rooms.Where(r => r.Guest != null && r.Guest.IsOut).Select(r => r.Guest).ToList();

        foreach (Room room in Rooms)
        {
            room.HasSmell = false;
            if (room.Guest != null)
            {
                room.Guest.IsOut = false;
                room.Guest.HeardHallway = false;
                room.Guest.HeardScream = false;
                room.Guest.SurvivedAttack = false;
                room.Guest.NightSightings.Clear();
            }
        }

        foreach (Room room in Rooms.Where(r => r.Guest != null).ToList())
        {
            switch (room.Guest.Trait)
            {
                case Trait.NightOwl:
                    if (!room.Installed.Contains(Facility.ConfineLock))
                    {
                        roamers.Add((room.Guest, room.Floor));
                    }
                    break;
                case Trait.Suspicious:
                    List<Room> suspects = Rooms.Where(r => r.Guest != null && r.Guest != room.Guest && lobbyGuests.Contains(r.Guest)).ToList();
                    if (!room.Installed.Contains(Facility.ConfineLock) && suspects.Count > 0)
                    {
                        Room suspect = suspects[random.Next(suspects.Count)];
                        lurkers.Add((room.Guest, suspect, suspect.Guest));
                    }
                    break;
                case Trait.Balding:
                    room.HasFur = true;
                    break;
                case Trait.Unclean:
                    foreach (Room sameFloor in Rooms.Where(r => r.Floor == room.Floor))
                    {
                        sameFloor.HasSmell = true;
                    }
                    break;
            }
        }

        IEnumerable<MonsterState> actors = monsterStates
            .Where(s => FindRoom(s.Monster) != null)
            .OrderByDescending(s => GameData.GetSpecies(s.Monster.Species).NightSpeed)
            .ThenBy(s => s.Monster.CheckInOrder)
            .ToList();
        foreach (MonsterState state in actors)
        {
            Room room = FindRoom(state.Monster);
            if (room.Installed.Contains(Facility.ConfineLock))
            {
                continue;
            }

            RunWerewolf(state, room, roamers, lurkers, screamRooms, rampageMonsters);
        }

        // 겁쟁이는 괴물이 움직인 결과에 반응하므로 괴물 행동이 끝난 뒤에 판정한다.
        foreach (Room room in Rooms.Where(r => r.Guest != null && r.Guest.Trait == Trait.Coward))
        {
            bool frightened = roamers.Any(r => r.floor == room.Floor && r.actor != room.Guest) || lurkers.Any(l => l.room == room);
            if (frightened && !screamRooms.Contains(room))
            {
                screamRooms.Add(room);
            }
        }

        foreach (Room room in Rooms.Where(r => r.Guest != null))
        {
            Guest guest = room.Guest;
            if (guest.Trait == Trait.DeepSleeper)
            {
                continue;
            }

            guest.HeardHallway = roamers.Any(r => r.floor == room.Floor && r.actor != guest);
            guest.HeardScream = screamRooms.Any(s => IsAround(s, room));
            if (guest.Trait == Trait.Bold)
            {
                foreach ((Guest actor, Room room, Guest owner) lurk in lurkers.Where(l => l.room == room))
                {
                    guest.NightSightings.Add(string.Format(ChatText.BoldSawLurker, lurk.actor.DisplayName));
                }
            }
        }

        // 밤에 밖에 나와 있던 손님이 같은 층에서 본 것
        List<(Guest actor, int floor)> outside = roamers.Concat(lurkers.Select(l => (l.actor, l.room.Floor))).ToList();
        foreach ((Guest actor, int floor) watcher in outside)
        {
            foreach ((Guest actor, int floor) roamer in roamers.Where(r => r.floor == watcher.floor && r.actor != watcher.actor))
            {
                watcher.actor.NightSightings.Add(string.Format(ChatText.SawRoamer, roamer.actor.DisplayName));
            }
        }

        foreach ((Guest actor, int floor) roamer in roamers)
        {
            foreach ((Guest actor, Room room, Guest owner) lurk in lurkers.Where(l => l.room.Floor == roamer.floor && l.actor != roamer.actor))
            {
                roamer.actor.NightSightings.Add(string.Format(ChatText.SawLurker, lurk.actor.DisplayName, lurk.owner.DisplayName));
            }
        }

        foreach ((Guest actor, Room room, Guest owner) lurk in lurkers.Where(l => screamRooms.Contains(l.room)))
        {
            lurk.actor.NightSightings.Add(string.Format(ChatText.LurkScream, lurk.owner.DisplayName));
        }

        NightLog.AddRange(screamRooms.Select(_ => NightLogText.Scream));
        NightLog.AddRange(rampageMonsters.Select(_ => NightLogText.Rampage));
        Shuffle(NightLog);
        foreach (string line in NightLog)
        {
            AddLog($"{Day}일차 밤: {line}");
        }
    }

    void RunWerewolf(MonsterState state, Room room, List<(Guest actor, int floor)> roamers, List<(Guest actor, Room room, Guest owner)> lurkers,
                     List<Room> screamRooms, List<Guest> rampageMonsters)
    {
        Room targetRoom = state.Target != null ? FindRoom(state.Target) : null;
        if (state.Target != null && targetRoom == null)
        {
            state.Stage = 1;
            state.Rest = 0;
            state.Attacked = false;
            state.Target = null;
        }
        else if (state.Attacked)
        {
            state.Attacked = false;
            if (state.Rampage || IsInTargetRange(room, targetRoom))
            {
                state.Stage = 3;
                state.Rest = 0;
            }
            else
            {
                state.Stage = 1;
                state.Rest = 1;
                state.Target = null;
            }
        }

        if (state.Rest > 0)
        {
            state.Rest--;
            return;
        }

        if (state.Stage > 1 && targetRoom.Installed.Contains(counterFacility))
        {
            if (state.Stage == 3)
            {
                ChangeTrust(GameBalance.DefenseReward);
                Report($"{targetRoom.DisplayName} 문에 무언가 긁고 간 자국이 있다. 신뢰 +{GameBalance.DefenseReward}");
            }

            return;
        }

        switch (state.Stage)
        {
            case 1:
                List<Room> humans = Rooms.Where(r => r != room && r.Guest != null && !r.Guest.IsMonster).ToList();
                state.Rampage = !humans.Any(r => IsInTargetRange(room, r));
                List<Room> candidates = humans.Where(r => (state.Rampage || IsInTargetRange(room, r)) &&
                                                          !r.Installed.Contains(counterFacility)).ToList();
                if (candidates.Count == 0)
                {
                    return;
                }

                Room picked = candidates[random.Next(candidates.Count)];
                state.Target = picked.Guest;
                if (state.Rampage)
                {
                    rampageMonsters.Add(state.Monster);
                }
                roamers.Add((state.Monster, picked.Floor));
                state.Stage = 2;
                state.Rest = firstStageRest;
                break;

            case 2:
                if (lurkAtSecondStage)
                {
                    lurkers.Add((state.Monster, targetRoom, targetRoom.Guest));
                }
                else
                {
                    targetRoom.HasFur = true;
                }
                state.Stage = 3;
                state.Rest = 1;
                break;

            case 3:
                state.Attacked = true;
                if (targetRoom.Installed.Contains(Facility.GuardLock) && state.Monster.Trait != Trait.Strong)
                {
                    ChangeTrust(GameBalance.DefenseReward);
                    Report($"{targetRoom.DisplayName} 문에 무언가 긁고 간 자국이 있다. 신뢰 +{GameBalance.DefenseReward}");
                }
                else
                {
                    if (state.Target.Trait != Trait.Hoarse)
                    {
                        screamRooms.Add(targetRoom);
                    }

                    if (state.Target.Trait == Trait.Strong && random.NextDouble() < GameBalance.StrongSurviveChance)
                    {
                        state.Target.SurvivedAttack = true;
                    }
                    else
                    {
                        targetRoom.Guest = null;
                        ChangeTrust(-GameBalance.DeathPenalty);
                        Report($"{targetRoom.DisplayName}에서 {state.Target.DisplayName}의 시체가 발견되었다. 신뢰 -{GameBalance.DeathPenalty}");
                    }
                }
                break;
        }
    }

    bool IsInTargetRange(Room from, Room to)
    {
        switch (targetRule)
        {
            case TargetRule.SameFloorHuman:
                return to.Floor == from.Floor;
            case TargetRule.CrossAdjacent:
                return IsCrossAdjacent(from, to);
            default:
                return true;
        }
    }

    static bool IsCrossAdjacent(Room a, Room b)
    {
        return Math.Abs(a.Floor - b.Floor) + Math.Abs(a.Column - b.Column) == 1;
    }

    void StartDay()
    {
        Phase = DayPhase.Gather;
        Interrogations = GameBalance.DailyInterrogations;

        foreach (Room room in Rooms)
        {
            room.Installed.Clear();
            if (room.Guest != null)
            {
                float outingChance = room.Guest.Trait == Trait.NightOwl ? GameBalance.NightOwlOutingChance : GameBalance.OutingChance;
                room.Guest.IsOut = random.NextDouble() < outingChance;
                room.Guest.TalkedToday = false;
                room.Guest.Notes.Clear();
            }
        }

        if (Day >= GameBalance.InformantFirstDay &&
            (Day - GameBalance.InformantFirstDay) % GameBalance.InformantInterval == 0 &&
            hiddenFragments.Count > 0)
        {
            string fragment = hiddenFragments[random.Next(hiddenFragments.Count)];
            hiddenFragments.Remove(fragment);
            RevealedFragments.Add(fragment);
            Report($"정보원: {GameData.GetName(RunSpecies)}의 {fragment}");
        }

        AddLog($"{Day}일차 낮이 밝았다.");
    }

    #endregion

    #region 내부 유틸

    void AssignFragments()
    {
        RunSpecies = GameData.Monsters[random.Next(GameData.Monsters.Length)].Id;

        FragmentDefinition<TargetRule> target = PickFragment(GameData.TargetFragments);
        FragmentDefinition<(int firstRest, bool lurk)> process = PickFragment(GameData.ProcessFragments);
        FragmentDefinition<Facility> counter = PickFragment(GameData.CounterFragments);
        FragmentDefinition<int> extra = PickFragment(GameData.ExtraFragments);

        targetRule = target.Id;
        counterFacility = counter.Id;
        firstStageRest = process.Id.firstRest;
        lurkAtSecondStage = process.Id.lurk;
        hiddenFragments.Add($"타깃: {target.Description}");
        hiddenFragments.Add($"공격 과정: {process.Description}");
        hiddenFragments.Add($"효과적인 대응: {counter.Description}");
        if (extra.Description != "없음")
        {
            hiddenFragments.Add($"기타 변수: {extra.Description}");
        }
    }

    // 가중치가 모두 비어 있으면 후보끼리 균등 확률이다.
    FragmentDefinition<T> PickFragment<T>(FragmentDefinition<T>[] table)
    {
        List<FragmentDefinition<T>> candidates = table.Where(f => f.IsAvailableFor(RunSpecies)).ToList();
        int total = candidates.Sum(f => f.Weight);
        if (total == 0)
        {
            return candidates[random.Next(candidates.Count)];
        }

        int roll = random.Next(total);
        foreach (FragmentDefinition<T> fragment in candidates)
        {
            if (roll < fragment.Weight)
            {
                return fragment;
            }

            roll -= fragment.Weight;
        }

        return candidates[candidates.Count - 1];
    }

    List<Guest> CreateGuests(int count)
    {
        int monsters = Rooms.Count(r => r.Guest != null && r.Guest.IsMonster);
        int required = Math.Max(0, GameBalance.MinMonsters - monsters);
        List<Guest> guests = new List<Guest>();

        for (int i = 0; i < count; i++)
        {
            bool isMonster = i < required ||
                             (monsters < GameBalance.MaxMonsters && random.NextDouble() < GameBalance.MonsterChance);
            if (isMonster)
            {
                monsters++;
            }

            guests.Add(new Guest
            {
                Id = nextGuestId++,
                Species = isMonster ? RunSpecies : Species.Human,
                Trait = GameData.Traits[random.Next(GameData.Traits.Length)].Id
            });
        }

        Shuffle(guests);
        return guests;
    }

    string GetLastNightLine(Guest guest)
    {
        if (guest.Trait == Trait.Taciturn)
        {
            return ChatText.HeardNothing;
        }

        if (guest.SurvivedAttack)
        {
            return ChatText.SurvivedAttack;
        }

        if (guest.Trait == Trait.DeepSleeper)
        {
            return ChatText.HeardNothing;
        }

        List<string> lines = guest.NightSightings.Distinct().ToList();
        if (guest.HeardHallway)
        {
            lines.Add(ChatText.HeardHallway);
        }

        if (guest.HeardScream)
        {
            lines.Add(ChatText.HeardScream);
        }

        return lines.Count == 0 ? ChatText.HeardNothing : lines[random.Next(lines.Count)];
    }

    Room FindRoom(Guest guest)
    {
        return Rooms.FirstOrDefault(r => r.Guest == guest);
    }

    static bool IsAround(Room a, Room b)
    {
        return a != b && Math.Abs(a.Floor - b.Floor) <= 1 && Math.Abs(a.Column - b.Column) <= 1;
    }

    bool SpendTrust(int cost)
    {
        if (Trust < cost)
        {
            AddLog("신뢰가 부족하다.");
            return false;
        }

        Trust -= cost;
        return true;
    }

    // 신뢰는 0 아래로 내려가지 않는다.
    void ChangeTrust(int amount)
    {
        Trust = Math.Max(0, Trust + amount);
    }

    void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    void Report(string message)
    {
        MorningReport.Add(message);
        AddLog(message);
    }

    void AddNote(Guest guest, string note)
    {
        guest.Notes.Add(note);
        AddLog($"{guest.DisplayName} - {note}");
    }

    void AddLog(string message)
    {
        Log.Add(message);
    }

    #endregion
}
