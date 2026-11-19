using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class InnGameView : MonoBehaviour
{
    enum AppScreen
    {
        Title,
        Difficulty,
        Playing
    }

    enum Popup
    {
        None,
        NightLog,
        Report,
        Help
    }

    enum GuideCategory
    {
        Monster,
        Job,
        Facility,
        Clue
    }

    const float ReferenceWidth = 1920f;
    const float ReferenceHeight = 1080f;
    // 월드 공간 건물(객실·로비) 아래에 두는 기록 영역.
    const float LogTop = 770f;
    const float PanelLeft = 1240f;
    const float PanelWidth = 660f;
    const float TooltipWidth = 420f;
    static readonly Color TagColor = new Color(1f, 0.85f, 0.4f);

    // 긴 태그가 짧은 태그보다 먼저 맞춰지도록 길이 내림차순으로 둔다.
    static readonly string[] Tags = GameData.Tooltips.Select(t => t.Tag).OrderByDescending(t => t.Length).ToArray();

    InnGame game;
    AppScreen screen = AppScreen.Title;
    Room selectedRoom;
    bool ownerRoomSelected;
    // 로비 칸을 선택한 상태. 로비 목록에서 손님을 고르면 selectedRoom도 함께 잡힌다.
    bool lobbySelected;
    // 오른쪽 패널이 도감/메모 대신 선택 대상(방·로비·주인 방)의 행동을 보여 주는지.
    bool showSelectionTab;
    // 직업 행동 대상(방/손님)을 고르는 중인지.
    bool ordering;
    Guest orderFrom;
    NightOrder orderType;
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
    // 월드 공간 방 셀. 없으면 Resources/Prefabs/InnBuilding 또는 씬에 있는 InnBuildingView를 쓴다.
    InnBuildingView building;

    GUIStyle labelStyle;
    GUIStyle smallStyle;
    GUIStyle titleStyle;
    GUIStyle heroStyle;
    GUIStyle buttonStyle;
    GUIStyle textAreaStyle;
    readonly Dictionary<GUIStyle, GUIStyle> inlineStyles = new Dictionary<GUIStyle, GUIStyle>();
    readonly Dictionary<(int, string), float> pieceWidths = new Dictionary<(int, string), float>();

    // 씬 설정 없이 플레이하면 바로 게임이 뜨도록 한다. 순수 방 셀 데모만 있을 때는 끼어들지 않는다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindAnyObjectByType<InnGameView>() != null ||
            FindAnyObjectByType<InnRoomDemoBootstrap>() != null)
        {
            return;
        }

        new GameObject("InnGame").AddComponent<InnGameView>();
    }

    void Awake()
    {
        EnsureBuilding();
        ShowTitle();
    }

    void OnDestroy()
    {
        if (building != null)
        {
            building.RoomClicked -= OnBuildingRoomClicked;
            building.LobbyClicked -= OnBuildingLobbyClicked;
        }
    }

    void ShowTitle()
    {
        screen = AppScreen.Title;
        game = null;
        popup = Popup.None;
        ClearSelectionState();
        if (building != null)
        {
            building.gameObject.SetActive(false);
        }
    }

    void ShowDifficulty()
    {
        screen = AppScreen.Difficulty;
        popup = Popup.None;
        ClearSelectionState();
        if (building != null)
        {
            building.gameObject.SetActive(false);
        }
    }

    void StartNewGame(Difficulty difficulty)
    {
        screen = AppScreen.Playing;
        game = new InnGame(difficulty);
        ClearSelectionState();
        popup = Popup.Report;
        if (building != null)
        {
            building.gameObject.SetActive(true);
        }

        RebuildBuilding();
        FitCameraToBuilding();
    }

    void ClearSelectionState()
    {
        selectedRoom = null;
        ownerRoomSelected = false;
        lobbySelected = false;
        showSelectionTab = false;
        ordering = false;
        orderFrom = null;
        executeConfirm = null;
    }

    void EnsureBuilding()
    {
        if (building != null)
        {
            return;
        }

        building = FindAnyObjectByType<InnBuildingView>();
        if (building == null)
        {
            GameObject prefab = Resources.Load<GameObject>("Prefabs/InnBuilding");
            if (prefab != null)
            {
                building = Instantiate(prefab).GetComponent<InnBuildingView>();
                building.name = "InnBuilding";
            }
        }

        if (building == null)
        {
            Debug.LogError("InnBuilding 프리팹을 찾지 못했습니다. Resources/Prefabs/InnBuilding 이 필요합니다.");
            return;
        }

        // 건물(객실 + 0층 로비)이 화면 상단~중단, 기록 영역이 그 아래 오도록 위치를 맞춘다.
        building.transform.position = new Vector3(0.25f, 1.55f, 0f);
        building.RoomClicked -= OnBuildingRoomClicked;
        building.RoomClicked += OnBuildingRoomClicked;
        building.LobbyClicked -= OnBuildingLobbyClicked;
        building.LobbyClicked += OnBuildingLobbyClicked;
        EnsureInnCamera();
        building.gameObject.SetActive(false);
    }

    void RebuildBuilding()
    {
        if (building == null || game == null)
        {
            return;
        }

        building.Build(game.Rooms, game.OwnerLocks, GameBalance.OwnerRoomLocks);
        SyncBuildingSelection();
    }

    void RefreshBuilding()
    {
        if (building == null || game == null)
        {
            return;
        }

        building.Refresh(game.Rooms, game.OwnerLocks, GameBalance.OwnerRoomLocks);
        building.SetOrderHighlight(orderFrom, ordering);
        SyncBuildingSelection();
    }

    void SyncBuildingSelection()
    {
        if (building == null)
        {
            return;
        }

        if (ownerRoomSelected)
        {
            building.SelectOwner();
        }
        else if (selectedRoom != null)
        {
            building.SelectRoom(selectedRoom);
        }
        else if (lobbySelected)
        {
            building.SelectLobby();
        }
        else
        {
            building.Select(null);
        }
    }

    void OnBuildingRoomClicked(RoomCellView cell)
    {
        // OnMouseDown은 OnGUI보다 먼저 올 수 있어 popup/결과만으로 막는다.
        if (popup != Popup.None || game.Result != GameResult.Playing)
        {
            return;
        }

        showSelectionTab = true;
        if (cell.IsOwnerRoom)
        {
            selectedRoom = null;
            lobbySelected = false;
            ownerRoomSelected = true;
            executeConfirm = null;
            ordering = false;
            orderFrom = null;
            SyncBuildingSelection();
            return;
        }

        if (cell.BoundRoom != null)
        {
            lobbySelected = false;
            OnRoomClicked(cell.BoundRoom);
            SyncBuildingSelection();
        }
    }

    void OnBuildingLobbyClicked(LobbyCellView lobby)
    {
        if (popup != Popup.None || game.Result != GameResult.Playing)
        {
            return;
        }

        showSelectionTab = true;
        selectedRoom = null;
        ownerRoomSelected = false;
        lobbySelected = true;
        executeConfirm = null;
        SyncBuildingSelection();
    }

    static void EnsureInnCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject go = new GameObject("Main Camera");
            cam = go.AddComponent<Camera>();
            go.tag = "MainCamera";
            go.AddComponent<AudioListener>();
        }

        cam.orthographic = true;
        // 기본(2×2) 배치 기준. 난이도에 따라 FitCameraToBuilding에서 다시 맞춘다.
        cam.orthographicSize = 5.4f;
        cam.transform.position = new Vector3(8.3f, 1.1f, -10f);
        cam.backgroundColor = new Color(0.07f, 0.05f, 0.04f);
        cam.clearFlags = CameraClearFlags.SolidColor;
    }

    void FitCameraToBuilding()
    {
        Camera cam = Camera.main;
        if (cam == null || building == null)
        {
            return;
        }

        // 상단 바 아래~기록 영역 위, 오른쪽 패널 왼쪽에 건물이 들어오게 맞춤.
        const float topGui = 90f;
        const float bottomGui = LogTop - 35f;
        const float leftGui = 40f;
        float rightGui = PanelLeft - 20f;

        Vector3 origin = building.transform.position;
        float lobbyBottom = origin.y - InnBuildingView.FloorStride;
        float buildingTop = origin.y + (building.MaxFloor - 1) * InnBuildingView.FloorStride + RoomCellView.CellHeight;
        float buildingLeft = origin.x;
        float buildingRight = origin.x + building.LobbyWidth;

        float sizeY = (buildingTop - lobbyBottom) * ReferenceHeight / (2f * (bottomGui - topGui));
        float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
        float sizeX = (buildingRight - buildingLeft) * ReferenceWidth / (2f * aspect * (rightGui - leftGui));
        float size = Mathf.Max(sizeY, sizeX);

        float cy = lobbyBottom - size * (1f - 2f * bottomGui / ReferenceHeight);
        float halfW = size * aspect;
        float buildingCenterX = (buildingLeft + buildingRight) * 0.5f;
        float contentCenterGui = (leftGui + rightGui) * 0.5f;
        float cx = buildingCenterX - halfW * (2f * contentCenterGui / ReferenceWidth - 1f);

        cam.orthographicSize = size;
        cam.transform.position = new Vector3(cx, cy, -10f);
    }

    void OnGUI()
    {
        EnsureStyles();
        GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width / ReferenceWidth, Screen.height / ReferenceHeight, 1f));

        HandleShortcuts();
        // IMGUI 텍스트 입력은 IME가 자동으로 켜지지 않아 한글 조합이 안 되므로, 입력 중일 때만 강제로 켠다.
        Input.imeCompositionMode = GUIUtility.keyboardControl != 0 ? IMECompositionMode.On : IMECompositionMode.Auto;
        hoveredTag = null;
        clickedTag = null;

        if (screen == AppScreen.Title)
        {
            inputEnabled = true;
            tooltipEnabled = false;
            DrawTitleScreen();
            return;
        }

        if (screen == AppScreen.Difficulty)
        {
            inputEnabled = true;
            tooltipEnabled = false;
            DrawDifficultyScreen();
            return;
        }

        inputEnabled = popup == Popup.None && game.Result == GameResult.Playing;
        tooltipEnabled = inputEnabled;

        DrawTopBar();
        RefreshBuilding();
        DrawLog();
        DrawRightPanel();

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
        else if (popup == Popup.Help)
        {
            DrawHelp();
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
        heroStyle = new GUIStyle(labelStyle)
        {
            fontSize = 54,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 19 };
        textAreaStyle = new GUIStyle(GUI.skin.textArea) { fontSize = 18, wordWrap = true };
        ApplyButtonArt();
    }

    // Resources/UI 의 9-slice 텍스처를 버튼 스타일에 입힌다. 없으면 기본 스킨을 그대로 쓴다.
    void ApplyButtonArt()
    {
        Texture2D normal = Resources.Load<Texture2D>("UI/button_normal");
        Texture2D hover = Resources.Load<Texture2D>("UI/button_hover");
        Texture2D active = Resources.Load<Texture2D>("UI/button_active");
        if (normal == null)
        {
            return;
        }

        Color text = new Color(0.9f, 0.82f, 0.65f);
        Color textHover = new Color(1f, 0.93f, 0.75f);
        buttonStyle.border = new RectOffset(12, 12, 12, 12);
        buttonStyle.padding = new RectOffset(10, 10, 6, 6);
        buttonStyle.normal.background = normal;
        buttonStyle.normal.textColor = text;
        buttonStyle.hover.background = hover != null ? hover : normal;
        buttonStyle.hover.textColor = textHover;
        buttonStyle.active.background = active != null ? active : normal;
        buttonStyle.active.textColor = textHover;
        buttonStyle.focused.background = normal;
        buttonStyle.focused.textColor = text;
        buttonStyle.onNormal.background = normal;
        buttonStyle.onNormal.textColor = text;
        buttonStyle.onHover.background = hover != null ? hover : normal;
        buttonStyle.onHover.textColor = textHover;
        buttonStyle.onActive.background = active != null ? active : normal;
        buttonStyle.onActive.textColor = textHover;
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

        if (screen == AppScreen.Title)
        {
            if (e.keyCode == KeyCode.Space || e.keyCode == KeyCode.Return)
            {
                ShowDifficulty();
                e.Use();
            }

            return;
        }

        if (screen == AppScreen.Difficulty)
        {
            if (e.keyCode == KeyCode.Escape)
            {
                ShowTitle();
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
            else if (popup == Popup.Report || popup == Popup.Help)
            {
                popup = Popup.None;
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
            if (popup == Popup.Help)
            {
                popup = Popup.None;
                e.Use();
                return;
            }

            ordering = false;
            orderFrom = null;
            executeConfirm = null;
            selectedRoom = null;
            ownerRoomSelected = false;
            lobbySelected = false;
            e.Use();
        }
    }

    void EndDay()
    {
        game.EndDay();
        ordering = false;
        orderFrom = null;
        executeConfirm = null;
        nightLogIndex = 0;
        popup = Popup.NightLog;
    }

    void AdvancePhase()
    {
        game.AdvancePhase();
        ordering = false;
        orderFrom = null;
        executeConfirm = null;
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
        if (!showSelectionTab && !showMemoTab && guideTag == tag)
        {
            return;
        }

        guideHistory.Push((showMemoTab, guideCategory, guideTag, guideScroll));
        showSelectionTab = false;
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
        JobDefinition job = System.Array.Find(GameData.Jobs, j => j.Name == tag);
        FacilityDefinition facility = System.Array.Find(GameData.Facilities, f => f.Name == tag);
        ClueDefinition clue = System.Array.Find(GameData.Clues, c => c.Name == tag);
        SpeciesDefinition monster = System.Array.Find(GameData.Monsters, m => m.Name == tag);

        text.AppendLine(GameData.GetTooltip(tag) ?? job?.Feature ?? facility?.Description ?? clue?.Description);
        if (job != null)
        {
            text.AppendLine();
            text.AppendLine($"[손님 직업 · {GameData.GetCategoryName(job.Category)}]");
            text.AppendLine(job.Feature);
            text.AppendLine($"요청: {job.Request}");
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
            text.AppendLine($"공통 특성: {monster.Trait}");
            text.AppendLine("회차 예고·표적은 헌터 정보로만 공개된다.");
        }

        OmenTraitDefinition omen = System.Array.Find(GameData.OmenTraits, o => o.Name == tag);
        if (omen != null)
        {
            text.AppendLine();
            text.AppendLine($"[예고 특성] {omen.Effect}");
        }

        TargetRuleDefinition target = System.Array.Find(GameData.TargetRules, t => t.Name == tag);
        if (target != null)
        {
            text.AppendLine();
            text.AppendLine($"[표적 특성] {target.Effect}");
        }

        return text.ToString();
    }

    #endregion

    #region 상단·여관

    void DrawTopBar()
    {
        GUI.Box(new Rect(0, 0, ReferenceWidth, 70), GUIContent.none);
        string status = $"{game.Day}일차 / {GameBalance.TargetDay}일 ({GameData.GetPhaseName(game.Phase)})     " +
                        $"직업 행동 {game.JobActionsLeft}/{GameBalance.DailyJobActions}     " +
                        $"처형 {game.ExecutionsLeft}/{GameBalance.DailyExecutions}";
        TaggedLabel(new Rect(20, 18, 1380, 40), status, labelStyle);

        // 튜토리얼 안내는 상시 표시하지 않고 버튼으로만 연다.
        if (Button(new Rect(1410, 10, 130, 50), "도움말"))
        {
            popup = Popup.Help;
        }

        if (game.Phase == DayPhase.Focus)
        {
            if (Button(new Rect(1560, 10, 340, 50), "밤으로 (Space)", game.CanEndDay))
            {
                EndDay();
            }
        }
        else if (Button(new Rect(1560, 10, 340, 50), $"다음: {GameData.GetPhaseName(DayPhase.Focus)} (Space)", game.CanAdvancePhase))
        {
            AdvancePhase();
        }
    }

    void OnRoomClicked(Room room)
    {
        ownerRoomSelected = false;
        executeConfirm = null;

        if (ordering && orderFrom != null)
        {
            ApplyOrderTarget(room);
            return;
        }

        selectedRoom = room;
        if (game.CanHearClaim(room))
        {
            game.HearClaim(room);
        }
    }

    void ApplyOrderTarget(Room room)
    {
        Guest actor = orderFrom;
        switch (orderType)
        {
            case NightOrder.LockRoom:
                game.SetOrderLock(actor, room);
                break;
            case NightOrder.PerfumeRoom:
                game.SetOrderPerfume(actor, room);
                break;
            case NightOrder.WatchRoom:
                game.SetOrderWatch(actor, room);
                break;
            case NightOrder.CleanRoom:
                game.SetOrderClean(actor, room);
                break;
            case NightOrder.RememberGuest:
                if (room.Guest != null)
                {
                    game.SetOrderRemember(actor, room.Guest);
                }
                break;
        }

        ordering = false;
        orderFrom = null;
        selectedRoom = FindGuestRoom(actor) ?? room;
    }

    Room FindGuestRoom(Guest guest)
    {
        return game.Rooms.FirstOrDefault(r => r.Guest == guest);
    }

    #endregion

    #region 힌트·기록

    void DrawLog()
    {
        Rect area = new Rect(20, LogTop, PanelLeft - 40, ReferenceHeight - LogTop - 10);
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

    // 오른쪽에는 패널 하나만 둔다. 탭으로 선택 대상 행동 / 도감 / 자유 메모를 번갈아 보여 준다.
    void DrawRightPanel()
    {
        Rect area = new Rect(PanelLeft, 80, PanelWidth, ReferenceHeight - 90);
        GUI.Box(area, GUIContent.none);

        if (Button(new Rect(area.x + 15, area.y + 8, 100, 36), "선택", !showSelectionTab))
        {
            showSelectionTab = true;
        }

        if (Button(new Rect(area.x + 120, area.y + 8, 100, 36), "도감", showSelectionTab || showMemoTab))
        {
            showSelectionTab = false;
            showMemoTab = false;
        }

        if (Button(new Rect(area.x + 225, area.y + 8, 100, 36), "자유 메모", showSelectionTab || !showMemoTab))
        {
            showSelectionTab = false;
            showMemoTab = true;
        }

        if (showSelectionTab)
        {
            DrawSelectionPanel(area);
        }
        else
        {
            DrawGuidePanel(area);
        }
    }

    void DrawSelectionPanel(Rect area)
    {
        float x = area.x + 15;
        float y = area.y + 56;

        if (ownerRoomSelected)
        {
            return;
        }

        if (lobbySelected && selectedRoom == null)
        {
            DrawLobbyPanel(x, y);
            return;
        }

        if (selectedRoom == null)
        {
            GUI.Label(new Rect(x, y, 620, 80), "객실·로비 방을 클릭해 선택한다.\n자세한 진행 방법은 상단 [도움말]에서 확인할 수 있다.", labelStyle);
            return;
        }

        Room room = selectedRoom;
        if (lobbySelected)
        {
            // 로비 목록에서 고른 손님. 돌아가기로 로비 목록을 다시 연다.
            if (Button(new Rect(area.xMax - 135, y, 120, 34), "< 로비"))
            {
                selectedRoom = null;
                executeConfirm = null;
                return;
            }
        }

        GUI.Label(new Rect(x, y, 480, 34), room.DisplayName, titleStyle);
        y += 45;

        if (room.Guest == null)
        {
            GUI.Label(new Rect(x, y, 500, 40), room.DoorLocked ? "빈 방 (잠김)" : "빈 방", labelStyle);
            if (game.Phase == DayPhase.Gather)
            {
                string inspectLabel = room.DoorLocked ? "조사 (잠김)" : "조사 (하루 1방)";
                if (Button(new Rect(x, y + 50, 305, 40), inspectLabel, game.CanInspect(room)))
                {
                    game.Inspect(room);
                }
            }

            return;
        }

        float contentTop = y;
        if (game.Phase == DayPhase.Gather)
        {
            DrawGatherActions(room, x, y);
        }
        else if (DrawFocusActions(room, x, y))
        {
            return;
        }

        y = contentTop + 150;
        if (ordering && orderFrom == room.Guest)
        {
            GUI.Label(new Rect(x, y, 620, 30), "직업 행동 대상을 클릭한다. (Esc 취소)", smallStyle);
        }
        else if (!room.Guest.IsOut && game.Phase == DayPhase.Focus)
        {
            GUI.Label(new Rect(x, y, 620, 30), "손님이 방에 있다. 어젯밤 일을 들으려면 로비로 불러야 한다.", smallStyle);
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

    // 로비 칸을 선택했을 때. 로비에 나온 손님 목록을 보여 주고, 고르면 그 손님의 행동 패널로 넘어간다.
    void DrawLobbyPanel(float x, float y)
    {
        GUI.Label(new Rect(x, y, 600, 34), "로비", titleStyle);
        y += 45;
        GUI.Label(new Rect(x, y, 620, 50), "대화는 로비에 나온 손님하고만 할 수 있다.", smallStyle);
        y += 55;

        List<Room> outRooms = game.Rooms.Where(r => r.Guest != null && r.Guest.IsOut).ToList();
        if (outRooms.Count == 0)
        {
            GUI.Label(new Rect(x, y, 620, 40), "로비에 아무도 없다. 손님을 로비로 부를 수 있다.", labelStyle);
            return;
        }

        foreach (Room room in outRooms)
        {
            if (Button(new Rect(x, y, PanelWidth - 30, 44), $"{room.Guest.DisplayName} ({room.DisplayName})"))
            {
                OnRoomClicked(room);
                SyncBuildingSelection();
            }

            y += 50;
        }
    }

    void DrawGatherActions(Room room, float x, float y)
    {
        if (Button(new Rect(x, y, 305, 40), "로비로 부르기", game.CanLureOut(room)))
        {
            game.LureOut(room);
        }

        string inspectLabel = room.DoorLocked ? "조사 (잠김)" : "조사 (하루 1방)";
        if (Button(new Rect(x + 315, y, 305, 40), inspectLabel, game.CanInspect(room)))
        {
            game.Inspect(room);
        }
    }

    // 처형으로 손님이 사라지면 true를 돌려 나머지 그리기를 멈춘다.
    bool DrawFocusActions(Room room, float x, float y)
    {
        if (Button(new Rect(x, y, 200, 40), "어젯밤 일", game.CanTalk(room)))
        {
            game.Talk(room);
        }

        DrawJobOrderButtons(room, x + 210, y);

        bool confirming = executeConfirm == room.Guest;
        if (Button(new Rect(x + 430, y, 200, 40), confirming ? "정말로 처형한다?" : $"처형 ({game.ExecutionsLeft})", game.CanExecute(room)))
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

    void DrawJobOrderButtons(Room room, float x, float y)
    {
        Job? requestable = game.GetRequestableJob(room.Guest);
        if (requestable == null)
        {
            if (room.Guest.ClaimHeard && room.Guest.ClaimedJob == Job.Scout)
            {
                GUI.Label(new Rect(x, y, 210, 40), "정찰 보고", smallStyle);
            }
            else if (!room.Guest.ClaimHeard)
            {
                GUI.Label(new Rect(x, y, 210, 40), "직업 미청취", smallStyle);
            }

            return;
        }

        Job job = requestable.Value;
        string label = room.Guest.PendingOrder == NightOrder.None
            ? $"행동:{GameData.GetJob(job).Name} ({game.JobActionsLeft})"
            : "행동 취소";
        if (!Button(new Rect(x, y, 210, 40), label, game.CanOrder(room) || room.Guest.PendingOrder != NightOrder.None))
        {
            return;
        }

        if (room.Guest.PendingOrder != NightOrder.None)
        {
            game.ClearOrder(room.Guest);
            ordering = false;
            orderFrom = null;
            return;
        }

        if (job == Job.Informant)
        {
            game.SetOrderInformant(room.Guest);
            return;
        }

        ordering = true;
        orderFrom = room.Guest;
        switch (job)
        {
            case Job.Locksmith:
                orderType = NightOrder.LockRoom;
                break;
            case Job.Pharmacist:
                orderType = NightOrder.PerfumeRoom;
                break;
            case Job.DogHandler:
                orderType = NightOrder.WatchRoom;
                break;
            case Job.Cleaner:
                orderType = NightOrder.CleanRoom;
                break;
            case Job.Butler:
                orderType = NightOrder.RememberGuest;
                break;
        }
    }

    void DrawGuidePanel(Rect area)
    {
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

        string[] categoryNames = { "괴물", "직업", "설비", "단서" };
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

                text.AppendLine($"  공통 특성: {monster.Trait}");
                text.AppendLine("  괴물도 손님 직업 1개를 가진다.");
                text.AppendLine();
                text.AppendLine("[헌터 손님에게 들은 정보]");
                if (game.RevealedFragments.Count == 0)
                {
                    text.AppendLine("  아직 없음");
                }

                foreach (string fragment in game.RevealedFragments)
                {
                    text.AppendLine($"  - {fragment}");
                }
                break;

            case GuideCategory.Job:
                foreach (JobDefinition job in GameData.Jobs)
                {
                    text.AppendLine();
                    text.AppendLine($"[{GameData.GetCategoryName(job.Category)}] {job.Name}");
                    text.AppendLine(job.Feature);
                    text.AppendLine($"행동: {job.Request}");
                }
                break;

            case GuideCategory.Facility:
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
            popup = Popup.None;
        }
    }

    // 상단 [도움말] 버튼으로만 여는 튜토리얼 안내.
    void DrawHelp()
    {
        Rect area = new Rect(460, 180, 1000, 640);
        GUI.Box(area, GUIContent.none);
        GUI.Box(area, GUIContent.none);
        GUI.Label(new Rect(area.x + 20, area.y + 15, 960, 40), "도움말", titleStyle);

        string text = "목표: 괴물들을 모두 찾아내 처형하십시오!\n\n" +
                      "[화면]\n" +
                      "건물: 객실과 주인 방. 클릭하면 오른쪽 패널에서 상호작용할 수 있습니다.\n" +
                      "로비: 낮에 방에서 나온 손님이 모입니다. 로비의 손님을 클릭해서 상호작용할 수 있습니다.\n\n" +
                      "[낮 진행]\n" +
                      "소집·조사: 회의 시작 전 로비로 회의에 참가할 인원들을 모집하거나, 방을 조사할 수 있습니다.\n" +
                      "집중 심문: 손님 1명에게 행동을 요청하고, 손님 1명을 처형할 수 있습니다.\n" +
                      "기본적으로 잠긴 방은 조사할 수 없습니다. 흔적이 약하게 남는 단서들의 경우 수색 직업의 손님에게 맡겨야 합니다.\n\n" +
                      "[단축키]\n" +
                      "Space: 다음 단계 / 밤으로 / 팝업 닫기     Esc: 선택·대상 지정 취소";
        TaggedLabel(new Rect(area.x + 20, area.y + 70, 960, 480), text, labelStyle);

        if (GUI.Button(new Rect(area.x + 375, area.yMax - 70, 250, 50), "닫기 (Space)", buttonStyle))
        {
            popup = Popup.None;
        }
    }

    void DrawResult()
    {
        Rect area = new Rect(560, 220, 800, 560);
        GUI.Box(area, GUIContent.none);
        GUI.Box(area, GUIContent.none);
        string title = game.Result == GameResult.Won ? "승리" : "패배";
        GUI.Label(new Rect(area.x + 20, area.y + 15, 760, 40), $"{title} - {game.ResultMessage}", titleStyle);
        TaggedLabel(new Rect(area.x + 20, area.y + 70, 760, 400), string.Join("\n", game.MorningReport), labelStyle);

        if (GUI.Button(new Rect(area.x + 275, area.yMax - 70, 250, 50), "다시 시작", buttonStyle))
        {
            ShowTitle();
        }
    }

    void DrawTitleScreen()
    {
        GUI.Box(new Rect(0, 0, ReferenceWidth, ReferenceHeight), GUIContent.none);
        GUI.Label(new Rect(0, 280, ReferenceWidth, 80), "Rest Inn Peace", heroStyle);
        GUI.Label(new Rect(460, 380, 1000, 40), "인간으로 위장한 괴물들을 찾아내며 여관을 운영한다.", new GUIStyle(labelStyle)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 22
        });

        if (GUI.Button(new Rect(810, 520, 300, 60), "시작", buttonStyle))
        {
            ShowDifficulty();
        }
    }

    void DrawDifficultyScreen()
    {
        GUI.Box(new Rect(0, 0, ReferenceWidth, ReferenceHeight), GUIContent.none);
        GUI.Label(new Rect(0, 160, ReferenceWidth, 50), "난이도 선택", new GUIStyle(titleStyle)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 32
        });

        float y = 280f;
        foreach (DifficultyConfig config in GameBalance.Difficulties)
        {
            Rect button = new Rect(660, y, 600, 70);
            string label = $"{config.Name}  —  {config.Summary}";
            if (GUI.Button(button, label, buttonStyle))
            {
                StartNewGame(config.Id);
            }

            y += 90f;
        }

        if (GUI.Button(new Rect(810, 620, 300, 50), "뒤로", buttonStyle))
        {
            ShowTitle();
        }
    }

    #endregion
}
