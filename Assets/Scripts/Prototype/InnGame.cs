using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
    // 사망·처형되어 방에서 빠져도 종료 공개를 위해 전부 남긴다.
    public readonly List<Guest> AllGuests = new List<Guest>();
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
    // 조사·밤 제거로 확보한 단서. 회의 제시·반박·심기에 소비.
    public readonly List<PlayerClue> HeldClues = new List<PlayerClue>();
    public int FurClueCount => HeldClues.Count(c => c.Kind == ClueKind.Fur);
    // 회의 후 처형 요구. null이면 없음.
    public Guest ExecutionDemand { get; private set; }
    public int RefutesLeft { get; private set; }
    public readonly List<MeetingClaim> MeetingClaims = new List<MeetingClaim>();
    // 회의 종료(의심 반영)를 이미 했는지. 처형 요구 대기 중 재반영 방지.
    bool meetingResolved;

    public bool CanEndDay => Result == GameResult.Playing && Phase == DayPhase.NightPrep && ExecutionDemand == null;
    public bool CanAdvancePhase => Result == GameResult.Playing && Phase != DayPhase.NightPrep && ExecutionDemand == null;

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
            AllGuests.Add(starters[i]);
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

        if (Phase == DayPhase.Meeting)
        {
            if (!meetingResolved)
            {
                ResolveMeeting();
                meetingResolved = true;
            }

            // 처형 요구가 있으면 회의에 머물러 수락·거부·처형한다.
            if (ExecutionDemand != null)
            {
                AddLog($"{Day}일차: 처형 요구 대기 중. 처리한 뒤 밤 준비로 넘어간다.");
                return;
            }

            Phase = DayPhase.NightPrep;
            AddLog($"{Day}일차: {GameData.GetPhaseName(Phase)} 단계로 넘어갔다.");
            return;
        }

        Phase++;
        if (Phase == DayPhase.Meeting)
        {
            BeginMeeting();
        }

        AddLog($"{Day}일차: {GameData.GetPhaseName(Phase)} 단계로 넘어갔다.");
    }

    // 처형 요구를 처리한 뒤 밤 준비로 넘긴다.
    public void TryLeaveMeetingAfterDemand()
    {
        if (Phase != DayPhase.Meeting || !meetingResolved || ExecutionDemand != null || Result != GameResult.Playing)
        {
            return;
        }

        Phase = DayPhase.NightPrep;
        AddLog($"{Day}일차: {GameData.GetPhaseName(Phase)} 단계로 넘어갔다.");
    }

    // 회의 단계에서 로비 손님과 '어젯밤 일'을 나눈다. 1일차는 전날 밤이 없어 불가.
    public bool CanTalk(Room room)
    {
        return ExecutionDemand == null && Phase == DayPhase.Meeting && room.Guest != null && room.Guest.IsOut &&
               !room.Guest.TalkedToday && Day > 1;
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
        return ExecutionDemand == null && Phase == DayPhase.Recruit && room.Guest != null && !room.Guest.IsOut;
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

    public bool CanInspect(Room room)
    {
        return ExecutionDemand == null && Phase == DayPhase.Recruit && Trust >= GameBalance.InspectCost &&
               (room.Guest == null || room.Guest.IsOut);
    }

    // [털]을 발견하면 단서로 챙기고 방에서 제거한다.
    public void Inspect(Room room)
    {
        if (!CanInspect(room) || !SpendTrust(GameBalance.InspectCost))
        {
            return;
        }

        List<string> clues = new List<string>();
        if (room.HasFur)
        {
            room.HasFur = false;
            PlayerClue fur = AddFurClue(room.Number);
            clues.Add($"방바닥의 털을 수거했다. ({fur.DisplayName})");
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

    PlayerClue AddFurClue(int roomNumber)
    {
        PlayerClue clue = new PlayerClue
        {
            Kind = ClueKind.Fur,
            SourceRoomNumber = roomNumber,
            DayFound = Day
        };
        HeldClues.Add(clue);
        return clue;
    }

    bool TryConsumeFurClue(out PlayerClue used)
    {
        used = HeldClues.FirstOrDefault(c => c.Kind == ClueKind.Fur);
        if (used == null)
        {
            return false;
        }

        HeldClues.Remove(used);
        return true;
    }

    public bool CanMove(Room from) => false;

    public bool Move(Room from, Room to) => false;

    public bool CanInstall(Room room, Facility facility)
    {
        return ExecutionDemand == null && Phase == DayPhase.NightPrep && room.Guest != null &&
               Trust >= GameData.GetFacility(facility).Price && !room.Installed.Contains(facility);
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

    public bool CanInterrogate(Room room) => false;

    public void GuessTrait(Room room, Trait trait)
    {
    }

    public bool CanVerifyTrait(Room room) => false;

    public void VerifyTrait(Room room, Trait trait)
    {
    }

    // 회의 중 선택. [의심] 최대인 손님만. 처형 요구가 있으면 그 대상만.
    public bool CanExecute(Room room)
    {
        return Result == GameResult.Playing && room.Guest != null &&
               room.Guest.Suspicion >= GameBalance.MaxSuspicion &&
               Trust >= GameBalance.ExecuteCost &&
               Phase == DayPhase.Meeting &&
               (ExecutionDemand == null || room.Guest == ExecutionDemand);
    }

    // 괴물이면 순신뢰가 크게 오른다(도구만 상실). 인간이면 신뢰를 잃고 사망한다.
    public void Execute(Room room)
    {
        if (!CanExecute(room) || !SpendTrust(GameBalance.ExecuteCost))
        {
            return;
        }

        Guest guest = room.Guest;
        room.Guest = null;
        guest.Fate = "처형됨";
        if (ExecutionDemand == guest)
        {
            ExecutionDemand = null;
        }

        if (guest.IsMonster)
        {
            // 쓴 비용을 되돌린 뒤 순 +MonsterExecuteTrustGain이 되도록 한다.
            ChangeTrust(GameBalance.ExecuteCost + GameBalance.MonsterExecuteTrustGain);
            Report($"{guest.DisplayName}을(를) 처형했다. 정체는 {GameData.GetName(guest.Species)}이었다. 신뢰 +{GameBalance.MonsterExecuteTrustGain}");
        }
        else
        {
            Report($"{guest.DisplayName}을(를) 처형했다. 정체는 인간이었다...");
        }

        CheckResult();
        if (Result == GameResult.Playing && meetingResolved)
        {
            TryLeaveMeetingAfterDemand();
        }
    }

    public bool CanAcceptExecutionDemand()
    {
        if (ExecutionDemand == null || Result != GameResult.Playing)
        {
            return false;
        }

        Room room = FindRoom(ExecutionDemand);
        return room != null && CanExecute(room);
    }

    public void AcceptExecutionDemand()
    {
        if (!CanAcceptExecutionDemand())
        {
            return;
        }

        Execute(FindRoom(ExecutionDemand));
        if (Result == GameResult.Playing)
        {
            TryLeaveMeetingAfterDemand();
        }
    }

    public void RejectExecutionDemand()
    {
        if (ExecutionDemand == null || Result != GameResult.Playing)
        {
            return;
        }

        Guest demanded = ExecutionDemand;
        ExecutionDemand = null;
        ChangeTrust(-GameBalance.RejectDemandPenalty);
        Report($"{demanded.DisplayName} 처형 요구를 거부했다. 신뢰 -{GameBalance.RejectDemandPenalty}");
        CheckResult();
        if (Result == GameResult.Playing)
        {
            TryLeaveMeetingAfterDemand();
        }
    }

    #endregion

    #region 회의

    void BeginMeeting()
    {
        MeetingClaims.Clear();
        meetingResolved = false;
        RefutesLeft = GameBalance.DailyRefutes;

        // 1일차 아침에는 전날 밤이 없어 주장이 비어 있을 수 있다.
        foreach (Room room in Rooms.Where(r => r.Guest != null && r.Guest.IsOut))
        {
            Guest speaker = room.Guest;
            if (speaker.Trait == Trait.Taciturn)
            {
                continue;
            }

            // 회의 주장은 누군가를 지목하는 것만. [털] 등은 어젯밤 일 대화로 처리.
            foreach (Guest target in speaker.RoamersSeen.Where(t => FindRoom(t) != null).Distinct())
            {
                MeetingClaims.Add(new MeetingClaim
                {
                    Accuser = speaker,
                    Target = target,
                    Text = string.Format(ChatText.ClaimRoamer, speaker.DisplayName, target.DisplayName)
                });
            }
        }

        AddLog($"{Day}일차 회의: 주장 {MeetingClaims.Count}장이 테이블에 올랐다. 반박 {RefutesLeft}회.");
    }

    // 거짓 증거로 새 주장을 테이블에 올린다.
    public bool CanAddFalseClaim(Guest target)
    {
        return Phase == DayPhase.Meeting && ExecutionDemand == null && target != null && FindRoom(target) != null &&
               Trust >= GameBalance.FalseClaimCost;
    }

    public void AddFalseClaim(Guest target)
    {
        if (!CanAddFalseClaim(target) || !SpendTrust(GameBalance.FalseClaimCost))
        {
            return;
        }

        MeetingClaims.Add(new MeetingClaim
        {
            Accuser = null,
            Target = target,
            Text = string.Format(ChatText.ClaimHostFalse, target.DisplayName),
            IsHostFalse = true
        });
        AddLog($"거짓 증거를 제시했다 → {target.DisplayName}. 신뢰 -{GameBalance.FalseClaimCost}");
    }

    // 보유 [털] 단서를 제시해 누군가를 지목하는 주장을 만든다.
    public bool CanPresentFurClaim(Guest target)
    {
        return Phase == DayPhase.Meeting && ExecutionDemand == null && target != null && FindRoom(target) != null &&
               FurClueCount > 0;
    }

    public void PresentFurClaim(Guest target)
    {
        if (!CanPresentFurClaim(target) || !TryConsumeFurClue(out PlayerClue used))
        {
            return;
        }

        MeetingClaims.Add(new MeetingClaim
        {
            Accuser = null,
            Target = target,
            Text = string.Format(ChatText.ClaimFurPresent, target.DisplayName),
            IsHostFalse = false
        });
        AddLog($"털 단서 제시 → {target.DisplayName} 지목. ({used.DisplayName}, 남은 털 단서 {FurClueCount})");
    }

    // 반박에는 보유 단서 1장을 소비한다.
    public bool CanRefute(MeetingClaim claim)
    {
        return Phase == DayPhase.Meeting && claim != null && !claim.Refuted && RefutesLeft > 0 &&
               HeldClues.Count > 0 && ExecutionDemand == null;
    }

    public void RefuteClaim(MeetingClaim claim)
    {
        if (!CanRefute(claim))
        {
            return;
        }

        PlayerClue used = HeldClues[0];
        HeldClues.RemoveAt(0);
        claim.Refuted = true;
        RefutesLeft--;
        AddLog($"단서({used.DisplayName})로 반박: \"{claim.Text}\" (남은 반박 {RefutesLeft}, 단서 {HeldClues.Count})");
    }

    // 채택된 주장 1장당 지목자 의심 +1 (공식 S).
    void ResolveMeeting()
    {
        foreach (MeetingClaim claim in MeetingClaims.Where(c => !c.Refuted && c.Target != null && FindRoom(c.Target) != null))
        {
            Guest target = claim.Target;
            int before = target.Suspicion;
            target.Suspicion = Math.Min(GameBalance.MaxSuspicion, target.Suspicion + 1);
            int gained = target.Suspicion - before;
            if (gained > 0)
            {
                AddLog($"{target.DisplayName} 의심 +{gained} (채택: {claim.Text}). 현재 {target.Suspicion}/{GameBalance.MaxSuspicion}");
            }
        }

        RaiseExecutionDemandIfNeeded();
        if (ExecutionDemand != null)
        {
            AddLog($"손님들이 {ExecutionDemand.DisplayName}의 처형을 요구한다.");
        }
    }

    #endregion

    #region 밤 예약·실행

    public bool CanReserveRemoveFur(Room room)
    {
        return Phase == DayPhase.NightPrep && ExecutionDemand == null && room != null && room.HasFur &&
               !room.ReserveRemoveFur;
    }

    public void ReserveRemoveFur(Room room)
    {
        if (!CanReserveRemoveFur(room))
        {
            return;
        }

        room.ReserveRemoveFur = true;
        room.ReservePlantFur = false;
        AddLog($"{room.DisplayName} 털 제거를 예약했다.");
    }

    public bool CanReservePlantFur(Room room)
    {
        int pendingPlants = Rooms.Count(r => r.ReservePlantFur);
        return Phase == DayPhase.NightPrep && ExecutionDemand == null && room != null && !room.HasFur &&
               !room.ReservePlantFur && FurClueCount > pendingPlants;
    }

    public void ReservePlantFur(Room room)
    {
        if (!CanReservePlantFur(room))
        {
            return;
        }

        room.ReservePlantFur = true;
        room.ReserveRemoveFur = false;
        AddLog($"{room.DisplayName}에 털 뿌리기를 예약했다. (털 단서 {FurClueCount})");
    }

    void ApplyNightReservations()
    {
        foreach (Room room in Rooms.Where(r => r.ReserveRemoveFur))
        {
            if (room.HasFur)
            {
                room.HasFur = false;
                PlayerClue clue = AddFurClue(room.Number);
                AddLog($"예약 실행: {room.DisplayName}의 털을 제거해 단서로 확보. ({clue.DisplayName})");
            }

            room.ReserveRemoveFur = false;
        }

        foreach (Room room in Rooms.Where(r => r.ReservePlantFur))
        {
            if (TryConsumeFurClue(out PlayerClue used) && !room.HasFur)
            {
                room.HasFur = true;
                AddLog($"예약 실행: {used.DisplayName}을(를) {room.DisplayName}에 뿌렸다.");
            }

            room.ReservePlantFur = false;
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

        CheckResult();
        if (Result != GameResult.Playing)
        {
            return;
        }

        Day++;
        StartDay();
    }

    void RunNight()
    {
        ApplyNightReservations();

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
                room.Guest.RoamersSeen.Clear();
            }
        }

        foreach (Room room in Rooms.Where(r => r.Guest != null).ToList())
        {
            Guest guest = room.Guest;
            switch (guest.Trait)
            {
                case Trait.NightOwl:
                    if (!room.Installed.Contains(Facility.ConfineLock))
                    {
                        roamers.Add((guest, room.Floor));
                        LogNightAction(guest, $"{room.Floor}층에서 [복도를 돌아다님]");
                    }
                    else
                    {
                        LogNightAction(guest, "감금되어 [복도를 돌아다님]을 하지 못함");
                    }
                    break;
                case Trait.Suspicious:
                    List<Room> suspects = Rooms.Where(r => r.Guest != null && r.Guest != guest && lobbyGuests.Contains(r.Guest)).ToList();
                    if (!room.Installed.Contains(Facility.ConfineLock) && suspects.Count > 0)
                    {
                        Room suspect = suspects[random.Next(suspects.Count)];
                        lurkers.Add((guest, suspect, suspect.Guest));
                        LogNightAction(guest, $"{suspect.DisplayName}({suspect.Guest.DisplayName}) [문 앞을 서성임]");
                    }
                    else if (room.Installed.Contains(Facility.ConfineLock))
                    {
                        LogNightAction(guest, "감금되어 [문 앞을 서성임]을 하지 못함");
                    }
                    break;
                case Trait.Balding:
                    room.HasFur = true;
                    LogNightAction(guest, $"{room.DisplayName}에 [털]을 떨어뜨림");
                    break;
                case Trait.Unclean:
                    foreach (Room sameFloor in Rooms.Where(r => r.Floor == room.Floor))
                    {
                        sameFloor.HasSmell = true;
                    }
                    LogNightAction(guest, $"같은 층에 [냄새]를 풍김");
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
                LogNightAction(state.Monster, "감금되어 밤 행동을 하지 못함");
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
                LogNightAction(room.Guest, "[비명을 지름] (겁)");
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

        // 밤에 밖에 나와 있던 손님이 같은 층에서 본 것 → 회의 주장 재료(RoamersSeen)
        List<(Guest actor, int floor)> outside = roamers.Concat(lurkers.Select(l => (l.actor, l.room.Floor))).ToList();
        foreach ((Guest actor, int floor) watcher in outside)
        {
            foreach ((Guest actor, int floor) roamer in roamers.Where(r => r.floor == watcher.floor && r.actor != watcher.actor))
            {
                watcher.actor.NightSightings.Add(string.Format(ChatText.SawRoamer, roamer.actor.DisplayName));
                if (!watcher.actor.RoamersSeen.Contains(roamer.actor))
                {
                    watcher.actor.RoamersSeen.Add(roamer.actor);
                }
            }
        }

        // 방에 있던 손님도 같은 층 복도 배회자를 인지하면(숙면가 제외) 회의에서 지목할 수 있다.
        foreach (Room room in Rooms.Where(r => r.Guest != null && r.Guest.Trait != Trait.DeepSleeper))
        {
            Guest guest = room.Guest;
            if (outside.Any(o => o.actor == guest))
            {
                continue;
            }

            foreach ((Guest actor, int floor) roamer in roamers.Where(r => r.floor == room.Floor && r.actor != guest))
            {
                if (!guest.RoamersSeen.Contains(roamer.actor))
                {
                    guest.RoamersSeen.Add(roamer.actor);
                }
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
            LogNightAction(state.Monster, $"휴식 중 (남은 {state.Rest}일, 단계 {state.Stage})");
            state.Rest--;
            return;
        }

        if (state.Stage > 1 && targetRoom.Installed.Contains(counterFacility))
        {
            if (state.Stage == 3)
            {
                ChangeTrust(GameBalance.DefenseReward);
                Report($"{targetRoom.DisplayName} 문에 무언가 긁고 간 자국이 있다. 신뢰 +{GameBalance.DefenseReward}");
                LogNightAction(state.Monster, $"{targetRoom.DisplayName} 공격이 대응 설비에 막힘");
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
                    LogNightAction(state.Monster, "1단계: 타깃을 고르지 못함");
                    return;
                }

                Room picked = candidates[random.Next(candidates.Count)];
                state.Target = picked.Guest;
                if (state.Rampage)
                {
                    rampageMonsters.Add(state.Monster);
                }
                roamers.Add((state.Monster, picked.Floor));
                LogNightAction(state.Monster,
                    $"1단계: {picked.Guest.DisplayName}({picked.DisplayName})을(를) 타깃으로 선정" +
                    (state.Rampage ? " (폭주)" : "") + ", 그 층에서 [복도를 돌아다님]");
                state.Stage = 2;
                state.Rest = firstStageRest;
                break;

            case 2:
                if (lurkAtSecondStage)
                {
                    lurkers.Add((state.Monster, targetRoom, targetRoom.Guest));
                    LogNightAction(state.Monster, $"2단계: {targetRoom.DisplayName} [문 앞을 서성임]");
                }
                else
                {
                    targetRoom.HasFur = true;
                    LogNightAction(state.Monster, $"2단계: {targetRoom.DisplayName}에 [털]을 남김");
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
                    LogNightAction(state.Monster, $"3단계: {state.Target.DisplayName} 공격이 방범 자물쇠에 막힘");
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
                        LogNightAction(state.Monster, $"3단계: {state.Target.DisplayName}을(를) 공격했으나 살아남음");
                        LogNightAction(state.Target, "괴물에게 공격받았으나 생존");
                    }
                    else
                    {
                        // 살인마 v1: 괴물이 대신 죽인 손님은 신뢰 패널티 없음.
                        state.Target.Fate = "피살";
                        LogNightAction(state.Monster, $"3단계: {state.Target.DisplayName}을(를) 공격해 살해");
                        LogNightAction(state.Target, "괴물에게 피살");
                        targetRoom.Guest = null;
                        Report($"{targetRoom.DisplayName}에서 {state.Target.DisplayName}의 시체가 발견되었다.");
                    }
                }
                break;
        }
    }

    void LogNightAction(Guest guest, string action)
    {
        if (guest == null)
        {
            return;
        }

        guest.NightActionLog.Add($"{Day}일차 밤: {action}");
    }

    // 종료 화면용: 전원 특성·괴물 여부·밤 행동 로그.
    public string BuildRevealSummary()
    {
        StringBuilder text = new StringBuilder();
        text.AppendLine(Result == GameResult.Won ? "【승리】" : "【패배】");
        text.AppendLine(ResultMessage);
        text.AppendLine();

        foreach (Guest guest in AllGuests.OrderBy(g => g.Id))
        {
            string traitName = GameData.Traits.First(t => t.Id == guest.Trait).Name;
            string species = guest.IsMonster ? GameData.GetName(guest.Species) : "인간";
            text.AppendLine($"── {guest.DisplayName} ──");
            text.AppendLine($"정체: {species}");
            text.AppendLine($"특성: {traitName}");
            text.AppendLine($"결말: {guest.Fate}");
            text.AppendLine("밤 행동:");
            if (guest.NightActionLog.Count == 0)
            {
                text.AppendLine("  (기록 없음)");
            }
            else
            {
                foreach (string line in guest.NightActionLog)
                {
                    text.AppendLine($"  · {line}");
                }
            }

            text.AppendLine();
        }

        return text.ToString().TrimEnd();
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

    void RaiseExecutionDemandIfNeeded()
    {
        Guest target = Rooms
            .Where(r => r.Guest != null && r.Guest.Suspicion >= GameBalance.MaxSuspicion)
            .Select(r => r.Guest)
            .OrderByDescending(g => g.Suspicion)
            .ThenBy(g => g.Id)
            .FirstOrDefault();
        if (target == null)
        {
            ExecutionDemand = null;
            return;
        }

        ExecutionDemand = target;
    }

    // 승리: 전원 사망. 패배: 신뢰 0, 또는 인간 전멸·괴물 잔존.
    void CheckResult()
    {
        if (Result != GameResult.Playing)
        {
            return;
        }

        if (Trust <= 0)
        {
            Result = GameResult.Lost;
            ResultMessage = "신뢰가 바닥났다. 정체가 들통났다.";
            return;
        }

        bool anyGuest = Rooms.Any(r => r.Guest != null);
        bool anyHuman = Rooms.Any(r => r.Guest != null && !r.Guest.IsMonster);
        bool anyMonster = Rooms.Any(r => r.Guest != null && r.Guest.IsMonster);

        if (!anyGuest)
        {
            Result = GameResult.Won;
            ResultMessage = "여관의 모든 자를 처리했다.";
            return;
        }

        if (!anyHuman && anyMonster)
        {
            Result = GameResult.Lost;
            ResultMessage = "인간 손님이 모두 죽었다. 괴물과 1대1로는 이길 수 없다.";
        }
    }

    void StartDay()
    {
        Phase = DayPhase.Recruit;
        MeetingClaims.Clear();
        meetingResolved = false;
        RefutesLeft = 0;
        Interrogations = GameBalance.DailyInterrogations;

        foreach (Room room in Rooms)
        {
            room.Installed.Clear();
            room.ReserveRemoveFur = false;
            room.ReservePlantFur = false;
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

        // 탈모로 자기가 떨어뜨린 털은 말하지 않는다. 남의 털·괴물 털만.
        Room room = FindRoom(guest);
        if (room != null && room.HasFur && guest.Trait != Trait.Balding)
        {
            lines.Add(ChatText.TalkFoundFur);
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

    // 신뢰는 0 아래로 내려가지 않는다. 0이 되면 CheckResult에서 패배 처리.
    void ChangeTrust(int amount)
    {
        Trust = Math.Max(0, Trust + amount);
        if (amount < 0)
        {
            CheckResult();
        }
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
