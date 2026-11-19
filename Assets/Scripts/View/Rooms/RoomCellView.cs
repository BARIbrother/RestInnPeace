using System;
using UnityEngine;

// 여관 단면의 한 칸(손님 방 / 주인 방). 계층은 고정하고 스프라이트·표시만 Room / 선택 상태에 맞춰 갱신한다.
public class RoomCellView : MonoBehaviour
{
    public const float CellWidth = 3f;
    public const float CellHeight = 1.65f;

    [SerializeField] SpriteRenderer back;
    [SerializeField] SpriteRenderer facade;
    [SerializeField] SpriteRenderer floor;
    [SerializeField] SpriteRenderer sideShadow;
    [SerializeField] SpriteRenderer bed;
    [SerializeField] SpriteRenderer nightstand;
    [SerializeField] SpriteRenderer doorClosed;
    [SerializeField] SpriteRenderer doorOpen;
    [SerializeField] SpriteRenderer guest;
    [SerializeField] SpriteRenderer lockIcon;
    [SerializeField] SpriteRenderer clueFur;
    [SerializeField] SpriteRenderer clueSmell;
    [SerializeField] SpriteRenderer clueFootprint;
    [SerializeField] SpriteRenderer clueScratch;
    [SerializeField] SpriteRenderer selection;
    [SerializeField] SpriteRenderer ownerDesk;
    [SerializeField] TextMesh label;

    public int Floor { get; private set; }
    public int Column { get; private set; }
    public int RoomNumber { get; private set; }
    public bool IsOwnerRoom { get; private set; }
    public Room BoundRoom { get; private set; }

    bool selectedVisible;
    bool orderHighlighted;

    static readonly Vector3 GuestInsidePos = new Vector3(0.55f, 0.08f, -0.05f);
    // 닫힌 방 창문(약 x 0.36~1.68, y 0.37~1.29) 안에 실루엣이 비치도록 둔다.
    static readonly Vector3 GuestWindowPos = new Vector3(0.95f, 0.42f, -0.004f);
    static readonly Color GuestInsideColor = Color.white;
    static readonly Color GuestWindowColor = new Color(0.04f, 0.03f, 0.05f, 0.95f);

    public event Action<RoomCellView> Clicked;

    public void ConfigureSlots(
        SpriteRenderer backRenderer,
        SpriteRenderer facadeRenderer,
        SpriteRenderer floorRenderer,
        SpriteRenderer sideShadowRenderer,
        SpriteRenderer bedRenderer,
        SpriteRenderer nightstandRenderer,
        SpriteRenderer doorClosedRenderer,
        SpriteRenderer doorOpenRenderer,
        SpriteRenderer guestRenderer,
        SpriteRenderer lockRenderer,
        SpriteRenderer furRenderer,
        SpriteRenderer smellRenderer,
        SpriteRenderer footprintRenderer,
        SpriteRenderer scratchRenderer,
        SpriteRenderer selectionRenderer,
        SpriteRenderer ownerDeskRenderer,
        TextMesh labelMesh)
    {
        back = backRenderer;
        facade = facadeRenderer;
        floor = floorRenderer;
        sideShadow = sideShadowRenderer;
        bed = bedRenderer;
        nightstand = nightstandRenderer;
        doorClosed = doorClosedRenderer;
        doorOpen = doorOpenRenderer;
        guest = guestRenderer;
        lockIcon = lockRenderer;
        clueFur = furRenderer;
        clueSmell = smellRenderer;
        clueFootprint = footprintRenderer;
        clueScratch = scratchRenderer;
        selection = selectionRenderer;
        ownerDesk = ownerDeskRenderer;
        label = labelMesh;
    }

