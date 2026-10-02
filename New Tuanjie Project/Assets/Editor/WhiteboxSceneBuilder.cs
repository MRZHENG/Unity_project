using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class WhiteboxSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/WhiteboxTowerDefense.unity";
    private static readonly Dictionary<string, Material> Materials = new();
    private static readonly Vector3[] PathNodePositions =
    {
        new(0f, 0.4f, 19f),
        new(0f, 0.4f, 9f),
        new(-14f, 0.4f, 9f),
        new(-14f, 0.4f, -3f),
        new(12f, 0.4f, -3f),
        new(12f, 0.4f, -15f),
        new(0f, 0.4f, -15f),
        new(0f, 0.4f, -12.5f)
    };

    private static readonly Vector3[] TowerSlotPositions =
    {
        new(-4.8f, 0.2f, 14.2f),
        new(4.8f, 0.2f, 14.2f),
        new(-7.6f, 0.2f, 12.2f),
        new(-18.0f, 0.2f, 4.0f),
        new(-9.2f, 0.2f, -7.5f),
        new(-2.8f, 0.2f, 1.0f),
        new(5.8f, 0.2f, 1.0f),
        new(16.2f, 0.2f, -9.2f)
    };

    [MenuItem("Tools/Whitebox/Build Tower Defense Scene")]
    public static void BuildFromMenu()
    {
        BuildScene();
    }

    public static void BuildSceneFromBatchMode()
    {
        BuildScene();
    }

    private static void BuildScene()
    {
        Materials.Clear();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Transform environmentRoot = new GameObject("Environment").transform;
        Transform gameplayRoot = new GameObject("Gameplay").transform;
        Transform markersRoot = new GameObject("SceneMarkers").transform;

        CreateLighting(environmentRoot);
        CreateCamera(environmentRoot);
        CreateEnvironment(environmentRoot);

        Transform pathRoot = CreateGameplayMarkers(gameplayRoot, out Transform spawnPoint, out Transform slotsRoot);
        CreateSceneMarkers(markersRoot);

        TowerDefensePath path = pathRoot.gameObject.AddComponent<TowerDefensePath>();
        path.RebuildFromChildren();

        Transform runtimeRoot = new GameObject("Runtime").transform;
        TowerDefenseWaveSpawner waveSpawner = runtimeRoot.gameObject.AddComponent<TowerDefenseWaveSpawner>();
        TowerDefenseGameManager gameManager = runtimeRoot.gameObject.AddComponent<TowerDefenseGameManager>();
        TowerDefenseStrategyPlanner strategyPlanner = runtimeRoot.gameObject.AddComponent<TowerDefenseStrategyPlanner>();

        CreateCanvas(out TowerDefenseHud hud);
        CreateEventSystem();

        SerializedObject serializedSpawner = new(waveSpawner);
        serializedSpawner.FindProperty("spawnPoint").objectReferenceValue = spawnPoint;
        serializedSpawner.FindProperty("path").objectReferenceValue = path;
        serializedSpawner.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject serializedGameManager = new(gameManager);
        serializedGameManager.FindProperty("waveSpawner").objectReferenceValue = waveSpawner;
        serializedGameManager.FindProperty("hud").objectReferenceValue = hud;
        serializedGameManager.FindProperty("strategyPlanner").objectReferenceValue = strategyPlanner;
        serializedGameManager.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject serializedStrategyPlanner = new(strategyPlanner);
        serializedStrategyPlanner.FindProperty("apiKey").stringValue = "3664853fa93143f796ee2f89705fff11.hzsZ12iHdGg1D7x5";
        serializedStrategyPlanner.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(ScenePath, true)
        };

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Whitebox scene created at {ScenePath}");
    }

    private static void CreateLighting(Transform parent)
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.82f, 0.84f, 0.88f);

        GameObject lightObject = new("Directional Light");
        lightObject.transform.SetParent(parent);
        lightObject.transform.rotation = Quaternion.Euler(58f, -25f, 0f);

        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.25f;
        light.shadows = LightShadows.Soft;
    }

    private static void CreateCamera(Transform parent)
    {
        GameObject cameraObject = new("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetParent(parent);
        cameraObject.transform.position = new Vector3(0f, 34f, -8f);
        cameraObject.transform.rotation = Quaternion.Euler(78f, 0f, 0f);

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.90f, 0.93f, 0.97f);
        camera.orthographic = true;
        camera.orthographicSize = 17f;
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 100f;
    }

    private static void CreateEnvironment(Transform parent)
    {
        CreatePrimitive(PrimitiveType.Plane, "Ground", parent, new Vector3(0f, 0f, 0f), Vector3.zero, new Vector3(4.2f, 1f, 4.2f), new Color(0.93f, 0.94f, 0.96f));
        CreatePrimitive(PrimitiveType.Cube, "BasePlatform", parent, new Vector3(0f, 0.16f, -12.5f), Vector3.zero, new Vector3(10f, 0.3f, 7f), new Color(0.85f, 0.88f, 0.92f));
        CreatePrimitive(PrimitiveType.Cube, "UiObservationZone", parent, new Vector3(0f, 0.05f, -18f), Vector3.zero, new Vector3(18f, 0.1f, 7f), new Color(0.89f, 0.91f, 0.94f));

        CreateBorderWall("NorthWall", new Vector3(0f, 0.9f, 20f), new Vector3(42f, 1.8f, 0.8f), parent);
        CreateBorderWall("SouthWall", new Vector3(0f, 0.9f, -20f), new Vector3(42f, 1.8f, 0.8f), parent);
        CreateBorderWall("WestWall", new Vector3(-20f, 0.9f, 0f), new Vector3(0.8f, 1.8f, 42f), parent);
        CreateBorderWall("EastWall", new Vector3(20f, 0.9f, 0f), new Vector3(0.8f, 1.8f, 42f), parent);

        Transform pathsRoot = new GameObject("Paths").transform;
        pathsRoot.SetParent(parent);
        CreateGeneratedPathSegments(pathsRoot, PathNodePositions);

        CreateObstacleLayout(parent);
    }

    private static Transform CreateGameplayMarkers(Transform parent, out Transform spawnPoint, out Transform slotsRoot)
    {
        Transform pathRoot = new GameObject("PathNodes").transform;
        pathRoot.SetParent(parent);

        CreatePrimitive(PrimitiveType.Cube, "SpawnGate", parent, new Vector3(0f, 1.4f, 18.8f), Vector3.zero, new Vector3(4.8f, 2.8f, 0.8f), new Color(0.77f, 0.36f, 0.34f));
        spawnPoint = CreateMarker("SpawnPoint", PrimitiveType.Cylinder, new Vector3(0f, 0.2f, 17.5f), new Vector3(2.4f, 0.2f, 2.4f), new Color(0.89f, 0.47f, 0.44f), parent).transform;

        for (int index = 0; index < PathNodePositions.Length; index++)
        {
            CreateMarker($"PathNode_{index + 1:00}", PrimitiveType.Sphere, PathNodePositions[index], Vector3.one * 0.42f, new Color(0.56f, 0.70f, 0.90f), pathRoot);
        }

        CreatePathDirectionMarkers(PathNodePositions, parent);

        CreatePrimitive(PrimitiveType.Cube, "BaseBackWall", parent, new Vector3(0f, 1.1f, -15.6f), Vector3.zero, new Vector3(10f, 2.2f, 0.6f), new Color(0.73f, 0.78f, 0.84f));
        CreateMarker("BaseCore", PrimitiveType.Cylinder, new Vector3(0f, 0.6f, -12.5f), new Vector3(2.2f, 1.2f, 2.2f), new Color(0.94f, 0.84f, 0.34f), parent);

        slotsRoot = new GameObject("TowerSlots").transform;
        slotsRoot.SetParent(parent);
        Transform pedestalsRoot = new GameObject("TowerPedestals").transform;
        pedestalsRoot.SetParent(parent);

        for (int index = 0; index < TowerSlotPositions.Length; index++)
        {
            GameObject slotPad = CreatePrimitive(
                PrimitiveType.Cylinder,
                $"TowerSlot_{index + 1:00}",
                slotsRoot,
                TowerSlotPositions[index] + new Vector3(0f, 0.03f, 0f),
                Vector3.zero,
                new Vector3(2.3f, 0.06f, 2.3f),
                new Color(0.37f, 0.71f, 0.67f));

            CreatePrimitive(
                PrimitiveType.Cylinder,
                $"TowerSlotMarker_{index + 1:00}",
                slotPad.transform,
                new Vector3(0f, 0.18f, 0f),
                Vector3.zero,
                new Vector3(1.1f, 0.08f, 1.1f),
                new Color(0.19f, 0.52f, 0.48f));

            Collider markerCollider = slotPad.transform.Find($"TowerSlotMarker_{index + 1:00}")?.GetComponent<Collider>();
            if (markerCollider != null)
            {
                Object.DestroyImmediate(markerCollider);
            }

            slotPad.AddComponent<TowerDefenseTowerSlot>();

            CreatePrimitive(
                PrimitiveType.Cube,
                $"TowerPedestal_{index + 1:00}",
                pedestalsRoot,
                TowerSlotPositions[index] + new Vector3(0f, -0.08f, 0f),
                Vector3.zero,
                new Vector3(1.2f, 0.08f, 1.2f),
                new Color(0.27f, 0.34f, 0.35f));
        }

        return pathRoot;
    }

    private static void CreateSceneMarkers(Transform parent)
    {
        CreatePrimitive(PrimitiveType.Cube, "WaveInfoBoard", parent, new Vector3(0f, 0.12f, -18f), Vector3.zero, new Vector3(14f, 0.08f, 1.2f), new Color(0.79f, 0.84f, 0.90f));
        CreatePrimitive(PrimitiveType.Cube, "StrategyPanel", parent, new Vector3(15.3f, 1.05f, -9.5f), Vector3.zero, new Vector3(0.6f, 2.1f, 5.4f), new Color(0.78f, 0.83f, 0.89f));
    }

    private static void CreatePathDirectionMarkers(Vector3[] nodePositions, Transform parent)
    {
        Transform arrowRoot = new GameObject("PathDirectionMarkers").transform;
        arrowRoot.SetParent(parent);

        for (int index = 0; index < nodePositions.Length - 1; index++)
        {
            Vector3 start = nodePositions[index];
            Vector3 end = nodePositions[index + 1];
            Vector3 direction = (end - start).normalized;
            float distance = Vector3.Distance(start, end);
            int arrowCount = Mathf.Max(1, Mathf.FloorToInt(distance / 4f));

            for (int arrowIndex = 0; arrowIndex < arrowCount; arrowIndex++)
            {
                float t = (arrowIndex + 1f) / (arrowCount + 1f);
                Vector3 center = Vector3.Lerp(start, end, t) + Vector3.up * 0.08f;
                float angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

                GameObject body = CreatePrimitive(
                    PrimitiveType.Cube,
                    $"PathArrowBody_{index + 1:00}_{arrowIndex + 1:00}",
                    arrowRoot,
                    center - direction * 0.55f,
                    new Vector3(0f, angle, 0f),
                    new Vector3(0.22f, 0.06f, 1.05f),
                    new Color(0.20f, 0.55f, 0.96f));
                Object.DestroyImmediate(body.GetComponent<Collider>());

                GameObject head = CreatePrimitive(
                    PrimitiveType.Cube,
                    $"PathArrowHead_{index + 1:00}_{arrowIndex + 1:00}",
                    arrowRoot,
                    center + direction * 0.18f,
                    new Vector3(0f, angle, 0f),
                    new Vector3(0.62f, 0.06f, 0.62f),
                    new Color(0.12f, 0.44f, 0.91f));
                head.transform.localScale = new Vector3(0.62f, 0.06f, 0.62f);
                head.transform.rotation = Quaternion.Euler(0f, angle + 45f, 0f);
                Object.DestroyImmediate(head.GetComponent<Collider>());
            }
        }
    }

    private static void CreateCanvas(out TowerDefenseHud hud)
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject canvasObject = new("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panel = CreateUiObject("HudPanel", canvas.transform);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.02f, 0.02f);
        panelRect.anchorMax = new Vector2(0.44f, 0.50f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.08f, 0.12f, 0.16f, 0.82f);

        Text titleText = CreateText("TitleText", panel.transform, font, "智能战术指挥塔防 - 白盒原型", 30, TextAnchor.MiddleLeft, Color.white, new Vector2(20f, -18f), new Vector2(-20f, -68f));
        titleText.fontStyle = FontStyle.Bold;

        Text resourcesText = CreateText("ResourcesText", panel.transform, font, "资源：0", 24, TextAnchor.MiddleLeft, Color.white, new Vector2(20f, -78f), new Vector2(-20f, -118f));
        Text baseHealthText = CreateText("BaseHealthText", panel.transform, font, "基地生命：0", 24, TextAnchor.MiddleLeft, Color.white, new Vector2(20f, -118f), new Vector2(-20f, -158f));
        Text waveText = CreateText("WaveText", panel.transform, font, "波次：0/0", 24, TextAnchor.MiddleLeft, Color.white, new Vector2(20f, -158f), new Vector2(-20f, -198f));
        Text selectionText = CreateText("SelectionText", panel.transform, font, "当前塔型：Arrow", 21, TextAnchor.UpperLeft, Color.white, new Vector2(20f, -198f), new Vector2(-20f, -292f));
        Text strategyText = CreateText("StrategyText", panel.transform, font, "策略摘要：等待开始第一波。", 20, TextAnchor.UpperLeft, Color.white, new Vector2(20f, -298f), new Vector2(-20f, -382f));
        Text statusText = CreateText("StatusText", panel.transform, font, "状态：待机", 20, TextAnchor.UpperLeft, new Color(0.96f, 0.89f, 0.56f), new Vector2(20f, -388f), new Vector2(-180f, -456f));

        GameObject buttonObject = CreateUiObject("NextWaveButton", panel.transform);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 1f);
        buttonRect.anchorMax = new Vector2(1f, 1f);
        buttonRect.pivot = new Vector2(1f, 1f);
        buttonRect.anchoredPosition = new Vector2(-20f, -20f);
        buttonRect.sizeDelta = new Vector2(170f, 48f);

        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = new Color(0.24f, 0.56f, 0.91f);
        Button nextWaveButton = buttonObject.AddComponent<Button>();

        CreateText("Text", buttonObject.transform, font, "开始下一波", 22, TextAnchor.MiddleCenter, Color.white, Vector2.zero, Vector2.zero);

        hud = panel.AddComponent<TowerDefenseHud>();
        SerializedObject serializedHud = new(hud);
        serializedHud.FindProperty("resourcesText").objectReferenceValue = resourcesText;
        serializedHud.FindProperty("baseHealthText").objectReferenceValue = baseHealthText;
        serializedHud.FindProperty("waveText").objectReferenceValue = waveText;
        serializedHud.FindProperty("selectionText").objectReferenceValue = selectionText;
        serializedHud.FindProperty("strategyText").objectReferenceValue = strategyText;
        serializedHud.FindProperty("statusText").objectReferenceValue = statusText;
        serializedHud.FindProperty("nextWaveButton").objectReferenceValue = nextWaveButton;
        serializedHud.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateEventSystem()
    {
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private static void CreateBorderWall(string name, Vector3 position, Vector3 scale, Transform parent)
    {
        CreatePrimitive(PrimitiveType.Cube, name, parent, position, Vector3.zero, scale, new Color(0.72f, 0.76f, 0.82f));
    }

    private static void CreateObstacleLayout(Transform parent)
    {
        Transform obstacleRoot = new GameObject("ObstacleLayout").transform;
        obstacleRoot.SetParent(parent);

        Color obstacleColor = new Color(0.80f, 0.84f, 0.88f);

        CreatePrimitive(PrimitiveType.Cube, "Obstacle_01", obstacleRoot, new Vector3(-10.8f, 0.35f, 13.8f), new Vector3(0f, 18f, 0f), new Vector3(1.8f, 0.7f, 0.6f), obstacleColor);
        CreatePrimitive(PrimitiveType.Cube, "Obstacle_02", obstacleRoot, new Vector3(8.8f, 0.35f, 13.4f), new Vector3(0f, -14f, 0f), new Vector3(2.0f, 0.7f, 0.6f), obstacleColor);
        CreatePrimitive(PrimitiveType.Cube, "Obstacle_03", obstacleRoot, new Vector3(-17.8f, 0.35f, -8.5f), new Vector3(0f, 36f, 0f), new Vector3(1.5f, 0.7f, 0.55f), obstacleColor);
        CreatePrimitive(PrimitiveType.Cube, "Obstacle_04", obstacleRoot, new Vector3(-1.5f, 0.35f, 5.6f), new Vector3(0f, -22f, 0f), new Vector3(1.4f, 0.7f, 0.55f), obstacleColor);
        CreatePrimitive(PrimitiveType.Cube, "Obstacle_05", obstacleRoot, new Vector3(16.2f, 0.35f, 4.2f), new Vector3(0f, 28f, 0f), new Vector3(1.7f, 0.7f, 0.55f), obstacleColor);
        CreatePrimitive(PrimitiveType.Cube, "Obstacle_06", obstacleRoot, new Vector3(16.8f, 0.35f, -13.8f), new Vector3(0f, -32f, 0f), new Vector3(1.4f, 0.7f, 0.55f), obstacleColor);
        CreatePrimitive(PrimitiveType.Cube, "Obstacle_07", obstacleRoot, new Vector3(-8.2f, 0.35f, -17.0f), new Vector3(0f, 12f, 0f), new Vector3(1.9f, 0.7f, 0.55f), obstacleColor);
    }

    private static void CreatePathSegment(string name, Vector3 position, Vector3 scale, Transform parent)
    {
        GameObject roadSegment = CreatePrimitive(
            PrimitiveType.Cube,
            name,
            parent,
            position,
            Vector3.zero,
            new Vector3(scale.x, 0.14f, scale.z),
            new Color(0.28f, 0.32f, 0.38f));

        CreateRoadDecorations(roadSegment.transform, scale);
    }

    private static void CreateGeneratedPathSegments(Transform parent, IReadOnlyList<Vector3> nodePositions)
    {
        const float roadWidth = 3.2f;

        for (int index = 0; index < nodePositions.Count - 1; index++)
        {
            Vector3 start = nodePositions[index];
            Vector3 end = nodePositions[index + 1];
            Vector3 center = (start + end) * 0.5f;
            Vector3 delta = end - start;

            bool horizontal = Mathf.Abs(delta.x) >= Mathf.Abs(delta.z);
            float length = horizontal ? Mathf.Abs(delta.x) : Mathf.Abs(delta.z);
            Vector3 scale = horizontal
                ? new Vector3(length, 0.08f, roadWidth)
                : new Vector3(roadWidth, 0.08f, length);

            CreatePathSegment($"PathSegment_{index + 1:00}", new Vector3(center.x, 0.06f, center.z), scale, parent);
        }

        for (int index = 1; index < nodePositions.Count - 1; index++)
        {
            CreatePathSegment(
                $"PathCorner_{index:00}",
                new Vector3(nodePositions[index].x, 0.06f, nodePositions[index].z),
                new Vector3(roadWidth, 0.08f, roadWidth),
                parent);
        }
    }

    private static void CreateRoadDecorations(Transform roadSegment, Vector3 scale)
    {
        bool horizontal = scale.x >= scale.z;
        float roadWidth = horizontal ? scale.z : scale.x;
        float roadLength = horizontal ? scale.x : scale.z;
        float edgeOffset = roadWidth * 0.5f - 0.12f;

        Vector3 leftOffset = horizontal ? new Vector3(0f, 0.09f, -edgeOffset) : new Vector3(-edgeOffset, 0.09f, 0f);
        Vector3 rightOffset = horizontal ? new Vector3(0f, 0.09f, edgeOffset) : new Vector3(edgeOffset, 0.09f, 0f);
        Vector3 edgeScale = horizontal ? new Vector3(roadLength * 0.98f, 0.03f, 0.10f) : new Vector3(0.10f, 0.03f, roadLength * 0.98f);

        GameObject leftEdge = CreatePrimitive(
            PrimitiveType.Cube,
            "RoadEdge_Left",
            roadSegment,
            leftOffset,
            Vector3.zero,
            edgeScale,
            new Color(0.82f, 0.88f, 0.94f));
        Object.DestroyImmediate(leftEdge.GetComponent<Collider>());

        GameObject rightEdge = CreatePrimitive(
            PrimitiveType.Cube,
            "RoadEdge_Right",
            roadSegment,
            rightOffset,
            Vector3.zero,
            edgeScale,
            new Color(0.82f, 0.88f, 0.94f));
        Object.DestroyImmediate(rightEdge.GetComponent<Collider>());

        int dashCount = Mathf.Max(2, Mathf.FloorToInt(roadLength / 3.5f));
        for (int index = 0; index < dashCount; index++)
        {
            float t = dashCount == 1 ? 0.5f : index / (float)(dashCount - 1);
            Vector3 dashOffset = horizontal
                ? new Vector3(Mathf.Lerp(-roadLength * 0.42f, roadLength * 0.42f, t), 0.09f, 0f)
                : new Vector3(0f, 0.09f, Mathf.Lerp(-roadLength * 0.42f, roadLength * 0.42f, t));

            GameObject dash = CreatePrimitive(
                PrimitiveType.Cube,
                $"RoadDash_{index + 1:00}",
                roadSegment,
                dashOffset,
                Vector3.zero,
                horizontal ? new Vector3(0.58f, 0.025f, 0.12f) : new Vector3(0.12f, 0.025f, 0.58f),
                new Color(0.98f, 0.90f, 0.46f));
            Object.DestroyImmediate(dash.GetComponent<Collider>());
        }
    }

    private static GameObject CreateMarker(string name, PrimitiveType primitiveType, Vector3 position, Vector3 scale, Color color, Transform parent)
    {
        return CreatePrimitive(primitiveType, name, parent, position, Vector3.zero, scale, color);
    }

    private static GameObject CreatePrimitive(PrimitiveType primitiveType, string name, Transform parent, Vector3 position, Vector3 rotation, Vector3 scale, Color color)
    {
        GameObject gameObject = GameObject.CreatePrimitive(primitiveType);
        gameObject.name = name;
        gameObject.transform.SetParent(parent);
        gameObject.transform.localPosition = position;
        gameObject.transform.localEulerAngles = rotation;
        gameObject.transform.localScale = scale;

        Renderer renderer = gameObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = GetMaterial(color);
        }

        return gameObject;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject gameObject = new(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static Text CreateText(string name, Transform parent, Font font, string content, int fontSize, TextAnchor alignment, Color color, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject textObject = CreateUiObject(name, parent);
        RectTransform rectTransform = textObject.GetComponent<RectTransform>();

        if (offsetMin == Vector2.zero && offsetMax == Vector2.zero)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
        else
        {
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 1f);
            rectTransform.offsetMin = new Vector2(offsetMin.x, offsetMax.y);
            rectTransform.offsetMax = new Vector2(offsetMax.x, offsetMin.y);
        }

        Text text = textObject.AddComponent<Text>();
        text.font = font;
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.supportRichText = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static Material GetMaterial(Color color)
    {
        string key = ColorUtility.ToHtmlStringRGBA(color);
        if (Materials.TryGetValue(key, out Material material))
        {
            return material;
        }

        Shader shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
        material = new Material(shader) { color = color };
        Materials[key] = material;
        return material;
    }
}
