#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 플레이스홀더 PNG를 스프라이트로 맞춘 뒤 RoomCell / InnBuilding 프리팹과 데모 씬을 조립한다.
public static class RoomPrefabAssembler
{
    const string ArtFolder = "Assets/Art/Rooms/Placeholders";
    const string PrefabFolder = "Assets/Prefabs/Rooms";
    const string ResourcesPrefabFolder = "Assets/Resources/Prefabs";
    const string ResourcesBuildingPath = "Assets/Resources/Prefabs/InnBuilding.prefab";
    const string SoFolder = "Assets/ScriptableObjects/Rooms";
    const string ScenePath = "Assets/Scenes/InnRooms.unity";
    const float Ppu = 100f;

    [MenuItem("Rest Inn Peace/Assemble Room Prefabs")]
    public static void AssembleFromMenu()
    {
        Assemble();
    }

    // Unity.exe -batchmode -quit -projectPath ... -executeMethod RoomPrefabAssembler.Assemble
    public static void Assemble()
    {
        Directory.CreateDirectory(PrefabFolder);
        Directory.CreateDirectory(ResourcesPrefabFolder);
        Directory.CreateDirectory(SoFolder);
        Directory.CreateDirectory("Assets/Scenes");

        ImportSprites();
        ImportUiTextures();
        RoomArtSet art = CreateOrUpdateArtSet();
        string cellPath = CreateRoomCellPrefab(art);
        string buildingPath = CreateInnBuildingPrefab(cellPath, art);
        PublishResourcesPrefab(buildingPath);
        CreateGameScene(buildingPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Room prefabs assembled.");
    }

    static void PublishResourcesPrefab(string buildingPath)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ResourcesBuildingPath) != null)
        {
            AssetDatabase.DeleteAsset(ResourcesBuildingPath);
        }

        if (!AssetDatabase.CopyAsset(buildingPath, ResourcesBuildingPath))
        {
            throw new IOException("Failed to copy InnBuilding into Resources: " + ResourcesBuildingPath);
        }
    }

    static void ImportSprites()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                continue;
            }

            bool dirty = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                dirty = true;
            }

            if (importer.spritePixelsPerUnit != Ppu)
            {
                importer.spritePixelsPerUnit = Ppu;
                dirty = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                dirty = true;
            }

            if (importer.filterMode != FilterMode.Point)
            {
                importer.filterMode = FilterMode.Point;
                dirty = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                dirty = true;
            }

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            string fileName = Path.GetFileNameWithoutExtension(path);
            int wantedAlignment;
            if (fileName == "room_back" || fileName == "room_closed" || fileName == "selection_frame" ||
                fileName == "room_side_shadow")
            {
                wantedAlignment = (int)SpriteAlignment.BottomLeft;
            }
            else if (fileName.StartsWith("clue_") || fileName == "lock")
            {
                wantedAlignment = (int)SpriteAlignment.Center;
            }
            else
            {
                wantedAlignment = (int)SpriteAlignment.BottomCenter;
            }

            if (settings.spriteAlignment != wantedAlignment)
            {
                settings.spriteAlignment = wantedAlignment;
                settings.spritePivot = wantedAlignment == (int)SpriteAlignment.BottomLeft
                    ? new Vector2(0f, 0f)
                    : wantedAlignment == (int)SpriteAlignment.BottomCenter
                        ? new Vector2(0.5f, 0f)
                        : new Vector2(0.5f, 0.5f);
                importer.SetTextureSettings(settings);
                dirty = true;
            }

            if (dirty)
            {
                importer.SaveAndReimport();
            }
        }
    }

    // OnGUI 버튼·패널용 9-slice 텍스처. 압축 없이 GUI 타입으로 맞춰 테두리가 뭉개지지 않게 한다.
    static void ImportUiTextures()
    {
        const string uiFolder = "Assets/Resources/UI";
        if (!AssetDatabase.IsValidFolder(uiFolder))
        {
            return;
        }

        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { uiFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                continue;
            }

            bool dirty = false;
            if (importer.textureType != TextureImporterType.GUI)
            {
                importer.textureType = TextureImporterType.GUI;
                dirty = true;
            }

            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                dirty = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                dirty = true;
            }

            if (importer.wrapMode != TextureWrapMode.Clamp)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                dirty = true;
            }

            if (dirty)
            {
                importer.SaveAndReimport();
            }
        }
    }

    static RoomArtSet CreateOrUpdateArtSet()
    {
        string path = $"{SoFolder}/DefaultRoomArt.asset";
        RoomArtSet art = AssetDatabase.LoadAssetAtPath<RoomArtSet>(path);
        if (art == null)
        {
            art = ScriptableObject.CreateInstance<RoomArtSet>();
            AssetDatabase.CreateAsset(art, path);
        }

        art.Back = LoadSprite("room_back");
        art.Facade = LoadSprite("room_closed");
        art.Floor = LoadSprite("room_floor");
        art.SideShadow = LoadSprite("room_side_shadow");
        art.Bed = LoadSprite("bed");
        art.Nightstand = LoadSprite("nightstand");
        art.DoorClosed = LoadSprite("door_closed");
        art.DoorOpen = LoadSprite("door_open");
        art.Lock = LoadSprite("lock");
        art.ClueFur = LoadSprite("clue_fur");
        art.ClueSmell = LoadSprite("clue_smell");
        art.ClueFootprint = LoadSprite("clue_footprint");
        art.ClueScratch = LoadSprite("clue_scratch");
        art.SelectionFrame = LoadSprite("selection_frame");
        art.GuestSilhouette = LoadSprite("guest_silhouette");
        art.OwnerDesk = LoadSprite("owner_desk");
        art.Corridor = LoadSprite("corridor");
        EditorUtility.SetDirty(art);
        return art;
    }

    static Sprite LoadSprite(string name)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtFolder}/{name}.png");
    }

    static string CreateRoomCellPrefab(RoomArtSet art)
    {
        GameObject root = new GameObject("RoomCell");
        RoomCellView view = root.AddComponent<RoomCellView>();
        BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(RoomCellView.CellWidth, RoomCellView.CellHeight);
        collider.offset = new Vector2(RoomCellView.CellWidth * 0.5f, RoomCellView.CellHeight * 0.5f);

        SpriteRenderer back = CreateLayer(root.transform, "Back", 0, new Vector3(0f, 0f, 0f));
        SpriteRenderer facade = CreateLayer(root.transform, "Facade", 8, new Vector3(0f, 0f, -0.005f));
        SpriteRenderer floor = CreateLayer(root.transform, "Floor", 1, new Vector3(RoomCellView.CellWidth * 0.5f, 0f, -0.01f));
        SpriteRenderer side = CreateLayer(root.transform, "SideShadow", 2, new Vector3(0f, 0f, -0.02f));
        SpriteRenderer bed = CreateLayer(root.transform, "Bed", 3, new Vector3(1.05f, 0.08f, -0.03f));
        SpriteRenderer nightstand = CreateLayer(root.transform, "Nightstand", 3, new Vector3(1.95f, 0.08f, -0.03f));
        SpriteRenderer doorClosed = CreateLayer(root.transform, "DoorClosed", 4, new Vector3(2.55f, 0.05f, -0.04f));
        SpriteRenderer doorOpen = CreateLayer(root.transform, "DoorOpen", 4, new Vector3(2.55f, 0.05f, -0.04f));
        SpriteRenderer guest = CreateLayer(root.transform, "Guest", 5, new Vector3(0.55f, 0.08f, -0.05f));
        SpriteRenderer ownerDesk = CreateLayer(root.transform, "OwnerDesk", 3, new Vector3(1.5f, 0.1f, -0.03f));
        SpriteRenderer lockIcon = CreateLayer(root.transform, "Lock", 9, new Vector3(2.55f, 1.15f, -0.06f));
        SpriteRenderer fur = CreateLayer(root.transform, "ClueFur", 6, new Vector3(1.2f, 0.2f, -0.06f));
        SpriteRenderer smell = CreateLayer(root.transform, "ClueSmell", 6, new Vector3(0.4f, 0.7f, -0.06f));
        SpriteRenderer footprint = CreateLayer(root.transform, "ClueFootprint", 6, new Vector3(1.8f, 0.15f, -0.06f));
        SpriteRenderer scratch = CreateLayer(root.transform, "ClueScratch", 6, new Vector3(2.4f, 0.55f, -0.06f));
        SpriteRenderer selection = CreateLayer(root.transform, "Selection", 9, new Vector3(0f, 0f, -0.07f));

        GameObject labelGo = new GameObject("Label");
        labelGo.transform.SetParent(root.transform, false);
        labelGo.transform.localPosition = new Vector3(0.12f, 1.45f, -0.08f);
        TextMesh label = labelGo.AddComponent<TextMesh>();
        label.fontSize = 32;
        label.characterSize = 0.04f;
        label.anchor = TextAnchor.UpperLeft;
        label.color = new Color(0.9f, 0.82f, 0.65f);
        label.text = "101호";

        view.ConfigureSlots(back, facade, floor, side, bed, nightstand, doorClosed, doorOpen, guest, lockIcon, fur, smell, footprint, scratch, selection, ownerDesk, label);
        view.ApplyArt(art);
        view.SetSelected(false);

        string prefabPath = $"{PrefabFolder}/RoomCell.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.ImportAsset(prefabPath);
        return prefabPath;
    }

    static string CreateInnBuildingPrefab(string cellPrefabPath, RoomArtSet art)
    {
        RoomCellView cellPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(cellPrefabPath).GetComponent<RoomCellView>();

        GameObject root = new GameObject("InnBuilding");
        InnBuildingView building = root.AddComponent<InnBuildingView>();

        Transform roomsRoot = new GameObject("Rooms").transform;
        roomsRoot.SetParent(root.transform, false);

        SpriteRenderer corridorLower = CreateLayer(root.transform, "CorridorLower", -1, Vector3.zero);
        SpriteRenderer corridorUpper = CreateLayer(root.transform, "CorridorUpper", -1, Vector3.zero);
        corridorLower.sprite = art.Corridor;
        corridorUpper.sprite = art.Corridor;

        building.Configure(cellPrefab, art, roomsRoot, corridorLower, corridorUpper);

        string prefabPath = $"{PrefabFolder}/InnBuilding.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.ImportAsset(prefabPath);
        return prefabPath;
    }

    // 실제 게임 뷰(InnGameView) + 방 셀 건물이 들어 있는 플레이 씬.
    static void CreateGameScene(string buildingPrefabPath)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        Camera cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 3.4f;
        cam.transform.position = new Vector3(3.3f, 1.7f, -10f);
        cam.backgroundColor = new Color(0.07f, 0.05f, 0.04f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        camGo.AddComponent<AudioListener>();

        GameObject buildingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(buildingPrefabPath);
        if (buildingPrefab == null)
        {
            throw new FileNotFoundException("InnBuilding prefab missing", buildingPrefabPath);
        }

        GameObject buildingGo = (GameObject)PrefabUtility.InstantiatePrefab(buildingPrefab, scene);
        buildingGo.name = "InnBuilding";

        new GameObject("InnGame").AddComponent<InnGameView>();

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
        {
            throw new IOException("Failed to save " + ScenePath);
        }
    }

    static SpriteRenderer CreateLayer(Transform parent, string name, int order, Vector3 localPos)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = order;
        return renderer;
    }
}
#endif