    public void ApplyArt(RoomArtSet art)
    {
        if (art == null)
        {
            return;
        }

        EnsureFacade(art);
        SetSprite(back, art.Back);
        SetSprite(facade, art.Facade != null ? art.Facade : art.Back);
        SetSprite(floor, art.Floor);
        SetSprite(sideShadow, art.SideShadow);
        SetSprite(bed, art.Bed);
        SetSprite(nightstand, art.Nightstand);
        SetSprite(doorClosed, art.DoorClosed);
        SetSprite(doorOpen, art.DoorOpen);
        SetSprite(guest, art.GuestSilhouette);
        SetSprite(lockIcon, art.Lock);
        SetSprite(clueFur, art.ClueFur);
        SetSprite(clueSmell, art.ClueSmell);
        SetSprite(clueFootprint, art.ClueFootprint);
        SetSprite(clueScratch, art.ClueScratch);
        SetSprite(selection, art.SelectionFrame);
        SetSprite(ownerDesk, art.OwnerDesk);
    }

    public void SetupOwner(int floor, int column, RoomArtSet art)
    {
        Floor = floor;
        Column = column;
        RoomNumber = 0;
        IsOwnerRoom = true;
        BoundRoom = null;
        ApplyArt(art);
        SetInteriorVisible(true);
        SetActive(facade, false);
        SetActive(bed, false);
        SetActive(nightstand, false);
        SetActive(guest, false);
        SetActive(doorClosed, true);
        SetActive(doorOpen, false);
        SetActive(ownerDesk, true);
        ClearClues();
        SetSelected(false);
        SetLabel("주인 방");
    }

    public void Bind(Room room, RoomArtSet art, bool selected)
    {
        BoundRoom = room;
        Floor = room.Floor;
        Column = room.Column;
        RoomNumber = room.Number;
        IsOwnerRoom = false;
        ApplyArt(art);

        bool showInside = room.Inspected;
        bool guestPresent = room.Guest != null && !room.Guest.IsOut;
        SetInteriorVisible(showInside);
        // 닫힌 방에서도 뒷벽은 남겨 창문 너머 어두운 실내가 보이게 한다.
        SetActive(back, true);
        SetActive(facade, !showInside);
        SetActive(ownerDesk, false);
        SetActive(lockIcon, room.DoorLocked);

        if (showInside)
        {
            bool doorIsOpen = room.Guest != null && room.Guest.IsOut;
            SetActive(bed, true);
            SetActive(nightstand, true);
            PlaceGuest(guestPresent, GuestInsidePos, 5, GuestInsideColor, Vector3.one);
            SetActive(doorClosed, !doorIsOpen);
            SetActive(doorOpen, doorIsOpen);
            SetActive(clueFur, room.HasFur);
            SetActive(clueSmell, room.HasSmell);
            SetActive(clueFootprint, room.HasFootprint);
            SetActive(clueScratch, false);
        }
        else
        {
            ClearClues();
            SetActive(lockIcon, room.DoorLocked);
            // 닫힌 방·손님 있음: 창문 너머 검은 실루엣.
            PlaceGuest(guestPresent, GuestWindowPos, 7, GuestWindowColor, new Vector3(0.85f, 0.85f, 1f));
        }

        SetSelected(selected);
        if (!showInside)
        {
            SetLabel(room.DoorLocked ? $"{room.Number}호\n[잠김]" : $"{room.Number}호");
        }
        else if (room.Guest == null)
        {
            SetLabel($"{room.Number}호\n빈 방");
        }
        else if (room.Guest.IsOut)
        {
            SetLabel($"{room.Number}호\n(로비)");
        }
        else
        {
            SetLabel($"{room.Number}호\n{room.Guest.DisplayName}");
        }
    }

    public void SetSelected(bool selected)
    {
        selectedVisible = selected;
        RefreshSelectionVisual();
    }

    // 직업 행동 대상 지정 중일 때 클릭 가능한 방을 초록으로 표시한다.
    public void SetOrderHighlight(bool highlighted)
    {
        orderHighlighted = highlighted;
        RefreshSelectionVisual();
    }

