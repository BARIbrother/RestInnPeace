using System;
using System.Collections.Generic;
using UnityEngine;

// 낮에 방에서 나온 손님이 모이는 로비 칸. 객실 3칸 너비로 건물 맨 아래에 놓이며, 외출한 손님을 실루엣으로 보여 준다.
public class LobbyCellView : MonoBehaviour
{
    const float GuestSpacing = 0.9f;
    const float GuestStartX = 0.6f;

    SpriteRenderer back;
    SpriteRenderer floor;
    SpriteRenderer selection;
    TextMesh label;
    Transform guestsRoot;
    RoomArtSet art;
    float width;
    readonly List<SpriteRenderer> guestMarkers = new List<SpriteRenderer>();

    public bool IsSelected { get; private set; }

    public event Action<LobbyCellView> Clicked;

    public static LobbyCellView Create(Transform parent, RoomArtSet artSet, float cellWidth, float cellHeight)
    {
        GameObject root = new GameObject("LobbyCell");
        root.transform.SetParent(parent, false);
        LobbyCellView view = root.AddComponent<LobbyCellView>();
        view.width = cellWidth;
        view.art = artSet;

        BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(cellWidth, cellHeight);
        collider.offset = new Vector2(cellWidth * 0.5f, cellHeight * 0.5f);

        // 객실용 300px 스프라이트를 가로로 늘려 로비 너비를 채운다.
        float stretch = cellWidth / RoomCellView.CellWidth;
        view.back = CreateLayer(root.transform, "Back", 0, Vector3.zero, new Vector3(stretch, 1f, 1f));
        view.floor = CreateLayer(root.transform, "Floor", 1, new Vector3(cellWidth * 0.5f, 0f, -0.01f), new Vector3(stretch, 1f, 1f));
        view.selection = CreateLayer(root.transform, "Selection", 7, new Vector3(0f, 0f, -0.07f), new Vector3(stretch, 1f, 1f));
        view.guestsRoot = new GameObject("Guests").transform;
        view.guestsRoot.SetParent(root.transform, false);

        GameObject labelGo = new GameObject("Label");
        labelGo.transform.SetParent(root.transform, false);
        labelGo.transform.localPosition = new Vector3(0.12f, cellHeight - 0.2f, -0.08f);
        view.label = labelGo.AddComponent<TextMesh>();
        view.label.fontSize = 32;
        view.label.characterSize = 0.04f;
        view.label.anchor = TextAnchor.UpperLeft;
        view.label.color = new Color(0.9f, 0.82f, 0.65f);
        RoomCellView.EnsureLabelFont(view.label);

        if (artSet != null)
        {
            view.back.sprite = artSet.Back;
            view.floor.sprite = artSet.Floor;
            view.selection.sprite = artSet.SelectionFrame;
        }

        view.selection.enabled = false;
        return view;
    }

    public void Bind(IEnumerable<Room> rooms, bool selected)
    {
        int index = 0;
        foreach (Room room in rooms)
        {
            if (room.Guest == null || !room.Guest.IsOut)
            {
                continue;
            }

            SpriteRenderer marker = GetMarker(index);
            marker.enabled = true;
            marker.transform.localPosition = new Vector3(GuestStartX + index * GuestSpacing, 0.08f, -0.05f);
            index++;
        }

        for (int i = index; i < guestMarkers.Count; i++)
        {
            guestMarkers[i].enabled = false;
        }

        label.text = index == 0 ? "로비\n(아무도 없음)" : $"로비\n손님 {index}명";
        SetSelected(selected);
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        if (selection != null)
        {
            selection.enabled = selected;
        }
    }

    void OnMouseDown()
    {
        Clicked?.Invoke(this);
    }

    SpriteRenderer GetMarker(int index)
    {
        while (guestMarkers.Count <= index)
        {
            SpriteRenderer marker = CreateLayer(guestsRoot, $"Guest{guestMarkers.Count}", 5, Vector3.zero, Vector3.one);
            marker.sprite = art != null ? art.GuestSilhouette : null;
            guestMarkers.Add(marker);
        }

        return guestMarkers[index];
    }

    static SpriteRenderer CreateLayer(Transform parent, string name, int order, Vector3 localPos, Vector3 localScale)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = order;
        return renderer;
    }
}
