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

    readonly Random random = new Random();
    readonly List<MonsterState> monsterStates = new List<MonsterState>();
    readonly List<string> hiddenFragments = new List<string>();
    readonly HashSet<int> perfumeRoomNumbers = new HashSet<int>();
    readonly HashSet<int> lockedTonight = new HashSet<int>();
    readonly Dictionary<int, List<string>> floorEventKinds = new Dictionary<int, List<string>>();
    readonly DifficultyConfig config;
    int nextGuestId = 1;
    int nextCheckInOrder = 1;
    OmenTrait omenTrait;
    TargetRule targetRule;
    bool inspectedToday;

    public readonly List<Room> Rooms = new List<Room>();
    public readonly List<string> RevealedFragments = new List<string>();
    public readonly List<string> Log = new List<string>();
    public readonly List<string> NightLog = new List<string>();
    public readonly List<string> MorningReport = new List<string>();
    public string Memo = "";

    public Difficulty Difficulty { get; }
    public DifficultyConfig Config => config;
    public Species RunSpecies { get; private set; }
    public OmenTrait RunOmen { get; private set; }
    public TargetRule RunTarget { get; private set; }
    public int Day { get; private set; }
    public DayPhase Phase { get; private set; }
    public int JobActionsLeft { get; private set; }
    public int ExecutionsLeft { get; private set; }
    public int OwnerLocks { get; private set; }
    public GameResult Result { get; private set; }
    public string ResultMessage { get; private set; }

    public bool CanEndDay => Result == GameResult.Playing && Phase == DayPhase.Focus;
    public bool CanAdvancePhase => Result == GameResult.Playing && Phase == DayPhase.Gather;

    // 수색 직업에게 시키지 않으면 하루 한 방만 조사할 수 있다.
    public bool CanInspect(Room room)
    {
        return Phase == DayPhase.Gather && !inspectedToday && room != null && !room.DoorLocked;
    }

    public InnGame(Difficulty difficulty = Difficulty.VeryEasy)
    {
        Difficulty = difficulty;
        config = GameBalance.Get(difficulty);
        Day = 1;
        OwnerLocks = GameBalance.OwnerRoomLocks;
        Result = GameResult.Playing;

        for (int floor = 1; floor <= config.Floors; floor++)
        {
            for (int column = 1; column <= config.RoomsPerFloor; column++)
            {
                Rooms.Add(new Room { Floor = floor, Column = column, Number = floor * 100 + column });
            }
        }

        AssignWerewolfTraits();

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

        SpeciesDefinition species = GameData.GetSpecies(RunSpecies);
        Report($"이번 회차에 등장하는 괴물: {species.Name} (공통: {species.Trait})");
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

    public bool CanTalk(Room room)
    {
        return Phase == DayPhase.Focus && room.Guest != null && room.Guest.IsOut && !room.Guest.TalkedToday && Day > 1;
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
        return Phase == DayPhase.Gather && room.Guest != null && !room.Guest.IsOut;
    }

    public void LureOut(Room room)
    {
        if (!CanLureOut(room))
        {
            return;
        }

        Guest guest = room.Guest;
        guest.IsOut = true;
        AddNote(guest, $"{Day}일차 로비로 부르기: 로비로 나왔다.");
    }

    // 손님을 클릭해 직업 주장을 듣는다. 아직 안 들은 손님만.
    public bool CanHearClaim(Room room)
    {
        return Result == GameResult.Playing && room.Guest != null && !room.Guest.ClaimHeard;
    }

    public void HearClaim(Room room)
    {
        if (!CanHearClaim(room))
        {
            return;
        }

        Guest guest = room.Guest;
        guest.ClaimHeard = true;
        string jobName = GameData.GetJob(guest.ClaimedJob).Name;
        AddNote(guest, $"{Day}일차 자기소개: 자신은 {jobName}이(가)라고 했다.");
    }

    public void Inspect(Room room)
    {
        if (!CanInspect(room))
        {
            return;
        }

        inspectedToday = true;
        room.Inspected = true;
        List<string> clues = new List<string>();
        if (room.HasFur)
        {
            clues.Add("[털]");
            room.HasFur = false;
        }

        if (room.HasSmell)
        {
            clues.Add("[냄새]");
        }

        string result = clues.Count == 0
            ? $"{room.DisplayName}: 특별한 흔적이 없다."
            : $"{room.DisplayName}: {string.Join(", ", clues)} 발견";
        AddLog($"{Day}일차 방 조사:\n" + result);
    }

    public bool CanExecute(Room room)
    {
        return Phase == DayPhase.Focus && room.Guest != null && ExecutionsLeft > 0;
    }

    public void Execute(Room room)
    {
        if (!CanExecute(room))
        {
            return;
        }

        ExecutionsLeft--;
        Guest guest = room.Guest;
        room.Guest = null;
        if (guest.IsMonster)
        {
            Report($"{guest.DisplayName}을(를) 처형했다. 정체는 {GameData.GetName(guest.Species)}이었다.");
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

    // 주장 직업 기준으로 직업 행동 1회. 실제 수행은 실제 직업이 맞을 때만 된다.
    public bool CanOrder(Room room)
    {
        return Phase == DayPhase.Focus && room.Guest != null && JobActionsLeft > 0 &&
               room.Guest.PendingOrder == NightOrder.None && GetRequestableJob(room.Guest) != null;
    }

    public Job? GetRequestableJob(Guest guest)
    {
        if (!guest.ClaimHeard)
        {
            return null;
        }

        // 정찰병은 요청이 없고, 주장·실제가 정찰병이면 아침 자동 보고만 한다.
        return guest.ClaimedJob == Job.Scout ? (Job?)null : guest.ClaimedJob;
    }

    public void ClearOrder(Guest guest)
    {
        if (guest.PendingOrder == NightOrder.None)
        {
            return;
        }

        guest.PendingOrder = NightOrder.None;
        guest.OrderRoomNumber = 0;
        guest.OrderGuestId = 0;
        JobActionsLeft = Math.Min(GameBalance.DailyJobActions, JobActionsLeft + 1);
        AddLog($"{guest.DisplayName}에게 맡긴 직업 행동을 취소했다. (직업 행동 남은 횟수 {JobActionsLeft})");
    }

    public void SetOrderLock(Guest guest, Room target)
    {
        if (!TryBeginOrder(guest, Job.Locksmith))
        {
            return;
        }

        guest.PendingOrder = NightOrder.LockRoom;
        guest.OrderRoomNumber = target.Number;
        AddNote(guest, $"{Day}일차 직업 행동: {target.DisplayName} 방문을 잠가 달라고 시켰다.");
    }

    public void SetOrderPerfume(Guest guest, Room target)
    {
        if (!TryBeginOrder(guest, Job.Pharmacist))
        {
            return;
        }

        guest.PendingOrder = NightOrder.PerfumeRoom;
        guest.OrderRoomNumber = target.Number;
        AddNote(guest, $"{Day}일차 직업 행동: {target.DisplayName}에 향을 뿌리라고 시켰다.");
    }

    public void SetOrderWatch(Guest guest, Room target)
    {
        if (!TryBeginOrder(guest, Job.DogHandler))
        {
            return;
        }

        guest.PendingOrder = NightOrder.WatchRoom;
        guest.OrderRoomNumber = target.Number;
        AddNote(guest, $"{Day}일차 직업 행동: {target.DisplayName} 앞을 감시하라고 시켰다.");
    }

    public void SetOrderClean(Guest guest, Room target)
    {
        if (!TryBeginOrder(guest, Job.Cleaner))
        {
            return;
        }

        guest.PendingOrder = NightOrder.CleanRoom;
        guest.OrderRoomNumber = target.Number;
        AddNote(guest, $"{Day}일차 직업 행동: {target.DisplayName}을(를) 청소하라고 시켰다.");
    }

    public void SetOrderRemember(Guest guest, Guest target)
    {
        if (!TryBeginOrder(guest, Job.Butler) || target == guest)
        {
            return;
        }

        guest.PendingOrder = NightOrder.RememberGuest;
        guest.OrderGuestId = target.Id;
        AddNote(guest, $"{Day}일차 직업 행동: {target.DisplayName}이(가) 밤에 움직였는지 기억하라고 시켰다.");
    }

    public void SetOrderInformant(Guest guest)
    {
        if (!TryBeginOrder(guest, Job.Informant))
        {
            return;
        }

        guest.PendingOrder = NightOrder.AskMonsterInfo;
        AddNote(guest, $"{Day}일차 직업 행동: 괴물 정보를 말하라고 시켰다.");
    }

    bool TryBeginOrder(Guest guest, Job requiredClaim)
    {
        if (Phase != DayPhase.Focus || guest == null || JobActionsLeft <= 0 || guest.PendingOrder != NightOrder.None)
        {
            return false;
        }

        if (!guest.ClaimHeard || guest.ClaimedJob != requiredClaim)
        {
            return false;
        }

        JobActionsLeft--;
        return true;
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
        RunNight();

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
        List<(Guest actor, int floor)> roamers = new List<(Guest actor, int floor)>();
        List<(Guest actor, Room room, Guest owner)> lurkers = new List<(Guest actor, Room room, Guest owner)>();
        List<Room> screamRooms = new List<Room>();
        List<Guest> rampageMonsters = new List<Guest>();
        List<Guest> nightMovers = new List<Guest>();
        floorEventKinds.Clear();
        perfumeRoomNumbers.Clear();
        lockedTonight.Clear();

        foreach (Room room in Rooms)
        {
            room.HasSmell = false;
            room.HasFootprint = false;
            room.DoorLocked = false;
            if (room.Guest != null)
            {
                room.Guest.IsOut = false;
                room.Guest.HeardHallway = false;
                room.Guest.HeardScream = false;
                room.Guest.SurvivedAttack = false;
                room.Guest.NightSightings.Clear();
                room.Guest.PendingMorningNotes.Clear();
            }
        }

        ApplyJobOrders();
        ApplyPassiveJobLocks();

        foreach (Room room in Rooms.Where(r => lockedTonight.Contains(r.Number)))
        {
            room.DoorLocked = true;
        }

        IEnumerable<MonsterState> actors = monsterStates
            .Where(s => FindRoom(s.Monster) != null)
            .OrderByDescending(s => GameData.GetSpecies(s.Monster.Species).NightSpeed)
            .ThenBy(s => s.Monster.CheckInOrder)
            .ToList();
        foreach (MonsterState state in actors)
        {
            Room room = FindRoom(state.Monster);
            if (room.DoorLocked)
            {
                continue;
            }

            RunWerewolf(state, room, roamers, lurkers, screamRooms, rampageMonsters);
        }

        foreach ((Guest actor, int floor) roamer in roamers)
        {
            nightMovers.Add(roamer.actor);
            AddFloorEvent(roamer.floor, "[복도를 돌아다님]");
            foreach (Room room in Rooms.Where(r => r.Floor == roamer.floor))
            {
                room.HasFootprint = true;
            }
        }

        foreach ((Guest actor, Room room, Guest owner) lurk in lurkers)
        {
            nightMovers.Add(lurk.actor);
            AddFloorEvent(lurk.room.Floor, "[문 앞을 서성임]");
        }

        foreach (Room room in screamRooms)
        {
            AddFloorEvent(room.Floor, "[비명을 지름]");
        }

        ResolveWatchOrders(lurkers);
        ResolveButlerOrders(nightMovers.Distinct().ToList());
        ResolveScoutReports();

        foreach (Room room in Rooms.Where(r => r.Guest != null))
        {
            Guest guest = room.Guest;
            guest.HeardHallway = roamers.Any(r => r.floor == room.Floor && r.actor != guest);
            guest.HeardScream = screamRooms.Any(s => IsAround(s, room));

            if (guest.Job == Job.DogHandler)
            {
                foreach ((Guest actor, Room room, Guest owner) lurk in lurkers.Where(l => l.room == room))
                {
                    guest.PendingMorningNotes.Add($"자기 방 앞에서 {lurk.actor.DisplayName}이(가) [문 앞을 서성임]을 했다.");
                }
            }

            if (guest.Job == Job.Cleaner)
            {
                List<string> weak = Rooms.Where(r => r.Floor == room.Floor && r.HasFootprint)
                    .Select(r => $"{r.DisplayName} [발자국]").ToList();
                if (weak.Count > 0)
                {
                    guest.PendingMorningNotes.Add("자기 층 약한 흔적: " + string.Join(", ", weak));
                }
            }

            if (guest.Job == Job.Pharmacist)
            {
                foreach (Room sameFloor in Rooms.Where(r => r.Floor == room.Floor))
                {
                    sameFloor.HasSmell = true;
                }
            }
        }

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

        foreach (Room room in Rooms.Where(r => r.Guest != null))
        {
            room.Guest.PendingOrder = NightOrder.None;
            room.Guest.OrderRoomNumber = 0;
            room.Guest.OrderGuestId = 0;
        }
    }

    void ApplyJobOrders()
    {
        foreach (Room room in Rooms.Where(r => r.Guest != null).ToList())
        {
            Guest guest = room.Guest;
            if (guest.PendingOrder == NightOrder.None)
            {
                continue;
            }

            // 요청은 주장 직업으로 받지만, 실제 수행은 실제 직업이 맞을 때만 된다.
            if (!OrderMatchesJob(guest.PendingOrder, guest.Job))
            {
                continue;
            }

            switch (guest.PendingOrder)
            {
                case NightOrder.LockRoom:
                    lockedTonight.Add(guest.OrderRoomNumber);
                    guest.PendingMorningNotes.Add($"{guest.OrderRoomNumber}호실 방문을 잠갔다.");
                    break;
                case NightOrder.PerfumeRoom:
                    perfumeRoomNumbers.Add(guest.OrderRoomNumber);
                    guest.PendingMorningNotes.Add($"{guest.OrderRoomNumber}호실에 향을 뿌렸다.");
                    break;
                case NightOrder.CleanRoom:
                    Room cleanTarget = Rooms.FirstOrDefault(r => r.Number == guest.OrderRoomNumber);
                    if (cleanTarget != null)
                    {
                        List<string> found = new List<string>();
                        if (cleanTarget.HasFur)
                        {
                            found.Add("[털]");
                            cleanTarget.HasFur = false;
                        }

                        if (cleanTarget.HasSmell)
                        {
                            found.Add("[냄새]");
                            cleanTarget.HasSmell = false;
                        }

                        if (cleanTarget.HasFootprint)
                        {
                            found.Add("[발자국]");
                            cleanTarget.HasFootprint = false;
                        }

                        guest.PendingMorningNotes.Add(found.Count == 0
                            ? $"{cleanTarget.DisplayName}을(를) 청소했다. 단서는 없었다."
                            : $"{cleanTarget.DisplayName}을(를) 청소하며 {string.Join(", ", found)}을(를) 치웠다.");
                    }
                    break;
                case NightOrder.AskMonsterInfo:
                    if (hiddenFragments.Count == 0)
                    {
                        guest.PendingMorningNotes.Add("더 이상 알려 줄 괴물 정보가 없다고 했다.");
                    }
                    else
                    {
                        string fragment = hiddenFragments[random.Next(hiddenFragments.Count)];
                        hiddenFragments.Remove(fragment);
                        RevealedFragments.Add(fragment);
                        guest.PendingMorningNotes.Add($"괴물 정보를 알려 주었다: {fragment}");
                        Report($"{guest.DisplayName}(정보원): \"{fragment}\"");
                    }
                    break;
                case NightOrder.WatchRoom:
                case NightOrder.RememberGuest:
                    // 밤 행동 집계 뒤에 해석한다.
                    break;
            }
        }
    }

    void ApplyPassiveJobLocks()
    {
        foreach (Room room in Rooms.Where(r => r.Guest != null && r.Guest.Job == Job.Locksmith))
        {
            lockedTonight.Add(room.Number);
            room.Guest.PendingMorningNotes.Add("자기 방문을 잠갔다.");
        }
    }

    void ResolveWatchOrders(List<(Guest actor, Room room, Guest owner)> lurkers)
    {
        foreach (Room room in Rooms.Where(r => r.Guest != null && r.Guest.PendingOrder == NightOrder.WatchRoom && r.Guest.Job == Job.DogHandler))
        {
            Guest guest = room.Guest;
            Room target = Rooms.FirstOrDefault(r => r.Number == guest.OrderRoomNumber);
            if (target == null)
            {
                continue;
            }

            List<(Guest actor, Room room, Guest owner)> hits = lurkers.Where(l => l.room == target).ToList();
            guest.PendingMorningNotes.Add(hits.Count == 0
                ? $"{target.DisplayName} 앞을 감시했지만 아무도 서성이지 않았다."
                : $"{target.DisplayName} 앞에서 {string.Join(", ", hits.Select(h => h.actor.DisplayName))}이(가) 서성였다.");
        }
    }

    void ResolveButlerOrders(List<Guest> nightMovers)
    {
        foreach (Room room in Rooms.Where(r => r.Guest != null && r.Guest.PendingOrder == NightOrder.RememberGuest && r.Guest.Job == Job.Butler))
        {
            Guest guest = room.Guest;
            Guest target = Rooms.Select(r => r.Guest).FirstOrDefault(g => g != null && g.Id == guest.OrderGuestId);
            if (target == null)
            {
                guest.PendingMorningNotes.Add("기억해 달라고 한 손님이 이미 없다.");
                continue;
            }

            bool moved = nightMovers.Contains(target);
            guest.PendingMorningNotes.Add(moved
                ? $"{target.DisplayName}은(는) 어젯밤 방에서 나와 움직였다."
                : $"{target.DisplayName}은(는) 어젯밤 방에서 나오지 않았다.");
        }
    }

    void ResolveScoutReports()
    {
        foreach (Room room in Rooms.Where(r => r.Guest != null &&
                                               r.Guest.ClaimHeard &&
                                               r.Guest.ClaimedJob == Job.Scout &&
                                               r.Guest.Job == Job.Scout))
        {
            Guest guest = room.Guest;
            if (!floorEventKinds.TryGetValue(room.Floor, out List<string> events) || events.Count == 0)
            {
                guest.PendingMorningNotes.Add($"정찰 보고: {room.Floor}층에서는 특별한 일이 없었다.");
            }
            else
            {
                guest.PendingMorningNotes.Add($"정찰 보고: {room.Floor}층에서 {string.Join(", ", events.Distinct())}이(가) 있었다. (누가 했는지는 모름)");
            }
        }
    }

    void AddFloorEvent(int floor, string kind)
    {
        if (!floorEventKinds.TryGetValue(floor, out List<string> list))
        {
            list = new List<string>();
            floorEventKinds[floor] = list;
        }

        list.Add(kind);
    }

    static bool OrderMatchesJob(NightOrder order, Job job)
    {
        switch (order)
        {
            case NightOrder.LockRoom:
                return job == Job.Locksmith;
            case NightOrder.PerfumeRoom:
                return job == Job.Pharmacist;
            case NightOrder.WatchRoom:
                return job == Job.DogHandler;
            case NightOrder.CleanRoom:
                return job == Job.Cleaner;
            case NightOrder.RememberGuest:
                return job == Job.Butler;
            case NightOrder.AskMonsterInfo:
                return job == Job.Informant;
            default:
                return false;
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

        if (state.Stage > 1 && targetRoom != null && perfumeRoomNumbers.Contains(targetRoom.Number))
        {
            if (state.Stage == 3)
            {
                Report($"{targetRoom.DisplayName} 문에 무언가 긁고 간 자국이 있다.");
            }

            return;
        }

        switch (state.Stage)
        {
            case 1:
                List<Room> humans = Rooms.Where(r => r != room && r.Guest != null && !r.Guest.IsMonster).ToList();
                state.Rampage = !humans.Any(r => IsInTargetRange(room, r));
                List<Room> candidates = humans.Where(r => (state.Rampage || IsInTargetRange(room, r)) &&
                                                          !perfumeRoomNumbers.Contains(r.Number) &&
                                                          !r.DoorLocked).ToList();
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
                state.Rest = 0;
                break;

            case 2:
                if (targetRoom.DoorLocked)
                {
                    return;
                }

                switch (omenTrait)
                {
                    case OmenTrait.TerritoryMark:
                        targetRoom.HasFur = true;
                        break;
                    case OmenTrait.DoorHunter:
                        lurkers.Add((state.Monster, targetRoom, targetRoom.Guest));
                        break;
                    case OmenTrait.SilentApproach:
                        // 2단계 단서 없음
                        break;
                }

                state.Stage = 3;
                state.Rest = 0;
                break;

            case 3:
                state.Attacked = true;
                if (targetRoom.DoorLocked)
                {
                    Report($"{targetRoom.DisplayName} 문에 무언가 긁고 간 자국이 있다.");
                }
                else
                {
                    screamRooms.Add(targetRoom);
                    targetRoom.Guest = null;
                    Report($"{targetRoom.DisplayName}에서 {state.Target.DisplayName}의 시체가 발견되었다.");
                }
                break;
        }
    }

    // 회차 표적 슬롯에 따라 타깃 후보 범위를 판정한다.
    bool IsInTargetRange(Room from, Room to)
    {
        switch (targetRule)
        {
            case TargetRule.SameFloorHuman:
                return to.Floor == from.Floor;
            case TargetRule.CrossAdjacent:
                // 상하좌우 1칸. 대각선·주인 방은 제외.
                int floorDelta = Math.Abs(to.Floor - from.Floor);
                int columnDelta = Math.Abs(to.Column - from.Column);
                return floorDelta + columnDelta == 1;
            case TargetRule.AnyHuman:
                return true;
            default:
                return to.Floor == from.Floor;
        }
    }

    void StartDay()
    {
        Phase = DayPhase.Gather;
        JobActionsLeft = GameBalance.DailyJobActions;
        ExecutionsLeft = GameBalance.DailyExecutions;
        inspectedToday = false;

        foreach (Room room in Rooms)
        {
            if (room.Guest != null)
            {
                room.Guest.IsOut = random.NextDouble() < GameBalance.OutingChance;
                room.Guest.TalkedToday = false;
                room.Guest.Notes.Clear();
                foreach (string note in room.Guest.PendingMorningNotes)
                {
                    AddNote(room.Guest, $"{Day}일차 아침: {note}");
                }

                room.Guest.PendingMorningNotes.Clear();
            }
        }

        AddLog($"{Day}일차 낮이 밝았다.");
    }

    #endregion

    #region 내부 유틸

    void AssignWerewolfTraits()
    {
        RunSpecies = Species.Werewolf;
        OmenTraitDefinition omen = GameData.OmenTraits[random.Next(GameData.OmenTraits.Length)];
        omenTrait = omen.Id;
        RunOmen = omen.Id;

        TargetRuleDefinition target = GameData.TargetRules[random.Next(GameData.TargetRules.Length)];
        targetRule = target.Id;
        RunTarget = target.Id;

        // 헌터에게 줄 힌트. 특성 이름은 직접 말하지 않는다.
        hiddenFragments.Add(omen.HunterHint);
        hiddenFragments.Add(target.HunterHint);
        hiddenFragments.Add("약제사 향을 피우거나, 문을 잠가 막을 수 있다.");
    }

    List<Guest> CreateGuests(int count)
    {
        List<Job> jobs = AssignJobs(count);
        List<string> names = GameData.GuestNames.ToList();
        Shuffle(names);
        int monsters = 0;
        int required = config.MinMonsters;
        List<Guest> guests = new List<Guest>();

        for (int i = 0; i < count; i++)
        {
            bool isMonster = i < required ||
                             (monsters < config.MaxMonsters && random.NextDouble() < GameBalance.MonsterChance);
            if (isMonster)
            {
                monsters++;
            }

            Job job = jobs[i];
            Job claimed = job;
            if (isMonster && random.NextDouble() < GameBalance.MonsterLieChance)
            {
                List<Job> lies = GameData.Jobs.Select(j => j.Id).Where(j => j != job).ToList();
                claimed = lies[random.Next(lies.Count)];
            }

            guests.Add(new Guest
            {
                Id = nextGuestId++,
                Species = isMonster ? RunSpecies : Species.Human,
                Job = job,
                ClaimedJob = claimed,
                Name = i < names.Count ? names[i] : null
            });
        }

        Shuffle(guests);
        return guests;
    }

    List<Job> AssignJobs(int count)
    {
        List<Job> picked = new List<Job>();
        List<Job> hunter = JobsOf(JobCategory.Hunter);
        List<Job> search = JobsOf(JobCategory.Search);
        List<Job> protect = JobsOf(JobCategory.Protect);
        List<Job> other = JobsOf(JobCategory.Other);

        void TakeMin(List<Job> pool, int min)
        {
            Shuffle(pool);
            for (int i = 0; i < min && pool.Count > 0 && picked.Count < count; i++)
            {
                picked.Add(pool[0]);
                pool.RemoveAt(0);
            }
        }

        TakeMin(hunter, GameBalance.MinHunter);
        TakeMin(search, GameBalance.MinSearch);
        TakeMin(protect, GameBalance.MinProtect);
        TakeMin(other, GameBalance.MinOther);

        List<Job> rest = hunter.Concat(search).Concat(protect).Concat(other).ToList();
        Shuffle(rest);
        while (picked.Count < count && rest.Count > 0)
        {
            picked.Add(rest[0]);
            rest.RemoveAt(0);
        }

        // 풀이 부족하면 중복 허용
        while (picked.Count < count)
        {
            picked.Add(GameData.Jobs[random.Next(GameData.Jobs.Length)].Id);
        }

        Shuffle(picked);
        return picked;
    }

    static List<Job> JobsOf(JobCategory category)
    {
        return GameData.Jobs.Where(j => j.Category == category).Select(j => j.Id).ToList();
    }

    string GetLastNightLine(Guest guest)
    {
        if (guest.SurvivedAttack)
        {
            return ChatText.SurvivedAttack;
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
