using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using Unity.MLAgents;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MLAgentsSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/MLAgents3DBall.unity";

    [MenuItem("Tools/AI Lab/Build ML-Agents 3DBall Scene")]
    public static void BuildSceneFromMenu()
    {
        BuildScene();
    }

    public static void BuildScene()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath) ?? "Assets/Scenes");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateCamera();
        CreateLighting();
        CreateGround();
        CreateAcademyRoot();
        CreateTrainingArea();

        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"ML-Agents sample scene created at {ScenePath}");
    }

    private static void CreateCamera()
    {
        GameObject cameraObject = new("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 9f, -11f);
        cameraObject.transform.rotation = Quaternion.Euler(28f, 0f, 0f);

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.91f, 0.95f, 0.99f);
    }

    private static void CreateLighting()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.82f, 0.86f, 0.92f);

        GameObject lightObject = new("Directional Light");
        lightObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.15f;
        light.shadows = LightShadows.Soft;
    }

    private static void CreateGround()
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(3f, 1f, 3f);

        Renderer renderer = ground.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material material = new(Shader.Find("Standard"));
            material.color = new Color(0.88f, 0.91f, 0.95f);
            renderer.sharedMaterial = material;
        }
    }

    private static void CreateAcademyRoot()
    {
        new GameObject("MLAgentsAcademy");
    }

    private static void CreateTrainingArea()
    {
        GameObject areaRoot = new("3DBallArea");

        GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
        platform.name = "Platform";
        platform.transform.SetParent(areaRoot.transform);
        platform.transform.position = new Vector3(0f, 1.2f, 0f);
        platform.transform.localScale = new Vector3(6f, 0.5f, 6f);

        Rigidbody platformRigidBody = platform.AddComponent<Rigidbody>();
        platformRigidBody.useGravity = false;
        platformRigidBody.angularDrag = 2f;
        platformRigidBody.drag = 1f;
        platformRigidBody.constraints = RigidbodyConstraints.FreezePosition;

        Renderer platformRenderer = platform.GetComponent<Renderer>();
        if (platformRenderer != null)
        {
            Material platformMaterial = new(Shader.Find("Standard"));
            platformMaterial.color = new Color(0.61f, 0.76f, 0.91f);
            platformRenderer.sharedMaterial = platformMaterial;
        }

        GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "Ball";
        ball.transform.SetParent(areaRoot.transform);
        ball.transform.position = new Vector3(0f, 2.4f, 0f);
        ball.transform.localScale = Vector3.one * 0.8f;

        Rigidbody ballRigidBody = ball.AddComponent<Rigidbody>();
        ballRigidBody.angularDrag = 0.05f;
        ballRigidBody.drag = 0.05f;

        Renderer ballRenderer = ball.GetComponent<Renderer>();
        if (ballRenderer != null)
        {
            Material ballMaterial = new(Shader.Find("Standard"));
            ballMaterial.color = new Color(0.94f, 0.69f, 0.45f);
            ballRenderer.sharedMaterial = ballMaterial;
        }

        BallBalanceAgent agent = platform.AddComponent<BallBalanceAgent>();
        agent.MaxStep = 2000;

        BehaviorParameters behavior = platform.GetComponent<BehaviorParameters>();
        if (behavior == null)
        {
            behavior = platform.AddComponent<BehaviorParameters>();
        }
        behavior.BehaviorName = "3DBall";
        behavior.BrainParameters.VectorObservationSize = 8;
        behavior.BrainParameters.NumStackedVectorObservations = 1;
        behavior.BrainParameters.ActionSpec = ActionSpec.MakeContinuous(2);
        behavior.BehaviorType = BehaviorType.Default;

        platform.AddComponent<DecisionRequester>().DecisionPeriod = 5;

        SerializedObject serializedAgent = new(agent);
        serializedAgent.FindProperty("platform").objectReferenceValue = platform.transform;
        serializedAgent.FindProperty("platformRigidbody").objectReferenceValue = platformRigidBody;
        serializedAgent.FindProperty("ballRigidbody").objectReferenceValue = ballRigidBody;
        serializedAgent.ApplyModifiedPropertiesWithoutUndo();
    }
}
