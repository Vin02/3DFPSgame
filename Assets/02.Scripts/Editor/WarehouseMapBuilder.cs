using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// 슈팅게임 웨어하우스 같은 실내 맵을 한 번에 만든다. 이동 기능을 시험할 구간도 자연스럽게 섞여 있다.
//   중앙 통로(가속·감속) / 남쪽 로딩 도크(틈 건너기: 코요테 타임, 점프 버퍼, 공중 제어)
//   북쪽 화물 구역(높이가 다른 상자: 가변 점프) / 중2층 아래(낮은 천장: 천장 부딪힘)
//   고층 통로(10m 높이: 빠른 낙하, 최대 낙하 속도)
// 씬 파일은 저장하지 않는다. 결과를 확인한 뒤 직접 저장한다. (Ctrl+Z로 되돌릴 수 있다)
// 다시 실행하면 이전에 만든 맵을 지우고 새로 만든다.
public static class WarehouseMapBuilder
{
    private const string RootName = "Warehouse";
    private const string LegacyRootName = "JumpTestMap";
    private const string UndoGroupName = "Build Warehouse Map";

    // 건물 (x: 동서, z: 남북). 출입구는 서쪽 벽 가운데에 있다.
    private const float HallMinX = 12f;
    private const float HallMaxX = 48f;
    private const float HallMinZ = -30f;
    private const float HallMaxZ = 30f;
    private const float CeilingHeight = 14f;
    private const float WallThickness = 0.5f;
    private const float FloorThickness = 0.04f;
    private const float EntranceHalfWidth = 2f;
    private const float EntranceHeight = 4.5f;
    private const float WainscotHeight = 1.2f;
    private const float WainscotThickness = 0.06f;
    private const float CorridorHalfWidth = 3f;

    // 기둥과 조명
    private static readonly float[] ColumnXs = { 18f, 30f, 42f };
    private static readonly float[] ColumnZs = { -10f, 10f };
    private static readonly float[] LampXs = { 18f, 30f, 42f };
    private static readonly float[] LampZs = { -21f, -7f, 7f, 21f };
    private static readonly float[] WindowXs = { 15f, 21f, 27f, 33f, 39f, 45f };
    private const float WindowBottom = 10.5f;
    private const float WindowTop = 12f;
    private const float WindowHalfWidth = 2f;

    // 남쪽 로딩 도크: 틈이 점점 벌어지는 발판
    private const float DockHeight = 1.2f;
    private const float DockPlatformLength = 3.5f;
    private const float DockMinZ = -29.5f;
    private const float DockMaxZ = -24.5f;
    private const float DockStartX = 13f;
    private static readonly float[] DockGapWidths = { 1.5f, 2f, 2.5f, 3f, 3.5f };

    // 북쪽 화물 구역: 높이가 다른 상자
    private const float CargoCrateSize = 2f;
    private const float CargoCrateSpacing = 4f;
    private const float CargoStartX = 15f;
    private const float CargoZ = 14f;
    private static readonly float[] CargoCrateHeights = { 0.5f, 1f, 1.5f, 2f, 2.5f, 3f, 3.5f };

    // 중2층(아래 천장 3.4m)과 고층 통로(바닥 10m)
    private const float MezzanineMinX = 22f;
    private const float MezzanineMaxX = 46f;
    private const float MezzanineMinZ = 20f;
    private const float MezzanineMaxZ = 30f;
    private const float MezzanineUnderside = 3.4f;
    private const float MezzanineTop = 3.8f;
    private const float GantryMinX = 40f;
    private const float GantryMaxX = 47.8f;
    private const float GantryTop = 10f;
    private const float DeckThickness = 0.4f;
    private const float SupportSize = 0.25f;
    private const float RampCenterZ = 25f;
    private const float RampWidth = 3f;
    private const float RailingHeight = 1f;
    private const float RailingPostSpacing = 2f;
    private const float RailingThickness = 0.06f;

    // 철제 선반
    private const float RackLength = 3f;
    private const float RackDepth = 1.1f;
    private const float RackPostSize = 0.1f;
    private const float RackShelfThickness = 0.08f;
    private const float RackStartX = 16f;
    private const float RackPitch = 3.4f;
    private const int RackUnitCount = 8;
    private static readonly float[] RackShelfHeights = { 0.25f, 1.35f, 2.45f, 3.55f };
    private const int RackRandomSeed = 7;