    void RefreshSelectionVisual()
    {
        if (selection == null)
        {
            return;
        }

        if (selectedVisible)
        {
            selection.enabled = true;
            selection.color = Color.white;
        }
        else if (orderHighlighted)
        {
            selection.enabled = true;
            selection.color = new Color(0.45f, 1f, 0.45f, 1f);
        }
        else
        {
            selection.enabled = false;
            selection.color = Color.white;
        }
    }

    public void SetOwnerLocks(int remaining, int max)
    {
        if (!IsOwnerRoom)
        {
            return;
        }

        SetLabel($"주인 방\n자물쇠 {remaining}/{max}");
        SetActive(lockIcon, remaining > 0);
    }

    void OnMouseDown()
    {
        Clicked?.Invoke(this);
    }

    // 기존 프리팹에 Facade 슬롯이 없어도 런타임에 만들어 닫힌 방 외벽을 깐다.
    void EnsureFacade(RoomArtSet art)
    {
        if (facade == null)
        {
            Transform existing = transform.Find("Facade");
            if (existing != null)
            {
                facade = existing.GetComponent<SpriteRenderer>();
            }
        }

        if (facade == null)
        {
            GameObject go = new GameObject("Facade");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            facade = go.AddComponent<SpriteRenderer>();
            facade.sortingOrder = 8;
        }

        if (selection != null && selection.sortingOrder <= facade.sortingOrder)
        {
            selection.sortingOrder = facade.sortingOrder + 1;
        }

        if (label != null)
        {
            MeshRenderer labelRenderer = label.GetComponent<MeshRenderer>();
            if (labelRenderer != null && labelRenderer.sortingOrder <= facade.sortingOrder)
            {
                labelRenderer.sortingOrder = facade.sortingOrder + 2;
            }
        }

        if (lockIcon != null && lockIcon.sortingOrder <= facade.sortingOrder)
        {
            lockIcon.sortingOrder = facade.sortingOrder + 1;
        }
    }

    void PlaceGuest(bool visible, Vector3 localPos, int sortingOrder, Color color, Vector3 scale)
    {
        if (guest == null)
        {
            return;
        }

        guest.enabled = visible;
        if (!visible)
        {
            return;
        }

        guest.transform.localPosition = localPos;
        guest.transform.localScale = scale;
        guest.sortingOrder = sortingOrder;
        guest.color = color;
    }

    void SetInteriorVisible(bool visible)
    {
        SetActive(back, visible);
        SetActive(floor, visible);
        SetActive(sideShadow, visible);
        if (!visible)
        {
            SetActive(bed, false);
            SetActive(nightstand, false);
            SetActive(doorClosed, false);
            SetActive(doorOpen, false);
            SetActive(guest, false);
            SetActive(clueFur, false);
            SetActive(clueSmell, false);
            SetActive(clueFootprint, false);
            SetActive(clueScratch, false);
        }
    }

    void ClearClues()
    {
        SetActive(clueFur, false);
        SetActive(clueSmell, false);
        SetActive(clueFootprint, false);
        SetActive(clueScratch, false);
        SetActive(lockIcon, false);
    }

    static void SetSprite(SpriteRenderer renderer, Sprite sprite)
    {
        if (renderer != null)
        {
            renderer.sprite = sprite;
        }
    }

    static void SetActive(SpriteRenderer renderer, bool active)
    {
        if (renderer != null)
        {
            renderer.enabled = active;
        }
    }

    void SetLabel(string text)
    {
        if (label != null)
        {
            EnsureLabelFont(label);
            label.text = text;
        }
    }

    // 코드로 만든 TextMesh는 폰트가 비어 있어 글자가 안 보이므로 내장 폰트를 채워 준다.
    public static void EnsureLabelFont(TextMesh textMesh)
    {
        if (textMesh == null || textMesh.font != null)
        {
            return;
        }

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            return;
        }

        textMesh.font = font;
        MeshRenderer renderer = textMesh.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = font.material;
            renderer.sortingOrder = 8;
        }
    }
}
