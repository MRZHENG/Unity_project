using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class AIExperimentSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/AITestScene.unity";

    [MenuItem("Tools/AI Lab/Build Experiment 6.3 Scene")]
    public static void BuildSceneFromMenu()
    {
        BuildScene();
    }

    private static void BuildScene()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath) ?? "Assets/Scenes");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateCamera();
        CreateLighting();
        CreateEnvironment();

        Canvas canvas = CreateCanvas();
        CreateEventSystem();

        Text responseText;
        InputField inputField;
        Button sendButton;

        CreateChatInterface(canvas.transform, out responseText, out inputField, out sendButton);
        CreateGameManager(inputField, responseText, sendButton);

        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/WhiteboxTowerDefense.unity", true),
            new EditorBuildSettingsScene(ScenePath, true)
        };

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Experiment 6.3 scene created at {ScenePath}");
    }

    private static void CreateCamera()
    {
        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 6f, -10f);
        cameraObject.transform.rotation = Quaternion.Euler(24f, 0f, 0f);

        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.93f, 0.95f, 0.98f);
    }

    private static void CreateLighting()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.86f, 0.88f, 0.92f);

        var lightObject = new GameObject("Directional Light");
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        light.shadows = LightShadows.Soft;
    }

    private static void CreateEnvironment()
    {
        CreatePrimitive("Ground", PrimitiveType.Plane, new Vector3(0f, 0f, 0f), new Vector3(3f, 1f, 3f), new Color(0.90f, 0.92f, 0.95f));
        CreatePrimitive("Backdrop", PrimitiveType.Cube, new Vector3(0f, 2.5f, 4.5f), new Vector3(10f, 5f, 0.4f), new Color(0.78f, 0.84f, 0.91f));
        CreatePrimitive("AccentCube", PrimitiveType.Cube, new Vector3(-2.8f, 0.5f, 2f), new Vector3(1f, 1f, 1f), new Color(0.63f, 0.76f, 0.89f));
        CreatePrimitive("AccentSphere", PrimitiveType.Sphere, new Vector3(2.5f, 0.7f, 1.6f), new Vector3(1.2f, 1.2f, 1.2f), new Color(0.90f, 0.71f, 0.51f));
    }

    private static GameObject CreatePrimitive(string name, PrimitiveType type, Vector3 position, Vector3 scale, Color color)
    {
        var primitive = GameObject.CreatePrimitive(type);
        primitive.name = name;
        primitive.transform.position = position;
        primitive.transform.localScale = scale;

        var renderer = primitive.GetComponent<Renderer>();
        if (renderer != null)
        {
            var material = new Material(Shader.Find("Standard"));
            material.color = color;
            renderer.sharedMaterial = material;
        }

        return primitive;
    }

    private static Canvas CreateCanvas()
    {
        var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    private static void CreateEventSystem()
    {
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private static void CreateChatInterface(Transform canvasRoot, out Text responseText, out InputField inputField, out Button sendButton)
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var panel = CreateUiObject("MainPanel", canvasRoot);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.14f, 0.12f);
        panelRect.anchorMax = new Vector2(0.86f, 0.88f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.08f, 0.12f, 0.18f, 0.86f);

        CreateTitleText(
            panel.transform,
            font,
            "实验 6.3 - Unity 智谱 AI 对话测试");

        GameObject scrollView = CreateUiObject("ChatScrollView", panel.transform);
        RectTransform scrollRect = scrollView.GetComponent<RectTransform>();
        scrollRect.anchorMin = new Vector2(0f, 0f);
        scrollRect.anchorMax = new Vector2(1f, 1f);
        scrollRect.offsetMin = new Vector2(20f, 120f);
        scrollRect.offsetMax = new Vector2(-20f, -100f);

        var scrollImage = scrollView.AddComponent<Image>();
        scrollImage.color = new Color(1f, 1f, 1f, 0.08f);

        var scroll = scrollView.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        GameObject viewport = CreateUiObject("Viewport", scrollView.transform);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(8f, 8f);
        viewportRect.offsetMax = new Vector2(-8f, -8f);

        var viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.03f);
        var mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject content = CreateUiObject("Content", viewport.transform);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.offsetMin = new Vector2(0f, 0f);
        contentRect.offsetMax = new Vector2(0f, 0f);

        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        responseText = CreateText(
            "ResponseText",
            content.transform,
            font,
            "等待提问...",
            26,
            TextAnchor.UpperLeft,
            Color.white,
            new Vector2(16f, -16f),
            new Vector2(-16f, 0f));
        responseText.horizontalOverflow = HorizontalWrapMode.Wrap;
        responseText.verticalOverflow = VerticalWrapMode.Overflow;
        responseText.supportRichText = true;
        var responseFitter = responseText.gameObject.AddComponent<ContentSizeFitter>();
        responseFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewportRect;
        scroll.content = contentRect;

        GameObject inputRoot = CreateUiObject("InputRoot", panel.transform);
        RectTransform inputRootRect = inputRoot.GetComponent<RectTransform>();
        inputRootRect.anchorMin = new Vector2(0f, 0f);
        inputRootRect.anchorMax = new Vector2(1f, 0f);
        inputRootRect.offsetMin = new Vector2(20f, 20f);
        inputRootRect.offsetMax = new Vector2(-20f, 80f);

        GameObject inputObject = CreateUiObject("InputField", inputRoot.transform);
        RectTransform inputRect = inputObject.GetComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0f, 0f);
        inputRect.anchorMax = new Vector2(1f, 1f);
        inputRect.offsetMin = new Vector2(0f, 0f);
        inputRect.offsetMax = new Vector2(-220f, 0f);

        var inputImage = inputObject.AddComponent<Image>();
        inputImage.color = new Color(1f, 1f, 1f, 0.92f);

        inputField = inputObject.AddComponent<InputField>();
        inputField.lineType = InputField.LineType.SingleLine;
        inputField.textComponent = CreateText(
            "Text",
            inputObject.transform,
            font,
            string.Empty,
            24,
            TextAnchor.MiddleLeft,
            new Color(0.12f, 0.16f, 0.22f),
            new Vector2(18f, 14f),
            new Vector2(-18f, -14f));

        Text placeholder = CreateText(
            "Placeholder",
            inputObject.transform,
            font,
            "输入您的问题...",
            24,
            TextAnchor.MiddleLeft,
            new Color(0.55f, 0.58f, 0.64f),
            new Vector2(18f, 14f),
            new Vector2(-18f, -14f));
        placeholder.fontStyle = FontStyle.Italic;
        inputField.placeholder = placeholder;

        GameObject buttonObject = CreateUiObject("SendButton", inputRoot.transform);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 0f);
        buttonRect.anchorMax = new Vector2(1f, 1f);
        buttonRect.pivot = new Vector2(1f, 0.5f);
        buttonRect.sizeDelta = new Vector2(180f, 0f);
        buttonRect.anchoredPosition = Vector2.zero;

        var buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = new Color(0.24f, 0.56f, 0.91f);

        sendButton = buttonObject.AddComponent<Button>();
        ColorBlock colors = sendButton.colors;
        colors.highlightedColor = new Color(0.30f, 0.62f, 0.96f);
        colors.pressedColor = new Color(0.18f, 0.44f, 0.76f);
        sendButton.colors = colors;

        CreateText(
            "Text",
            buttonObject.transform,
            font,
            "发送",
            28,
            TextAnchor.MiddleCenter,
            Color.white,
            Vector2.zero,
            Vector2.zero);
    }

    private static void CreateGameManager(InputField inputField, Text responseText, Button sendButton)
    {
        var gameManager = new GameObject("GameManager");
        var aiTest = gameManager.AddComponent<AITest>();

        var serializedObject = new SerializedObject(aiTest);
        serializedObject.FindProperty("inputField").objectReferenceValue = inputField;
        serializedObject.FindProperty("responseText").objectReferenceValue = responseText;
        serializedObject.FindProperty("sendButton").objectReferenceValue = sendButton;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static Text CreateTitleText(Transform parent, Font font, string content)
    {
        var titleObject = CreateUiObject("Title", parent);
        var rectTransform = titleObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(1f, 1f);
        rectTransform.pivot = new Vector2(0.5f, 1f);
        rectTransform.anchoredPosition = new Vector2(0f, -18f);
        rectTransform.sizeDelta = new Vector2(-40f, 56f);

        var text = titleObject.AddComponent<Text>();
        text.font = font;
        text.text = content;
        text.fontSize = 34;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = Color.white;
        text.supportRichText = false;
        return text;
    }

    private static Text CreateText(
        string name,
        Transform parent,
        Font font,
        string content,
        int fontSize,
        TextAnchor alignment,
        Color color,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        var textObject = CreateUiObject(name, parent);
        var rectTransform = textObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0f, 0f);
        rectTransform.anchorMax = new Vector2(1f, 1f);
        rectTransform.offsetMin = offsetMin;
        rectTransform.offsetMax = offsetMax;

        var text = textObject.AddComponent<Text>();
        text.font = font;
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.supportRichText = false;
        return text;
    }
}