    private const float ContainerHeight = 2.6f;
    private const float ContainerLength = 12f;
    private const float ContainerDepth = 2.4f;
    private const float ContainerCenterZ = -8f;

    private const float CrateBandThickness = 0.12f;
    private const float CrateBandMargin = 0.2f;
    private const float CrateBandOverhang = 0.02f;
    private const float BarrelRadius = 0.3f;
    private const float BarrelHeight = 1f;

    [MenuItem("Tools/Map/Build Warehouse")]
    private static void Build()
    {
        Undo.SetCurrentGroupName(UndoGroupName);
        int undoGroup = Undo.GetCurrentGroup();

        RemoveExisting(RootName);
        RemoveExisting(LegacyRootName);

        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, UndoGroupName);

        MapMaterialLibrary materials = MapMaterialLibrary.Prepare(UndoGroupName);

        BuildShell(MapPartFactory.CreateGroup(root.transform, "Shell"), materials);
        BuildLamps(MapPartFactory.CreateGroup(root.transform, "Lamps"), materials);
        BuildLoadingDock(MapPartFactory.CreateGroup(root.transform, "LoadingDock"), materials);
        BuildCargoBay(MapPartFactory.CreateGroup(root.transform, "CargoBay"), materials);
        BuildMezzanine(MapPartFactory.CreateGroup(root.transform, "Mezzanine"), materials);
        BuildRacks(MapPartFactory.CreateGroup(root.transform, "Racks"), materials);
        BuildProps(MapPartFactory.CreateGroup(root.transform, "Props"), materials);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(root.scene);
        Undo.CollapseUndoOperations(undoGroup);
        Selection.activeGameObject = root;
    }

    private static void RemoveExisting(string name)
    {
        GameObject existing = GameObject.Find(name);

        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing);
        }
    }

    #region 건물

    private static void BuildShell(Transform parent, MapMaterialLibrary materials)
    {
        Material floor = materials.Get(MapPaint.Floor);
        Material wall = materials.Get(MapPaint.Wall);
        Material trim = materials.Get(MapPaint.Trim);
        Material hazard = materials.Get(MapPaint.Hazard);

        float outerMinX = HallMinX - WallThickness;
        float outerMaxX = HallMaxX + WallThickness;
        float outerMinZ = HallMinZ - WallThickness;
        float outerMaxZ = HallMaxZ + WallThickness;

        MapPartFactory.CreateBoxFromBounds(parent, "Floor",
            new Vector3(HallMinX, 0f, HallMinZ), new Vector3(HallMaxX, FloorThickness, HallMaxZ), floor);

        MapPartFactory.CreateBoxFromBounds(parent, "Roof",
            new Vector3(outerMinX, CeilingHeight, outerMinZ),
            new Vector3(outerMaxX, CeilingHeight + WallThickness, outerMaxZ),
            materials.Get(MapPaint.Roof));

        // 바깥 벽. 서쪽 벽은 출입구를 비워 둔다.
        MapPartFactory.CreateBoxFromBounds(parent, "WallSouth",
            new Vector3(outerMinX, 0f, outerMinZ), new Vector3(outerMaxX, CeilingHeight, HallMinZ), wall);
        MapPartFactory.CreateBoxFromBounds(parent, "WallNorth",
            new Vector3(outerMinX, 0f, HallMaxZ), new Vector3(outerMaxX, CeilingHeight, outerMaxZ), wall);
        MapPartFactory.CreateBoxFromBounds(parent, "WallEast",
            new Vector3(HallMaxX, 0f, HallMinZ), new Vector3(outerMaxX, CeilingHeight, HallMaxZ), wall);
        MapPartFactory.CreateBoxFromBounds(parent, "WallWestSouth",
            new Vector3(outerMinX, 0f, HallMinZ), new Vector3(HallMinX, CeilingHeight, -EntranceHalfWidth), wall);
        MapPartFactory.CreateBoxFromBounds(parent, "WallWestNorth",
            new Vector3(outerMinX, 0f, EntranceHalfWidth), new Vector3(HallMinX, CeilingHeight, HallMaxZ), wall);
        MapPartFactory.CreateBoxFromBounds(parent, "WallWestLintel",
            new Vector3(outerMinX, EntranceHeight, -EntranceHalfWidth),
            new Vector3(HallMinX, CeilingHeight, EntranceHalfWidth), wall);

        BuildEntranceFrame(parent, hazard);
        BuildWainscot(parent, trim);
        BuildWindows(parent, materials.Get(MapPaint.Window));
        BuildColumns(parent, materials.Get(MapPaint.Column), hazard);
        BuildFloorMarkings(parent, hazard);
    }

    private static void BuildEntranceFrame(Transform parent, Material hazard)
    {
        const float stripe = 0.25f;
        float minX = HallMinX - WallThickness;
        float maxX = HallMinX + 0.05f;

        MapPartFactory.CreateBoxFromBounds(parent, "EntranceFrameSouth",
            new Vector3(minX, 0f, -EntranceHalfWidth - stripe), new Vector3(maxX, EntranceHeight, -EntranceHalfWidth), hazard);
        MapPartFactory.CreateBoxFromBounds(parent, "EntranceFrameNorth",
            new Vector3(minX, 0f, EntranceHalfWidth), new Vector3(maxX, EntranceHeight, EntranceHalfWidth + stripe), hazard);
        MapPartFactory.CreateBoxFromBounds(parent, "EntranceFrameTop",
            new Vector3(minX, EntranceHeight - stripe, -EntranceHalfWidth), new Vector3(maxX, EntranceHeight, EntranceHalfWidth), hazard);
    }

    // 벽 아래쪽 어두운 띠
    private static void BuildWainscot(Transform parent, Material trim)
    {
        const float bottom = FloorThickness;
        const float top = WainscotHeight;

        MapPartFactory.CreateBoxFromBounds(parent, "TrimSouth",
            new Vector3(HallMinX, bottom, HallMinZ), new Vector3(HallMaxX, top, HallMinZ + WainscotThickness), trim);
        MapPartFactory.CreateBoxFromBounds(parent, "TrimNorth",
            new Vector3(HallMinX, bottom, HallMaxZ - WainscotThickness), new Vector3(HallMaxX, top, HallMaxZ), trim);
        MapPartFactory.CreateBoxFromBounds(parent, "TrimEast",
            new Vector3(HallMaxX - WainscotThickness, bottom, HallMinZ), new Vector3(HallMaxX, top, HallMaxZ), trim);
        MapPartFactory.CreateBoxFromBounds(parent, "TrimWestSouth",
            new Vector3(HallMinX, bottom, HallMinZ), new Vector3(HallMinX + WainscotThickness, top, -EntranceHalfWidth), trim);
        MapPartFactory.CreateBoxFromBounds(parent, "TrimWestNorth",
            new Vector3(HallMinX, bottom, EntranceHalfWidth), new Vector3(HallMinX + WainscotThickness, top, HallMaxZ), trim);
    }

    // 높은 곳의 채광창. 빛나는 면이라 건물이 닫혀 있어도 밝은 느낌을 준다.
    private static void BuildWindows(Transform parent, Material window)
    {
        const float depth = 0.04f;

        foreach (float x in WindowXs)
        {
            GameObject south = MapPartFactory.CreateBoxFromBounds(parent, "WindowSouth",
                new Vector3(x - WindowHalfWidth, WindowBottom, HallMinZ),
                new Vector3(x + WindowHalfWidth, WindowTop, HallMinZ + depth), window);
            GameObject north = MapPartFactory.CreateBoxFromBounds(parent, "WindowNorth",
                new Vector3(x - WindowHalfWidth, WindowBottom, HallMaxZ - depth),
                new Vector3(x + WindowHalfWidth, WindowTop, HallMaxZ), window);

            Object.DestroyImmediate(south.GetComponent<Collider>());
            Object.DestroyImmediate(north.GetComponent<Collider>());
        }
    }

    private static void BuildColumns(Transform parent, Material column, Material hazard)
    {
        const float size = 1f;
        const float baseBandHeight = 0.6f;
        const float baseBandOverhang = 0.05f;

        foreach (float x in ColumnXs)
        {
            foreach (float z in ColumnZs)
            {
                float half = size * 0.5f;

                MapPartFactory.CreateBoxFromBounds(parent, "Column",
                    new Vector3(x - half, 0f, z - half), new Vector3(x + half, CeilingHeight, z + half), column);

                float bandHalf = half + baseBandOverhang;
                MapPartFactory.CreateBoxFromBounds(parent, "ColumnBand",
                    new Vector3(x - bandHalf, 0f, z - bandHalf), new Vector3(x + bandHalf, baseBandHeight, z + bandHalf), hazard);
            }
        }
    }

    // 중앙 통로 양옆의 노란 선
    private static void BuildFloorMarkings(Transform parent, Material hazard)
    {
        const float lineHalfWidth = 0.075f;
        const float lineHeight = 0.015f;

        foreach (float z in new[] { -CorridorHalfWidth, CorridorHalfWidth })
        {
            MapPartFactory.CreateBoxFromBounds(parent, "CorridorLine",
                new Vector3(HallMinX, FloorThickness, z - lineHalfWidth),
                new Vector3(HallMaxX, FloorThickness + lineHeight, z + lineHalfWidth), hazard);
        }
    }

    private static void BuildLamps(Transform parent, MapMaterialLibrary materials)
    {
        const float hangOffset = 0.2f;

        foreach (float x in LampXs)
        {
            foreach (float z in LampZs)
            {
                MapPartFactory.CreateLamp(parent, "Lamp",
                    new Vector3(x, CeilingHeight - hangOffset, z), materials.Get(MapPaint.LampFixture));
            }
        }
    }

    #endregion

    #region 이동 시험 구간

    // 남쪽 벽을 따라 놓인 로딩 도크. 발판 사이 틈이 점점 넓어진다.
    private static void BuildLoadingDock(Transform parent, MapMaterialLibrary materials)
    {
        const float stripeWidth = 0.2f;
        const float stripeHeight = 0.02f;

        float x = DockStartX;

        for (int i = 0; i <= DockGapWidths.Length; i++)
        {
            MapPartFactory.CreateBoxFromBounds(parent, $"DockPlatform{i}",
                new Vector3(x, 0f, DockMinZ), new Vector3(x + DockPlatformLength, DockHeight, DockMaxZ),
                materials.Get(MapPaint.Steel));

            MapPartFactory.CreateBoxFromBounds(parent, $"DockStripe{i}",
                new Vector3(x, DockHeight, DockMaxZ - stripeWidth),
                new Vector3(x + DockPlatformLength, DockHeight + stripeHeight, DockMaxZ),
                materials.Get(MapPaint.Hazard));

            x += DockPlatformLength;

            if (i < DockGapWidths.Length)
            {
                x += DockGapWidths[i];
            }
        }
    }

    // 높이가 0.5m부터 3.5m까지 다른 화물 상자
    private static void BuildCargoBay(Transform parent, MapMaterialLibrary materials)
    {
        float half = CargoCrateSize * 0.5f;

        for (int i = 0; i < CargoCrateHeights.Length; i++)
        {
            float centerX = CargoStartX + i * CargoCrateSpacing;

            CreateCrate(parent, $"CargoCrate{i}",
                new Vector3(centerX - half, 0f, CargoZ - half),
                new Vector3(centerX + half, CargoCrateHeights[i], CargoZ + half), materials);
        }
    }

    // 중2층, 올라가는 상자 계단, 경사로, 고층 통로
    private static void BuildMezzanine(Transform parent, MapMaterialLibrary materials)
    {
        Material steel = materials.Get(MapPaint.Steel);
        Material hazard = materials.Get(MapPaint.Hazard);

        MapPartFactory.CreateBoxFromBounds(parent, "MezzanineDeck",
            new Vector3(MezzanineMinX, MezzanineUnderside, MezzanineMinZ),
            new Vector3(MezzanineMaxX, MezzanineTop, MezzanineMaxZ), steel);

        float[] supportXs = { MezzanineMinX + 0.3f, (MezzanineMinX + MezzanineMaxX) * 0.5f, MezzanineMaxX - 0.3f };
        float[] supportZs = { MezzanineMinZ + 0.3f, MezzanineMaxZ - 0.3f };
        CreateSupports(parent, "MezzanineSupport", supportXs, supportZs, MezzanineUnderside, steel);

        // 서쪽에서 올라오는 상자 계단 (1.2m -> 2.4m -> 3.4m -> 중2층 3.8m)
        float stairMinZ = RampCenterZ - 2.5f;
        float stairMaxZ = RampCenterZ + 2.5f;
        CreateCrate(parent, "StairCrate0", new Vector3(16.5f, 0f, stairMinZ), new Vector3(18.5f, 1.2f, stairMaxZ), materials);
        CreateCrate(parent, "StairCrate1", new Vector3(18.5f, 0f, stairMinZ), new Vector3(20.5f, 2.4f, stairMaxZ), materials);
        CreateCrate(parent, "StairCrate2", new Vector3(20.5f, 0f, stairMinZ), new Vector3(MezzanineMinX, 3.4f, stairMaxZ), materials);

        // 중2층 앞쪽과 서쪽 가장자리 난간 (계단이 닿는 곳은 비워 둔다)
        float railZ = MezzanineMinZ + 0.1f;
        float railX = MezzanineMinX + 0.1f;
        CreateRailing(parent, "MezzanineRailFront", new Vector3(MezzanineMinX, MezzanineTop, railZ), new Vector3(MezzanineMaxX, MezzanineTop, railZ), hazard);
        CreateRailing(parent, "MezzanineRailWest0", new Vector3(railX, MezzanineTop, MezzanineMinZ), new Vector3(railX, MezzanineTop, stairMinZ), hazard);
        CreateRailing(parent, "MezzanineRailWest1", new Vector3(railX, MezzanineTop, stairMaxZ), new Vector3(railX, MezzanineTop, MezzanineMaxZ), hazard);

        // 중2층에서 고층 통로로 오르는 경사로 (약 19도). 양쪽 가장자리에 안전선을 칠한다.
        Vector3 rampBottom = new Vector3(MezzanineMinX, MezzanineTop, RampCenterZ);
        Vector3 rampTop = new Vector3(GantryMinX, GantryTop, RampCenterZ);
        MapPartFactory.CreateRamp(parent, "Ramp", rampBottom, rampTop, RampWidth, DeckThickness, steel);
        CreateRampEdgeStripes(parent, rampBottom, rampTop, hazard);

        MapPartFactory.CreateBoxFromBounds(parent, "GantryDeck",
            new Vector3(GantryMinX, GantryTop - DeckThickness, MezzanineMinZ),
            new Vector3(GantryMaxX, GantryTop, MezzanineMaxZ), steel);

        float[] gantrySupportXs = { GantryMinX + 0.3f, GantryMaxX - 0.3f };
        CreateSupports(parent, "GantrySupport", gantrySupportXs, supportZs, GantryTop - DeckThickness, steel);

        // 고층 통로 앞쪽 난간. 가운데 3m는 뛰어내릴 수 있게 비워 둔다.
        float gantryRailZ = MezzanineMinZ + 0.1f;
        CreateRailing(parent, "GantryRailFront0", new Vector3(GantryMinX, GantryTop, gantryRailZ), new Vector3(43f, GantryTop, gantryRailZ), hazard);
        CreateRailing(parent, "GantryRailFront1", new Vector3(46f, GantryTop, gantryRailZ), new Vector3(GantryMaxX, GantryTop, gantryRailZ), hazard);
        float gantryRailX = GantryMinX + 0.1f;
        CreateRailing(parent, "GantryRailWest0", new Vector3(gantryRailX, GantryTop, MezzanineMinZ), new Vector3(gantryRailX, GantryTop, RampCenterZ - RampWidth * 0.5f), hazard);
        CreateRailing(parent, "GantryRailWest1", new Vector3(gantryRailX, GantryTop, RampCenterZ + RampWidth * 0.5f), new Vector3(gantryRailX, GantryTop, MezzanineMaxZ), hazard);
    }

    #endregion

    #region 선반과 소품

    private static void BuildRacks(Transform parent, MapMaterialLibrary materials)
    {
        var random = new System.Random(RackRandomSeed);

        // 사이사이 비워 둔 칸은 가로질러 지나갈 수 있는 틈이다.
        BuildRackRow(parent, "RackRowSouthA", -12f, new[] { 3, 4 }, materials, random);
        BuildRackRow(parent, "RackRowSouthB", -18f, Array.Empty<int>(), materials, random);
        BuildRackRow(parent, "RackRowNorth", 7f, new[] { 4, 5 }, materials, random);
    }

    private static void BuildRackRow(Transform parent, string name, float z, int[] skipIndices,
        MapMaterialLibrary materials, System.Random random)
    {
        Transform row = MapPartFactory.CreateGroup(parent, name);

        for (int i = 0; i < RackUnitCount; i++)
        {
            if (Array.IndexOf(skipIndices, i) >= 0)
            {
                continue;
            }

            BuildRack(row, $"Rack{i}", RackStartX + i * RackPitch, z, materials, random);
        }
    }

    private static void BuildRack(Transform parent, string name, float minX, float centerZ,
        MapMaterialLibrary materials, System.Random random)
    {
        Transform rack = MapPartFactory.CreateGroup(parent, name);
        float maxX = minX + RackLength;
        float height = RackShelfHeights[RackShelfHeights.Length - 1] + RackShelfThickness;
        float halfDepth = RackDepth * 0.5f;

        foreach (float postX in new[] { minX, maxX - RackPostSize })
        {
            foreach (float postZ in new[] { centerZ - halfDepth, centerZ + halfDepth - RackPostSize })
            {
                MapPartFactory.CreateBoxFromBounds(rack, "Post",
                    new Vector3(postX, 0f, postZ), new Vector3(postX + RackPostSize, height, postZ + RackPostSize),
                    materials.Get(MapPaint.RackPost));
            }
        }

        foreach (float shelfHeight in RackShelfHeights)
        {
            MapPartFactory.CreateBox(rack, "Shelf",
                new Vector3(minX + RackLength * 0.5f, shelfHeight, centerZ),
                new Vector3(RackLength, RackShelfThickness, RackDepth),
                materials.Get(MapPaint.RackShelf));

            PlaceGoodsOnShelf(rack, minX, centerZ, shelfHeight, materials, random);
        }
    }

    private static void PlaceGoodsOnShelf(Transform rack, float minX, float centerZ, float shelfHeight,
        MapMaterialLibrary materials, System.Random random)
    {
        const int maxGoodsPerShelf = 2;
        const float shelfEdgeMargin = 0.5f;

        int goodsCount = random.Next(0, maxGoodsPerShelf + 1);

        for (int i = 0; i < goodsCount; i++)
        {
            float width = NextFloat(random, 0.5f, 0.9f);
            float height = NextFloat(random, 0.4f, 0.8f);
            float depth = NextFloat(random, 0.5f, 0.8f);
            float x = minX + NextFloat(random, shelfEdgeMargin, RackLength - shelfEdgeMargin);
            float y = shelfHeight + RackShelfThickness * 0.5f + height * 0.5f;

            MapPaint paint = random.Next(2) == 0 ? MapPaint.Cardboard : MapPaint.Crate;
            MapPartFactory.CreateBox(rack, "Goods", new Vector3(x, y, centerZ),
                new Vector3(width, height, depth), materials.Get(paint));
        }
    }

    // 컨테이너, 쌓아 둔 상자, 드럼통 같은 엄폐물과 장식
    private static void BuildProps(Transform parent, MapMaterialLibrary materials)
    {
        CreateContainer(parent, "ContainerGreen", 14f, materials.Get(MapPaint.ContainerGreen));
        CreateContainer(parent, "ContainerRed", 34f, materials.Get(MapPaint.ContainerRed));

        CreateCrateStack(parent, "CrateStackA", 14f, 8f, 1.5f, 2, materials);
        CreateCrateStack(parent, "CrateStackB", 44.5f, 8f, 1.5f, 3, materials);
        CreateCrateStack(parent, "CrateStackC", 13.1f, -9f, 1.5f, 2, materials);
        CreateCrateStack(parent, "CrateStackD", 46f, -17f, 1.5f, 2, materials);

        Vector3[] barrelPositions =
        {
            new Vector3(46.8f, 0f, 22f), new Vector3(46.8f, 0f, 23.2f), new Vector3(45.7f, 0f, 22.6f),
            new Vector3(13.6f, 0f, -4f), new Vector3(13.6f, 0f, -5.1f),
            new Vector3(47f, 0f, -22f), new Vector3(47f, 0f, -23.2f),
        };

        foreach (Vector3 position in barrelPositions)
        {
            MapPartFactory.CreateCylinder(parent, "Barrel", position, BarrelRadius, BarrelHeight,
                materials.Get(MapPaint.Barrel));
        }
    }

    private static void CreateContainer(Transform parent, string name, float minX, Material material)
    {
        float halfDepth = ContainerDepth * 0.5f;

        MapPartFactory.CreateBoxFromBounds(parent, name,
            new Vector3(minX, 0f, ContainerCenterZ - halfDepth),
            new Vector3(minX + ContainerLength, ContainerHeight, ContainerCenterZ + halfDepth), material);
    }

    private static void CreateCrateStack(Transform parent, string name, float centerX, float centerZ,
        float size, int count, MapMaterialLibrary materials)
    {
        float half = size * 0.5f;

        for (int i = 0; i < count; i++)
        {
            CreateCrate(parent, $"{name}_{i}",
                new Vector3(centerX - half, i * size, centerZ - half),
                new Vector3(centerX + half, (i + 1) * size, centerZ + half), materials);
        }
    }

    #endregion

    #region 부품 조립

    // 위아래에 띠를 두른 나무 상자
    private static void CreateCrate(Transform parent, string name, Vector3 min, Vector3 max, MapMaterialLibrary materials)
    {
        Transform crate = MapPartFactory.CreateGroup(parent, name);
        MapPartFactory.CreateBoxFromBounds(crate, "Body", min, max, materials.Get(MapPaint.Crate));

        float height = max.y - min.y;

        // 낮은 상자에는 띠가 겹쳐 보이므로 두르지 않는다.
        if (height < (CrateBandThickness + CrateBandMargin) * 2f)
        {
            return;
        }

        Vector3 overhang = new Vector3(CrateBandOverhang, 0f, CrateBandOverhang);

        foreach (float bandY in new[] { min.y + CrateBandMargin, max.y - CrateBandMargin })
        {
            float bandHalf = CrateBandThickness * 0.5f;
            GameObject band = MapPartFactory.CreateBoxFromBounds(crate, "Band",
                new Vector3(min.x, bandY - bandHalf, min.z) - overhang,
                new Vector3(max.x, bandY + bandHalf, max.z) + overhang, materials.Get(MapPaint.CrateBand));

            Object.DestroyImmediate(band.GetComponent<Collider>());
        }
    }

    private static void CreateSupports(Transform parent, string name, float[] xs, float[] zs, float height, Material material)
    {
        float half = SupportSize * 0.5f;

        foreach (float x in xs)
        {
            foreach (float z in zs)
            {
                MapPartFactory.CreateBoxFromBounds(parent, name,
                    new Vector3(x - half, 0f, z - half), new Vector3(x + half, height, z + half), material);
            }
        }
    }

    // x축 또는 z축과 나란한 난간. 일정 간격의 기둥과 위, 가운데 두 줄의 가로대로 이루어진다.
    private static void CreateRailing(Transform parent, string name, Vector3 start, Vector3 end, Material material)
    {
        Transform railing = MapPartFactory.CreateGroup(parent, name);
        float half = RailingThickness * 0.5f;
        Vector3 halfExtent = new Vector3(half, 0f, half);

        int postCount = Mathf.CeilToInt(Vector3.Distance(start, end) / RailingPostSpacing) + 1;

        for (int i = 0; i < postCount; i++)
        {
            Vector3 position = Vector3.Lerp(start, end, postCount == 1 ? 0f : (float)i / (postCount - 1));

            MapPartFactory.CreateBoxFromBounds(railing, "Post",
                position - halfExtent, position + halfExtent + Vector3.up * RailingHeight, material);
        }

        foreach (float heightRatio in new[] { 0.5f, 1f })
        {
            Vector3 lift = Vector3.up * (RailingHeight * heightRatio);

            MapPartFactory.CreateBoxFromBounds(railing, "Rail",
                Vector3.Min(start, end) + lift - halfExtent,
                Vector3.Max(start, end) + lift + halfExtent + Vector3.up * RailingThickness, material);
        }
    }

    private static void CreateRampEdgeStripes(Transform parent, Vector3 bottom, Vector3 top, Material material)
    {
        const float stripeWidth = 0.2f;
        const float stripeLift = 0.02f;
        float offsetZ = RampWidth * 0.5f - stripeWidth * 0.5f;

        foreach (float sign in new[] { -1f, 1f })
        {
            Vector3 lift = Vector3.up * stripeLift;
            Vector3 stripeBottom = bottom + lift + Vector3.forward * (sign * offsetZ);
            Vector3 stripeTop = top + lift + Vector3.forward * (sign * offsetZ);

            MapPartFactory.CreateRamp(parent, "RampStripe", stripeBottom, stripeTop, stripeWidth, DeckThickness, material);
        }
    }

    private static float NextFloat(System.Random random, float min, float max)
    {
        return min + (float)random.NextDouble() * (max - min);
    }

    #endregion
}
