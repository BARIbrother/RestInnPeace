using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class InnGameView : MonoBehaviour
{
    enum Popup
    {
        None,
        NightLog,
        Report,
        ExecutionDemand
    }

    enum GuideCategory
    {
        Monster,
        Trait,
        Facility,
        Clue
    }

    const float ReferenceWidth = 1920f;
    const float ReferenceHeight = 1080f;
    const float CellWidth = 300f;
    const float FloorHeight = 230f;
    const float RoomHeight = 165f;
    const float CellGap = 10f;
    const float InnLeft = 30f;
    const float InnTop = 90f;
    const int InnColumns = 3;
    const float LobbyTop = 580f;
    const float LobbyHeight = 190f;
    const float PanelLeft = 1240f;
    const float PanelWidth = 660f;
    const float TooltipWidth = 420f;
    static readonly Color TagColor = new Color(1f, 0.85f, 0.4f);

    // 긴 태그가 짧은 태그보다 먼저 맞춰지도록 길이 내림차순으로 둔다.
    static readonly string[] Tags = GameData.Tooltips.Select(t => t.Tag).OrderByDescending(t => t.Length).ToArray();

    InnGame game;
    Room selectedRoom;
    bool ownerRoomSelected;
    Room movingFrom;
    // 선택한 손님의 집중 심문 화면(특성 맞히기·검증)을 열었는지.
    bool interrogating;
    // 집중 심문 화면에서 특성 버튼이 맞히기 대신 검증(가졌는지 아닌지만 판정)으로 쓰이는지.
    bool verifying;
    // 처형은 되돌릴 수 없으므로, 같은 손님의 처형 버튼을 한 번 더 눌러야 실행한다.
    Guest executeConfirm;
    Popup popup;
    int nightLogIndex;
    bool inputEnabled;
    Vector2 logScroll;
    Vector2 notesScroll;
    // 손님 기록 내용 높이가 바뀌면(새 기록 추가, 다른 손님 선택) 최신 기록이 있는 맨 아래로 스크롤하기 위해 지난 높이를 기억한다.
    float notesHeight;
    Vector2 guideScroll;
    Vector2 memoScroll;
    Vector2 resultScroll;
    bool showMemoTab;
    GuideCategory guideCategory;
    // 태그를 클릭해 연 도감 설명 페이지. null이면 분류 목록을 보여 준다.
    string guideTag;
    // 뒤로가기용으로, 태그 페이지로 넘어가기 직전의 패널 상태를 쌓아 둔다.
    readonly Stack<(bool showMemoTab, GuideCategory category, string tag, Vector2 scroll)> guideHistory = new Stack<(bool, GuideCategory, string, Vector2)>();
    // 이번 OnGUI에서 커서가 올라간 태그. 모든 UI를 그린 뒤 맨 위에 툴팁으로 띄운다.
    string hoveredTag;
    // 이번 OnGUI에서 클릭된 태그. 그리는 도중 패널 상태가 바뀌지 않도록 다 그린 뒤 페이지를 연다.
    string clickedTag;
    // 팝업에 가려졌거나 스크롤 영역 밖으로 잘린 태그는 툴팁을 띄우지 않기 위한 플래그.
    bool tooltipEnabled;

    GUIStyle labelStyle;
    GUIStyle smallStyle;
    GUIStyle titleStyle;
    GUIStyle buttonStyle;
    GUIStyle textAreaStyle;
    readonly Dictionary<GUIStyle, GUIStyle> inlineStyles = new Dictionary<GUIStyle, GUIStyle>();
    readonly Dictionary<(int, string), float> pieceWidths = new Dictionary<(int, string), float>();

    // 씬 설정 없이 플레이하면 바로 프로토타입이 뜨도록 한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindAnyObjectByType<InnGameView>() != null)
        {
            return;
        }

        new GameObject("InnGame").AddComponent<InnGameView>();
    }

    void Awake()
    {
        StartNewGame();
    }

    void StartNewGame()
    {
        game = new InnGame();
        selectedRoom = null;
        ownerRoomSelected = false;
        movingFrom = null;
        interrogating = false;
        executeConfirm = null;
        popup = Popup.Report;
    }

    void OnGUI()
    {
        EnsureStyles();
        GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width / ReferenceWidth, Screen.height / ReferenceHeight, 1f));

        HandleShortcuts();
        // IMGUI 텍스트 입력은 IME가 자동으로 켜지지 않아 한글 조합이 안 되므로, 입력 중일 때만 강제로 켠다.
        Input.imeCompositionMode = GUIUtility.keyboardControl != 0 ? IMECompositionMode.On : IMECompositionMode.Auto;
        inputEnabled = popup == Popup.None && game.Result == GameResult.Playing;
        hoveredTag = null;
        clickedTag = null;
        tooltipEnabled = inputEnabled;

        DrawTopBar();
        DrawInn();
        DrawTrust();
        DrawLog();
        DrawSelectionPanel();
        DrawGuidePanel();

        tooltipEnabled = true;
        if (popup == Popup.NightLog)
        {
            DrawNightLog();
        }
        else if (game.Result != GameResult.Playing)
        {
            DrawResult();
        }
        else if (popup == Popup.Report)
        {
            DrawReport();
        }
        else if (popup == Popup.ExecutionDemand)
        {
            DrawExecutionDemand();
        }

        DrawTooltip();

        if (clickedTag != null)
        {
            OpenGuidePage(clickedTag);
        }
    }

    void EnsureStyles()
    {
        if (labelStyle != null)
        {
            return;
        }

        labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = true };
        smallStyle = new GUIStyle(labelStyle) { fontSize = 17 };
        titleStyle = new GUIStyle(labelStyle) { fontSize = 24, fontStyle = FontStyle.Bold };
        buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 19 };
        textAreaStyle = new GUIStyle(GUI.skin.textArea) { fontSize = 18, wordWrap = true };
    }

    void HandleShortcuts()
    {
        Event e = Event.current;

        // IMGUI 버튼은 포커스를 가져가지 않으므로, 메모 밖을 클릭하면 직접 포커스를 풀어 단축키가 다시 동작하게 한다.
        if (e.type == EventType.MouseDown)
        {
            GUIUtility.keyboardControl = 0;
            return;
        }

        if (e.type != EventType.KeyDown)
        {
            return;
        }

        if (GUIUtility.keyboardControl != 0)
        {
            if (e.keyCode == KeyCode.Escape)
            {
                GUIUtility.keyboardControl = 0;
                e.Use();
            }

            return;
        }

        if (e.keyCode == KeyCode.Space)
        {
            if (popup == Popup.NightLog)
            {
                AdvanceNightLog();
            }
            else if (popup == Popup.Report)
            {
                CloseReport();
            }
            else if (game.CanEndDay)
            {
                EndDay();
            }
            else if (game.CanAdvancePhase)
            {
                AdvancePhase();
            }

            e.Use();
        }
        else if (e.keyCode == KeyCode.Escape)
        {
            movingFrom = null;
            interrogating = false;
            executeConfirm = null;
            selectedRoom = null;
            ownerRoomSelected = false;
            e.Use();
        }
    }

    void EndDay()
    {
        game.EndDay();
        movingFrom = null;
        interrogating = false;
        executeConfirm = null;
        nightLogIndex = 0;
        popup = Popup.NightLog;
    }

    void AdvancePhase()
    {
        game.AdvancePhase();
        movingFrom = null;
        interrogating = false;
        executeConfirm = null;
        if (game.ExecutionDemand != null)
        {
            popup = Popup.ExecutionDemand;
        }
    }

    void AdvanceNightLog()
    {
        if (nightLogIndex < game.NightLog.Count - 1)
        {
            nightLogIndex++;
            return;
        }

        popup = game.Result == GameResult.Playing ? Popup.Report : Popup.None;
    }

    void CloseReport()
    {
        popup = game.ExecutionDemand != null ? Popup.ExecutionDemand : Popup.None;
    }

    bool Button(Rect rect, string text, bool enabled = true)
    {
        bool previous = GUI.enabled;
        GUI.enabled = inputEnabled && enabled;
        bool clicked = GUI.Button(rect, text, buttonStyle);
        GUI.enabled = previous;

        if (tooltipEnabled && rect.Contains(Event.current.mousePosition))
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (MatchTag(text, i, out string tag, out _) > 0)
                {
                    hoveredTag = tag;
                    break;
                }
            }
        }

        return clicked;
    }

    string TextArea(Rect rect, string text)
    {
        bool previous = GUI.enabled;
        GUI.enabled = inputEnabled;
        string result = GUI.TextArea(rect, text, textAreaStyle);
        GUI.enabled = previous;
        return result;
    }

    #region 태그·툴팁

    static bool IsTag(string text)
    {
        return System.Array.IndexOf(Tags, text) >= 0;
    }

    // index에서 시작하는 태그를 찾아 원문에서 차지하는 길이를 돌려준다. 없으면 0.
    // [표시 문구][원본 태그] 형식은 [표시 문구]만 화면에 보이고 원본 태그로 동작한다.
    static int MatchTag(string text, int index, out string tag, out string display)
    {
        if (text[index] == '[')
        {
            int close = text.IndexOf(']', index);
            if (close > 0 && close + 1 < text.Length && text[close + 1] == '[')
            {
                int originClose = text.IndexOf(']', close + 2);
                if (originClose > 0)
                {
                    string origin = text.Substring(close + 2, originClose - close - 2);
                    tag = IsTag($"[{origin}]") ? $"[{origin}]" : IsTag(origin) ? origin : null;
                    if (tag != null)
                    {
                        display = text.Substring(index, close - index + 1);
                        return originClose - index + 1;
                    }
                }
            }
        }

        foreach (string candidate in Tags)
        {
            if (string.CompareOrdinal(text, index, candidate, 0, candidate.Length) == 0)
            {
                tag = candidate;
                display = candidate;
                return candidate.Length;
            }
        }

        tag = null;
        display = null;
        return 0;
    }

    // 조각 단위로 그리기 위해 줄바꿈·여백 없이 한 줄로 그리는 스타일.
    GUIStyle GetInlineStyle(GUIStyle style)
    {
        if (!inlineStyles.TryGetValue(style, out GUIStyle inline))
        {
            inline = new GUIStyle(style) { wordWrap = false, padding = new RectOffset(), margin = new RectOffset(), clipping = TextClipping.Overflow };
            inlineStyles[style] = inline;
        }

        return inline;
    }

    float MeasurePiece(GUIStyle inline, string piece)
    {
        if (!pieceWidths.TryGetValue((inline.fontSize, piece), out float width))
        {
            width = inline.CalcSize(new GUIContent(piece)).x;
            pieceWidths[(inline.fontSize, piece)] = width;
        }

        return width;
    }

    // GUI.Label 대신 쓰는 라벨. 글자 위치를 알아야 태그에 커서가 올라갔는지 판정할 수 있으므로,
    // 태그와 띄어쓰기 단위 조각으로 나눠 직접 줄바꿈하며 그린다. draw가 false면 높이만 계산한다.
    float TaggedLabel(Rect rect, string text, GUIStyle style, bool draw = true)
    {
        GUIStyle inline = GetInlineStyle(style);
        float lineHeight = inline.CalcSize(new GUIContent("가")).y;
        Vector2 mouse = Event.current.mousePosition;
        float y = 0;

        foreach (string line in text.Replace("\r", "").Split('\n'))
        {
            float x = 0;
            int i = 0;
            while (i < line.Length)
            {
                int length = MatchTag(line, i, out string tag, out string piece);
                if (tag == null)
                {
                    if (line[i] == ' ')
                    {
                        length = 1;
                    }
                    else
                    {
                        length = 1;
                        while (i + length < line.Length && line[i + length] != ' ' && MatchTag(line, i + length, out _, out _) == 0)
                        {
                            length++;
                        }
                    }

                    piece = line.Substring(i, length);
                }

                i += length;
                float width = MeasurePiece(inline, piece);
                if (x > 0 && x + width > rect.width)
                {
                    x = 0;
                    y += lineHeight;
                    if (piece == " ")
                    {
                        continue;
                    }
                }

                if (draw && piece != " ")
                {
                    Rect pieceRect = new Rect(rect.x + x, rect.y + y, width, lineHeight);
                    if (tag != null)
                    {
                        Color previous = GUI.contentColor;
                        GUI.contentColor = TagColor;
                        GUI.Label(pieceRect, piece, inline);
                        GUI.contentColor = previous;
                        if (tooltipEnabled && pieceRect.Contains(mouse))
                        {
                            hoveredTag = tag;
                            // 방 칸 버튼 등이 클릭 이벤트를 먼저 소비해도 태그 클릭이 되도록 rawType으로 판정한다.
                            if (Event.current.rawType == EventType.MouseDown && Event.current.button == 0)
                            {
                                clickedTag = tag;
                            }
                        }
                    }
                    else
                    {
                        GUI.Label(pieceRect, piece, inline);
                    }
                }

                x += width;
            }

            y += lineHeight;
        }

        return y;
    }

    // 스크롤 영역 안의 라벨을 그린다. 영역 밖으로 잘린 태그에 툴팁이 뜨지 않게 커서가 영역 안에 있을 때만 판정한다.
    Vector2 TaggedScrollLabel(Rect area, Vector2 scroll, string text, GUIStyle style)
    {
        float viewWidth = area.width - 30;
        Rect view = new Rect(0, 0, viewWidth, TaggedLabel(new Rect(0, 0, viewWidth, 0), text, style, false));

        bool previous = tooltipEnabled;
        tooltipEnabled = previous && area.Contains(Event.current.mousePosition);
        scroll = GUI.BeginScrollView(area, scroll, view);
        TaggedLabel(view, text, style);
        GUI.EndScrollView();
        tooltipEnabled = previous;
        return scroll;
    }

    void DrawTooltip()
    {
        if (hoveredTag == null)
        {
            return;
        }

        // 툴팁 안의 태그는 색만 입히고, 툴팁·클릭 판정은 하지 않는다.
        tooltipEnabled = false;
        string text = $"{hoveredTag}\n{GameData.GetTooltip(hoveredTag)}\n(클릭하면 도감에서 보기)";
        float height = TaggedLabel(new Rect(0, 0, TooltipWidth - 20, 0), text, smallStyle, false) + 16;
        Vector2 mouse = Event.current.mousePosition;
        Rect rect = new Rect(Mathf.Min(mouse.x + 20, ReferenceWidth - TooltipWidth - 5), Mathf.Min(mouse.y + 20, ReferenceHeight - height - 5), TooltipWidth, height);
        GUI.Box(rect, GUIContent.none);
        GUI.Box(rect, GUIContent.none);
        TaggedLabel(new Rect(rect.x + 10, rect.y + 8, rect.width - 20, rect.height - 16), text, smallStyle);
    }

    void OpenGuidePage(string tag)
    {
        if (!showMemoTab && guideTag == tag)
        {
            return;
        }

        guideHistory.Push((showMemoTab, guideCategory, guideTag, guideScroll));
        showMemoTab = false;
        guideTag = tag;
        guideScroll = Vector2.zero;
    }

    void GoBackGuide()
    {
        if (guideHistory.Count == 0)
        {
            return;
        }

        (showMemoTab, guideCategory, guideTag, guideScroll) = guideHistory.Pop();
    }

    // 태그 설명 페이지 내용. 툴팁 설명을 먼저 보여 주고, 도감에 있는 상세 설명을 덧붙인다.
    static string BuildGuidePage(string tag)
    {
        StringBuilder text = new StringBuilder();
        TraitDefinition trait = System.Array.Find(GameData.Traits, t => t.Name == tag);
        FacilityDefinition facility = System.Array.Find(GameData.Facilities, f => f.Name == tag);
        ClueDefinition clue = System.Array.Find(GameData.Clues, c => c.Name == tag);
        SpeciesDefinition monster = System.Array.Find(GameData.Monsters, m => m.Name == tag);

        text.AppendLine(GameData.GetTooltip(tag) ?? trait?.Effect ?? facility?.Description ?? clue?.Description);
        if (trait != null)
        {
            text.AppendLine();
            text.AppendLine("[손님 특성] 괴물도 손님 특성을 1개 가지며, 그 특성대로 행동한다.");
            text.AppendLine(trait.Detail);
        }

        if (facility != null)
        {
            text.AppendLine();
            text.AppendLine("[설비] 설치한 날 밤에만 효과가 있다.");
            text.AppendLine(facility.Detail);
        }

        if (clue != null)
        {
            text.AppendLine();
            text.AppendLine($"가능한 원인: {clue.Causes}");
        }

        if (monster != null)
        {
            text.AppendLine();
            text.AppendLine($"고유 특성: {monster.Trait}");
        }

        return text.ToString();
    }

    #endregion

    #region 상단·여관

    void DrawTopBar()
    {
        GUI.Box(new Rect(0, 0, ReferenceWidth, 70), GUIContent.none);
        string status = $"{game.Day}일차 ({GameData.GetPhaseName(game.Phase)})     " +
                        $"신뢰 {game.Trust}     " +
                        $"털 단서 {game.FurClueCount}     " +
                        (game.Phase == DayPhase.Meeting ? $"반박 {game.RefutesLeft}/{GameBalance.DailyRefutes}     " : "") +
                        $"주인 방 자물쇠 {game.OwnerLocks}/{GameBalance.OwnerRoomLocks}";
        TaggedLabel(new Rect(20, 18, 1520, 40), status, labelStyle);

        if (game.Phase == DayPhase.NightPrep)
        {
            if (Button(new Rect(1560, 10, 340, 50), "밤 진행 (Space)", game.CanEndDay))
            {
                EndDay();
            }
        }
        else if (Button(new Rect(1560, 10, 340, 50),
                     game.Phase == DayPhase.Meeting ? "회의 종료 (Space)" : $"다음: {GameData.GetPhaseName(game.Phase + 1)} (Space)",
                     game.CanAdvancePhase))
        {
            AdvancePhase();
        }
    }

    static float GetFloorTop(int floor)
    {
        return InnTop + (2 - floor) * (FloorHeight + CellGap);
    }

    static Rect GetCellRect(int floor, int column)
    {
        return new Rect(InnLeft + column * (CellWidth + CellGap), GetFloorTop(floor), CellWidth, RoomHeight);
    }

    void DrawInn()
    {
        GUI.Box(new Rect(InnLeft - 10, InnTop - 10, InnColumns * (CellWidth + CellGap) + 10, 2 * (FloorHeight + CellGap) + 10), GUIContent.none);

        for (int floor = 1; floor <= 2; floor++)
        {
            float top = GetFloorTop(floor) + RoomHeight + 8;
            GUI.Box(new Rect(InnLeft, top, InnColumns * (CellWidth + CellGap) - CellGap, FloorHeight - RoomHeight - 8), GUIContent.none);
        }

        DrawOwnerRoom();
        foreach (Room room in game.Rooms)
        {
            DrawRoom(room);
        }

        DrawLobby();
    }

    // 낮에 방에서 나온 손님이 모이는 구역. 건물 아래층에 붙어 있다.
    // 손님 자리는 객실 배치와 같은 위치에 두고, 나오지 않은 손님 자리는 빈칸으로 둔다.
    void DrawLobby()
    {
        Rect area = new Rect(InnLeft - 10, LobbyTop, InnColumns * (CellWidth + CellGap) + 10, LobbyHeight);
        GUI.Box(area, GUIContent.none);
        GUI.Label(new Rect(InnLeft, LobbyTop + 10, CellWidth, LobbyHeight - 20), "로비\n대화와 집중 심문은 로비에 나온 손님하고만 할 수 있다.", labelStyle);

        Color previousColor = GUI.backgroundColor;
        foreach (Room room in game.Rooms)
        {
            Rect rect = GetLobbySlotRect(room.Floor, room.Column);
            if (room.Guest == null || !room.Guest.IsOut)
            {
                GUI.Box(rect, GUIContent.none);
                continue;
            }

            GUI.backgroundColor = room == selectedRoom ? Color.yellow : new Color(0.55f, 0.85f, 1f);
            if (Button(rect, $"{room.Guest.DisplayName} 의심{room.Guest.Suspicion}/{GameBalance.MaxSuspicion}\n({room.DisplayName})"))
            {
                OnRoomClicked(room);
            }
        }

        GUI.backgroundColor = previousColor;
    }

    static Rect GetLobbySlotRect(int floor, int column)
    {
        float slotHeight = (LobbyHeight - 30) / 2;
        return new Rect(InnLeft + column * (CellWidth + CellGap), LobbyTop + 10 + (2 - floor) * (slotHeight + CellGap), CellWidth, slotHeight);
    }

    void DrawOwnerRoom()
    {
        Rect rect = GetCellRect(InnGame.OwnerFloor, InnGame.OwnerColumn);
        if (DrawCell(rect, ownerRoomSelected, false, false))
        {
            selectedRoom = null;
            ownerRoomSelected = true;
        }

        GUI.Label(Inset(rect), $"주인 방\n자물쇠 {game.OwnerLocks}/{GameBalance.OwnerRoomLocks}", labelStyle);
    }

    void DrawRoom(Room room)
    {
        Rect rect = GetCellRect(room.Floor, room.Column);
        bool highlight = movingFrom != null && room != movingFrom;
        bool doorOpen = room.Guest != null && room.Guest.IsOut;
        if (DrawCell(rect, room == selectedRoom, highlight, doorOpen))
        {
            OnRoomClicked(room);
        }

        TaggedLabel(Inset(rect), BuildRoomText(room), labelStyle);
    }

    bool DrawCell(Rect rect, bool selected, bool highlight, bool doorOpen)
    {
        Color previousColor = GUI.backgroundColor;
        if (selected)
        {
            GUI.backgroundColor = Color.yellow;
        }
        else if (highlight)
        {
            GUI.backgroundColor = Color.green;
        }
        else if (doorOpen)
        {
            GUI.backgroundColor = new Color(0.55f, 0.85f, 1f);
        }

        bool previousEnabled = GUI.enabled;
        GUI.enabled = inputEnabled;
        bool clicked = GUI.Button(rect, GUIContent.none);
        GUI.enabled = previousEnabled;
        GUI.backgroundColor = previousColor;
        return clicked;
    }

    static Rect Inset(Rect rect)
    {
        return new Rect(rect.x + 10, rect.y + 8, rect.width - 20, rect.height - 16);
    }

    static string BuildRoomText(Room room)
    {
        StringBuilder text = new StringBuilder();
        text.AppendLine(room.DisplayName);

        if (room.Guest == null)
        {
            text.AppendLine("빈 방");
        }
        else
        {
            text.AppendLine(room.Guest.DisplayName);
            text.AppendLine($"의심 {room.Guest.Suspicion}/{GameBalance.MaxSuspicion}");
            if (room.Guest.TraitKnown)
            {
                text.AppendLine($"특성: {GameData.Traits.First(t => t.Id == room.Guest.Trait).Name}");
            }
        }

        if (room.HasFur)
        {
            text.AppendLine("[털]");
        }

        if (room.Installed.Count > 0)
        {
            text.AppendLine(string.Join(" ", room.Installed.Select(f => $"[{GameData.GetFacility(f).Name}]")));
        }

        return text.ToString();
    }

    void OnRoomClicked(Room room)
    {
        ownerRoomSelected = false;
        interrogating = false;
        executeConfirm = null;

        if (movingFrom != null)
        {
            if (room != movingFrom && game.Move(movingFrom, room))
            {
                selectedRoom = room;
            }

            movingFrom = null;
            return;
        }

        selectedRoom = room;
    }

    #endregion

    #region 신뢰·기록

    void DrawTrust()
    {
        float left = InnLeft + InnColumns * (CellWidth + CellGap) + 10;
        Rect area = new Rect(left, LobbyTop, PanelLeft - left - 20, LobbyHeight);
        GUI.Box(area, GUIContent.none);
        float x = area.x + 10;
        float y = area.y + 5;

        GUI.Label(new Rect(x, y, area.width - 20, 34), $"신뢰 {game.Trust}", titleStyle);
        y += 38;
        string costs = $"낮: 모집→회의(처형 선택) / 밤: 준비→괴물\n" +
                       $"부르기 무료 / 조사 {GameBalance.InspectCost} (털→단서)\n" +
                       $"털 단서 {game.FurClueCount} / 거짓 주장 {GameBalance.FalseClaimCost}\n" +
                       $"반박=단서 1장 (하루 {GameBalance.DailyRefutes}회) / 처형 {GameBalance.ExecuteCost}\n" +
                       string.Join("\n", GameData.Facilities.Select(f => $"{f.Name} {f.Price}"));
        TaggedLabel(new Rect(x, y, area.width - 20, area.yMax - y - 5), costs, smallStyle);
    }

    void DrawLog()
    {
        Rect area = new Rect(20, LobbyTop + LobbyHeight + 10, 1200, ReferenceHeight - LobbyTop - LobbyHeight - 20);
        GUI.Box(area, GUIContent.none);

        StringBuilder text = new StringBuilder();
        for (int i = game.Log.Count - 1; i >= 0; i--)
        {
            text.AppendLine(game.Log[i]);
        }

        logScroll = TaggedScrollLabel(new Rect(area.x + 10, area.y + 5, area.width - 15, area.height - 10), logScroll, text.ToString(), smallStyle);
    }

    #endregion

    #region 오른쪽 패널

    void DrawSelectionPanel()
    {
        Rect area = new Rect(PanelLeft, 80, PanelWidth, 560);
        GUI.Box(area, GUIContent.none);
        float x = area.x + 15;
        float y = area.y + 10;

        if (ownerRoomSelected)
        {
            GUI.Label(new Rect(x, y, 600, 34), "주인 방", titleStyle);
            GUI.Label(new Rect(x, y + 50, 620, 120),
                $"주인 방 자물쇠 {game.OwnerLocks}/{GameBalance.OwnerRoomLocks}\n모든 특성을 무시하고 공격을 막는다. 1회 막으면 파괴되며 보충할 수 없다.", labelStyle);
            return;
        }

        if (game.Phase == DayPhase.Meeting)
        {
            DrawMeetingPanel(area, x, y);
            return;
        }

        if (selectedRoom == null)
        {
            GUI.Label(new Rect(x, y, 620, 220),
                "방이나 로비의 손님을 클릭해 선택하세요.\n" +
                "낮1 모집: 부르기·조사(털은 단서로 수거)\n" +
                "낮2 회의: 주장·반박(단서)·털 제시·처형(선택)\n" +
                "밤1 준비: 설비 + 털 제거/뿌리기 예약 → 밤 진행", labelStyle);
            return;
        }

        Room room = selectedRoom;
        GUI.Label(new Rect(x, y, 600, 34), room.DisplayName, titleStyle);
        y += 45;

        if (room.Guest == null)
        {
            GUI.Label(new Rect(x, y, 300, 40), "빈 방", labelStyle);
            if (Button(new Rect(x + 210, y, 200, 40), $"방 조사 ({GameBalance.InspectCost})", game.CanInspect(room)))
            {
                game.Inspect(room);
            }

            DrawNightRoomActions(room, x, y + 50);
            return;
        }

        GUI.Label(new Rect(x, y, 620, 30),
            $"{room.Guest.DisplayName} · 의심 {room.Guest.Suspicion}/{GameBalance.MaxSuspicion}", labelStyle);
        y += 35;

        float contentTop = y;
        switch (game.Phase)
        {
            case DayPhase.Recruit:
                DrawRecruitActions(room, x, y);
                break;
            case DayPhase.NightPrep:
                DrawInstallActions(room, x, y);
                DrawReserveActions(room, x, y + 50);
                break;
        }

        y = contentTop + 100;
        if (!room.Guest.IsOut && game.Phase == DayPhase.Recruit)
        {
            GUI.Label(new Rect(x, y, 620, 30), "방에 있다. 조사하려면 로비로 부르거나 빈 방일 때 조사한다.", smallStyle);
        }

        y += 35;
        GUI.Label(new Rect(x, y + 15, 70, 30), "메모", labelStyle);
        room.Guest.Memo = TextArea(new Rect(x + 70, y, PanelWidth - 100, 60), room.Guest.Memo);
        y += 70;

        GUI.Label(new Rect(x, y, 600, 30), $"{room.Guest.DisplayName} 기록", labelStyle);
        y += 35;

        string notes = room.Guest.Notes.Count == 0 ? "아직 알아낸 것이 없다." : string.Join("\n", room.Guest.Notes);
        float viewWidth = PanelWidth - 60;
        Rect view = new Rect(0, 0, viewWidth, smallStyle.CalcHeight(new GUIContent(notes), viewWidth));
        Rect notesArea = new Rect(x, y, PanelWidth - 30, area.yMax - y - 10);
        if (!Mathf.Approximately(view.height, notesHeight))
        {
            notesHeight = view.height;
            notesScroll.y = Mathf.Max(0, view.height - notesArea.height);
        }

        notesScroll = GUI.BeginScrollView(notesArea, notesScroll, view);
        GUI.Label(view, notes, smallStyle);
        GUI.EndScrollView();
    }

    Vector2 meetingScroll;

    void DrawMeetingPanel(Rect area, float x, float y)
    {
        GUI.Label(new Rect(x, y, 620, 34),
            $"회의 · 반박 {game.RefutesLeft}/{GameBalance.DailyRefutes} · 단서 {game.HeldClues.Count}", titleStyle);
        y += 40;
        GUI.Label(new Rect(x, y, 620, 44),
            "주장 자동 채택→의심+1. 반박·털 제시는 단서 소비. 처형은 선택.", smallStyle);
        y += 48;

        if (selectedRoom?.Guest != null)
        {
            Guest guest = selectedRoom.Guest;
            GUI.Label(new Rect(x, y, 280, 28),
                $"{guest.DisplayName} · 의심 {guest.Suspicion}/{GameBalance.MaxSuspicion}", smallStyle);
            if (guest.IsOut && Button(new Rect(x + 290, y, 140, 28), "어젯밤 일", game.CanTalk(selectedRoom)))
            {
                game.Talk(selectedRoom);
            }

            if (Button(new Rect(x + 440, y, 180, 28), $"처형 ({GameBalance.ExecuteCost})", game.CanExecute(selectedRoom)))
            {
                if (executeConfirm == guest)
                {
                    executeConfirm = null;
                    game.Execute(selectedRoom);
                }
                else
                {
                    executeConfirm = guest;
                }
            }

            y += 36;
        }
        else
        {
            GUI.Label(new Rect(x, y, 620, 28), "손님을 골라 대화·처형·지목할 수 있다.", smallStyle);
            y += 32;
        }

        GUI.Label(new Rect(x, y, 620, 24),
            $"거짓 주장({GameBalance.FalseClaimCost}) / 털 제시(단서) 대상:", smallStyle);
        y += 28;
        float fx = x;
        foreach (Room room in game.Rooms.Where(r => r.Guest != null))
        {
            if (Button(new Rect(fx, y, 100, 26), $"거짓:{room.Guest.Id}", game.CanAddFalseClaim(room.Guest)))
            {
                game.AddFalseClaim(room.Guest);
            }

            if (Button(new Rect(fx, y + 28, 100, 26), $"털→#{room.Guest.Id}", game.CanPresentFurClaim(room.Guest)))
            {
                game.PresentFurClaim(room.Guest);
            }

            fx += 105;
            if (fx > area.xMax - 120)
            {
                fx = x;
                y += 58;
            }
        }

        y += 64;
        float listTop = y;
        float listHeight = area.yMax - listTop - 10;
        float contentHeight = Mathf.Max(listHeight, game.MeetingClaims.Count * 70f + 10f);
        meetingScroll = GUI.BeginScrollView(new Rect(x, listTop, PanelWidth - 30, listHeight), meetingScroll,
            new Rect(0, 0, PanelWidth - 50, contentHeight));
        float cy = 0;
        if (game.MeetingClaims.Count == 0)
        {
            GUI.Label(new Rect(0, cy, PanelWidth - 60, 40), "올라온 주장이 없다.", smallStyle);
        }

        foreach (MeetingClaim claim in game.MeetingClaims)
        {
            string mark = claim.Refuted ? "[반박됨] " : "";
            GUI.Label(new Rect(0, cy, PanelWidth - 180, 60), mark + claim.Text, smallStyle);
            if (!claim.Refuted &&
                Button(new Rect(PanelWidth - 170, cy + 10, 120, 36), "반박(단서)", game.CanRefute(claim)))
            {
                game.RefuteClaim(claim);
            }

            cy += 70;
        }

        GUI.EndScrollView();
    }

    void DrawRecruitActions(Room room, float x, float y)
    {
        if (Button(new Rect(x, y, 305, 40), "로비로 부르기", game.CanLureOut(room)))
        {
            game.LureOut(room);
        }

        if (Button(new Rect(x + 315, y, 305, 40), $"방 조사 ({GameBalance.InspectCost})", game.CanInspect(room)))
        {
            game.Inspect(room);
        }
    }

    bool DrawExecuteActions(Room room, float x, float y)
    {
        bool confirming = executeConfirm == room.Guest;
        string executeLabel = confirming
            ? "정말 처형? (다시 클릭)"
            : $"처형 ({GameBalance.ExecuteCost}, 의심 {room.Guest.Suspicion}/{GameBalance.MaxSuspicion})";
        if (Button(new Rect(x, y, 620, 40), executeLabel, game.CanExecute(room)))
        {
            if (confirming)
            {
                executeConfirm = null;
                game.Execute(room);
                return true;
            }

            executeConfirm = room.Guest;
        }

        return false;
    }

    void DrawInstallActions(Room room, float x, float y)
    {
        float buttonX = x;
        foreach (FacilityDefinition facility in GameData.Facilities)
        {
            if (Button(new Rect(buttonX, y, 200, 40), $"{facility.Name} ({facility.Price})", game.CanInstall(room, facility.Id)))
            {
                game.Install(room, facility.Id);
            }

            buttonX += 210;
        }
    }

    void DrawReserveActions(Room room, float x, float y)
    {
        string removeLabel = room.ReserveRemoveFur ? "털 제거 예약됨" : "털 제거 예약";
        if (Button(new Rect(x, y, 305, 40), removeLabel, game.CanReserveRemoveFur(room)))
        {
            game.ReserveRemoveFur(room);
        }

        string plantLabel = room.ReservePlantFur ? "털 뿌리기 예약됨" : $"털 뿌리기 ({game.FurClueCount})";
        if (Button(new Rect(x + 315, y, 305, 40), plantLabel, game.CanReservePlantFur(room)))
        {
            game.ReservePlantFur(room);
        }
    }

    void DrawNightRoomActions(Room room, float x, float y)
    {
        if (game.Phase == DayPhase.NightPrep)
        {
            DrawReserveActions(room, x, y);
        }
    }

    // 메모·기록 자리에 특성 버튼과 맞히기/검증 전환 버튼을 대신 보여 준다.
    void DrawInterrogation(Room room, float x, float y)
    {
        string question = verifying
            ? $"{room.Guest.DisplayName}이(가) 이 특성을 가졌는지 검증한다. (신뢰 -{GameBalance.VerifyCost})"
            : $"{room.Guest.DisplayName}의 특성은? (맞히면 신뢰 +{GameBalance.GuessReward}, 틀리면 -{GameBalance.GuessPenalty})";
        GUI.Label(new Rect(x, y, 620, 30), question, labelStyle);
        y += 40;

        for (int i = 0; i < GameData.Traits.Length; i++)
        {
            TraitDefinition trait = GameData.Traits[i];
            if (Button(new Rect(x + i % 3 * 210, y + i / 3 * 45, 200, 40), trait.Name, !verifying || game.CanVerifyTrait(room)))
            {
                interrogating = false;
                if (verifying)
                {
                    game.VerifyTrait(room, trait.Id);
                }
                else
                {
                    game.GuessTrait(room, trait.Id);
                }
            }
        }

        y += (GameData.Traits.Length + 2) / 3 * 45 + 5;
        string modeLabel = verifying ? "맞히기로 전환" : $"검증으로 전환 ({GameBalance.VerifyCost})";
        if (Button(new Rect(x, y, 305, 40), modeLabel, verifying || game.CanVerifyTrait(room)))
        {
            verifying = !verifying;
        }

        if (Button(new Rect(x + 315, y, 305, 40), "취소"))
        {
            interrogating = false;
        }
    }

    void DrawGuidePanel()
    {
        Rect area = new Rect(PanelLeft, 650, PanelWidth, 420);
        GUI.Box(area, GUIContent.none);

        if (Button(new Rect(area.x + 15, area.y + 8, 150, 36), "도감", showMemoTab))
        {
            showMemoTab = false;
        }

        if (Button(new Rect(area.x + 175, area.y + 8, 150, 36), "자유 메모", !showMemoTab))
        {
            showMemoTab = true;
        }

        Rect content = new Rect(area.x + 15, area.y + 52, PanelWidth - 30, area.height - 62);
        if (showMemoTab)
        {
            float memoWidth = content.width - 25;
            float memoHeight = Mathf.Max(content.height, textAreaStyle.CalcHeight(new GUIContent(game.Memo), memoWidth) + 10);
            memoScroll = GUI.BeginScrollView(content, memoScroll, new Rect(0, 0, memoWidth, memoHeight));
            game.Memo = TextArea(new Rect(0, 0, memoWidth, memoHeight), game.Memo);
            GUI.EndScrollView();
            return;
        }

        string[] categoryNames = { "괴물", "특성", "설비", "단서" };
        for (int i = 0; i < categoryNames.Length; i++)
        {
            GuideCategory category = (GuideCategory)i;
            if (Button(new Rect(area.x + 340 + i * 75, area.y + 8, 70, 36), categoryNames[i], showMemoTab || guideTag != null || guideCategory != category))
            {
                showMemoTab = false;
                guideCategory = category;
                guideTag = null;
                guideHistory.Clear();
                guideScroll = Vector2.zero;
            }
        }

        if (guideTag != null)
        {
            if (Button(new Rect(content.x, content.y, 120, 36), "< 뒤로", guideHistory.Count > 0))
            {
                GoBackGuide();
                return;
            }

            Color previousColor = GUI.contentColor;
            GUI.contentColor = TagColor;
            GUI.Label(new Rect(content.x + 135, content.y + 2, content.width - 135, 36), guideTag, titleStyle);
            GUI.contentColor = previousColor;

            Rect pageArea = new Rect(content.x, content.y + 46, content.width, content.height - 46);
            guideScroll = TaggedScrollLabel(pageArea, guideScroll, BuildGuidePage(guideTag), smallStyle);
            return;
        }

        StringBuilder text = new StringBuilder();
        switch (guideCategory)
        {
            case GuideCategory.Monster:
                SpeciesDefinition monster = GameData.GetSpecies(game.RunSpecies);
                text.AppendLine($"[이번 괴물] {monster.Name}");
                string monsterTooltip = GameData.GetTooltip(monster.Name);
                if (monsterTooltip != null)
                {
                    text.AppendLine($"  {monsterTooltip}");
                }

                text.AppendLine($"  고유 특성: {monster.Trait}");
                text.AppendLine("  괴물도 손님 특성을 1개 가지며, 그 특성대로 행동한다.");
                text.AppendLine();
                text.AppendLine("[정보원에게 들은 패턴]");
                if (game.RevealedFragments.Count == 0)
                {
                    text.AppendLine("  아직 없음");
                }

                foreach (string fragment in game.RevealedFragments)
                {
                    text.AppendLine($"  - {fragment}");
                }
                break;

            case GuideCategory.Trait:
                text.AppendLine("모든 손님에게 1개씩 있다.");
                foreach (TraitDefinition trait in GameData.Traits)
                {
                    text.AppendLine();
                    text.AppendLine($"[{trait.Name}] {GameData.GetTooltip(trait.Name) ?? trait.Effect}");
                    text.AppendLine(trait.Detail);
                }
                break;

            case GuideCategory.Facility:
                text.AppendLine("설치한 날 밤에만 효과가 있다.");
                foreach (FacilityDefinition facility in GameData.Facilities)
                {
                    text.AppendLine();
                    text.AppendLine($"[{facility.Name}] {GameData.GetTooltip(facility.Name) ?? facility.Description}");
                    text.AppendLine(facility.Detail);
                }
                break;

            case GuideCategory.Clue:
                text.AppendLine("게임 중 벌어지는 일이 어떤 단서로 드러나는지, 원인이 무엇일 수 있는지 정리한 것이다.");
                foreach (ClueDefinition clue in GameData.Clues)
                {
                    text.AppendLine();
                    text.AppendLine(clue.Name);
                    text.AppendLine(GameData.GetTooltip(clue.Name) ?? clue.Description);
                    text.AppendLine($"가능한 원인: {clue.Causes}");
                }
                break;
        }

        guideScroll = TaggedScrollLabel(content, guideScroll, text.ToString(), smallStyle);
    }

    #endregion

    #region 팝업

    void DrawNightLog()
    {
        Rect area = new Rect(560, 220, 800, 560);
        GUI.Box(area, GUIContent.none);
        GUI.Box(area, GUIContent.none);
        GUI.Label(new Rect(area.x + 20, area.y + 15, 760, 40), $"{game.Day}일차 밤", titleStyle);

        string text = game.NightLog.Count == 0
            ? "아무 소리도 들리지 않는 밤이었다."
            : string.Join("\n", game.NightLog.Take(nightLogIndex + 1));
        TaggedLabel(new Rect(area.x + 20, area.y + 70, 760, 400), text, labelStyle);

        bool last = nightLogIndex >= game.NightLog.Count - 1;
        if (GUI.Button(new Rect(area.x + 275, area.yMax - 70, 250, 50), last ? "아침으로 (Space)" : "다음 (Space)", buttonStyle))
        {
            AdvanceNightLog();
        }
    }

    void DrawReport()
    {
        Rect area = new Rect(560, 220, 800, 560);
        GUI.Box(area, GUIContent.none);
        GUI.Box(area, GUIContent.none);
        GUI.Label(new Rect(area.x + 20, area.y + 15, 760, 40), $"{game.Day}일차 아침", titleStyle);

        string text = game.MorningReport.Count == 0 ? "별일 없는 아침이다." : string.Join("\n", game.MorningReport);
        TaggedLabel(new Rect(area.x + 20, area.y + 70, 760, 400), text, labelStyle);

        if (GUI.Button(new Rect(area.x + 275, area.yMax - 70, 250, 50), "확인 (Space)", buttonStyle))
        {
            CloseReport();
        }
    }

    void DrawExecutionDemand()
    {
        Rect area = new Rect(560, 220, 800, 560);
        GUI.Box(area, GUIContent.none);
        GUI.Box(area, GUIContent.none);
        Guest demand = game.ExecutionDemand;
        string name = demand != null ? demand.DisplayName : "?";
        GUI.Label(new Rect(area.x + 20, area.y + 15, 760, 40), "처형 요구", titleStyle);
        TaggedLabel(
            new Rect(area.x + 20, area.y + 70, 760, 300),
            $"손님들이 {name}의 처형을 요구한다.\n의심 {demand?.Suspicion ?? 0}/{GameBalance.MaxSuspicion}\n\n" +
            $"수락: 신뢰 {GameBalance.ExecuteCost}을(를) 쓰고 처형한다.\n" +
            $"거부: 신뢰 -{GameBalance.RejectDemandPenalty}",
            labelStyle);

        bool canAccept = game.CanAcceptExecutionDemand();
        bool previous = GUI.enabled;
        GUI.enabled = canAccept;
        if (GUI.Button(new Rect(area.x + 120, area.yMax - 70, 250, 50), canAccept ? "수락·처형" : "신뢰 부족", buttonStyle))
        {
            game.AcceptExecutionDemand();
            popup = Popup.None;
        }

        GUI.enabled = previous;
        if (GUI.Button(new Rect(area.x + 430, area.yMax - 70, 250, 50), "거부", buttonStyle))
        {
            game.RejectExecutionDemand();
            popup = Popup.None;
        }
    }

    void DrawResult()
    {
        Rect area = new Rect(360, 80, 1200, 920);
        GUI.Box(area, GUIContent.none);
        GUI.Box(area, GUIContent.none);
        string title = game.Result == GameResult.Won ? "승리" : "패배";
        GUI.Label(new Rect(area.x + 20, area.y + 15, 1160, 40), $"{title} - {game.ResultMessage}", titleStyle);

        string reveal = game.BuildRevealSummary();
        float viewWidth = 1140f;
        float viewHeight = Mathf.Max(700f, smallStyle.CalcHeight(new GUIContent(reveal), viewWidth) + 20f);
        Rect scrollArea = new Rect(area.x + 20, area.y + 60, 1160, area.height - 150);
        resultScroll = GUI.BeginScrollView(scrollArea, resultScroll, new Rect(0, 0, viewWidth, viewHeight));
        GUI.Label(new Rect(0, 0, viewWidth, viewHeight), reveal, smallStyle);
        GUI.EndScrollView();

        if (GUI.Button(new Rect(area.x + 475, area.yMax - 70, 250, 50), "다시 시작", buttonStyle))
        {
            resultScroll = Vector2.zero;
            StartNewGame();
        }
    }

    #endregion
}
