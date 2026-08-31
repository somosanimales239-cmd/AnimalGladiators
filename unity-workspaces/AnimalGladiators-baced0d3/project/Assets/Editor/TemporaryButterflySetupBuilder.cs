#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class TemporaryButterflySetupBuilder
{
    private const string ScenePath = "Assets/Scenes/AnimalGladiators_Arena.unity";
    private const string RequestPath = ".nexa-bridge/butterfly-setup-request.txt";
    private const string ResultPath = ".nexa-bridge/butterfly-setup-result.txt";
    private const string MonitorPath = ".nexa-bridge/butterfly-test-monitor.txt";
    private const string RuntimeReportPath = ".nexa-bridge/butterfly-test-report.txt";

    private static bool isRunning;
    private static bool runtimeInitialized;
    private static double runtimeStart;
    private static readonly Dictionary<string, Vector3> startPositions = new Dictionary<string, Vector3>();
    private static readonly Dictionary<string, Quaternion> startWingRotations = new Dictionary<string, Quaternion>();
    private static readonly HashSet<string> visibleButterflies = new HashSet<string>();
    private static readonly HashSet<string> movingWings = new HashSet<string>();
    private static bool independentPhasesObserved;
    private static bool remainedInHabitat = true;

    static TemporaryButterflySetupBuilder()
    {
        EditorApplication.update += Update;
    }

    private static void Update()
    {
        MonitorRuntime();

        if (isRunning || EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(RequestPath))
            return;

        isRunning = true;
        try
        {
            string command = File.ReadAllText(RequestPath).Trim();
            File.Delete(RequestPath);
            if (command == "setup")
                SetupScene();
            else if (command == "orient")
                FixVisualOrientation();
            else if (command == "test")
                StartRuntimeTest();
            else
                throw new InvalidOperationException("Unknown butterfly setup command: " + command);
        }
        catch (Exception exception)
        {
            File.WriteAllText(ResultPath, "ERROR\n" + exception);
            Debug.LogException(exception);
        }
        finally
        {
            isRunning = false;
        }
    }

    private static void FixVisualOrientation()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject environment = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Environment");
        Transform group = environment != null ? environment.transform.Find("Butterflies") : null;
        if (group == null)
            throw new InvalidOperationException("Environment/Butterflies was not found.");

        ConfigureVisualPivot(group.Find("Butterfly_01"), "Bone_017", "Bone_021", Vector3.forward, 9.5f);
        ConfigureVisualPivot(group.Find("Butterfly_02"), "Bone_010", "Bone_012", Vector3.right, 11.2f);
        ConfigureVisualPivot(group.Find("Butterfly_03"), "Bone_010", "Bone_008", Vector3.forward, 8.6f);

        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene);
        File.WriteAllText(ResultPath,
            "OK\n" +
            "VisualPivot created for Butterfly_01, Butterfly_02 and Butterfly_03.\n" +
            "Visual rotation X = 90; roots and natural motion untouched.\n" +
            "Scene saved.\n");
    }

    private static void ConfigureVisualPivot(
        Transform butterfly,
        string leftWingName,
        string rightWingName,
        Vector3 wingAxis,
        float flapFrequency)
    {
        if (butterfly == null)
            throw new InvalidOperationException("A butterfly root is missing.");

        if (PrefabUtility.IsPartOfPrefabInstance(butterfly.gameObject))
            PrefabUtility.UnpackPrefabInstance(
                PrefabUtility.GetOutermostPrefabInstanceRoot(butterfly.gameObject),
                PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);

        Transform pivot = butterfly.Find("VisualPivot");
        if (pivot == null)
        {
            GameObject pivotObject = new GameObject("VisualPivot");
            pivot = pivotObject.transform;
            pivot.SetParent(butterfly, false);
        }

        pivot.localPosition = Vector3.zero;
        pivot.localRotation = Quaternion.identity;
        pivot.localScale = Vector3.one;

        Transform[] visualChildren = butterfly.Cast<Transform>()
            .Where(child => child != pivot)
            .ToArray();
        foreach (Transform child in visualChildren)
            child.SetParent(pivot, false);

        pivot.localRotation = Quaternion.Euler(90f, 0f, 0f);

        ButterflyWingFlap flap = butterfly.GetComponent<ButterflyWingFlap>();
        if (flap == null)
            throw new InvalidOperationException(butterfly.name + " has no ButterflyWingFlap component.");

        Transform leftWing = FindTransform(pivot, leftWingName);
        Transform rightWing = FindTransform(pivot, rightWingName);
        if (leftWing == null || rightWing == null || leftWing == rightWing)
            throw new InvalidOperationException(butterfly.name + " does not have two independent wing-root bones.");

        flap.leftWing = leftWing;
        flap.rightWing = rightWing;
        flap.leftAxis = wingAxis;
        flap.rightAxis = wingAxis;
        flap.leftSign = 1f;
        flap.rightSign = -1f;
        flap.flapFrequency = flapFrequency;
        flap.randomizePhase = true;
        EditorUtility.SetDirty(flap);
    }

    private static void SetupScene()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject environment = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Environment");
        if (environment == null)
            throw new InvalidOperationException("Environment was not found in AnimalGladiators_Arena.");

        Transform previous = environment.transform.Find("Butterflies");
        if (previous != null)
            UnityEngine.Object.DestroyImmediate(previous.gameObject);

        GameObject butterflies = new GameObject("Butterflies");
        butterflies.transform.SetParent(environment.transform, false);
        butterflies.transform.localPosition = Vector3.zero;
        butterflies.transform.localRotation = Quaternion.identity;
        butterflies.transform.localScale = Vector3.one;

        GameObject habitatObject = new GameObject("ButterflyHabitat");
        habitatObject.transform.SetParent(butterflies.transform, false);

        GameObject airParent = new GameObject("AirPoints");
        airParent.transform.SetParent(habitatObject.transform, false);
        GameObject perchParent = new GameObject("PerchPoints");
        perchParent.transform.SetParent(habitatObject.transform, false);

        Vector3[] airPositions =
        {
            new Vector3(-3.50f, 0.68f, 2.55f),
            new Vector3(-2.95f, 1.32f, 3.05f),
            new Vector3(-2.30f, 0.92f, 2.20f),
            new Vector3(-1.55f, 1.62f, 2.75f),
            new Vector3(0.65f, 0.72f, 3.15f),
            new Vector3(1.25f, 1.38f, 2.45f),
            new Vector3(1.90f, 0.62f, 3.35f),
            new Vector3(2.45f, 1.12f, 2.25f)
        };

        Vector3[] perchPositions =
        {
            new Vector3(-3.55f, 0.04f, 2.35f),
            new Vector3(-2.85f, 0.04f, 3.10f),
            new Vector3(-1.95f, 0.04f, 2.15f),
            new Vector3(0.95f, 0.04f, 3.20f),
            new Vector3(1.70f, 0.04f, 2.50f),
            new Vector3(2.45f, 0.04f, 3.05f)
        };

        Transform[] airPoints = CreatePoints(airParent.transform, "Air", airPositions);
        Transform[] perchPoints = CreatePoints(perchParent.transform, "Perch", perchPositions);

        ButterflyHabitat habitat = habitatObject.AddComponent<ButterflyHabitat>();
        habitat.airPoints = airPoints;
        habitat.perchPoints = perchPoints;

        CreateButterfly(scene, butterflies.transform, habitat, 1, "Bone_017", "Bone_021", 55f, 9.5f, 0.30f, 0.48f, 3f, 7f, 0.096f, airPoints[0]);
        CreateButterfly(scene, butterflies.transform, habitat, 2, "Bone_010", "Bone_012", 52f, 11.2f, 0.36f, 0.58f, 5f, 10f, 0.120f, airPoints[3]);
        CreateButterfly(scene, butterflies.transform, habitat, 3, "Bone_010", "Bone_008", 58f, 8.6f, 0.27f, 0.52f, 2f, 6f, 0.0816f, airPoints[6]);

        EditorUtility.SetDirty(habitat);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene);
        File.WriteAllText(ResultPath,
            "OK\n" +
            "Butterflies created: 3\n" +
            "AirPoints: 8\n" +
            "PerchPoints: 6\n" +
            "Scene saved: " + ScenePath + "\n");
    }

    private static Transform[] CreatePoints(Transform parent, string prefix, Vector3[] positions)
    {
        Transform[] points = new Transform[positions.Length];
        for (int index = 0; index < positions.Length; index++)
        {
            GameObject point = new GameObject($"{prefix}_{index + 1:00}");
            point.transform.SetParent(parent, false);
            point.transform.position = positions[index];
            points[index] = point.transform;
        }
        return points;
    }

    private static void CreateButterfly(
        Scene scene,
        Transform parent,
        ButterflyHabitat habitat,
        int index,
        string leftWingName,
        string rightWingName,
        float flapAngle,
        float flapFrequency,
        float minMoveSpeed,
        float maxMoveSpeed,
        float minPerchTime,
        float maxPerchTime,
        float scale,
        Transform startPoint)
    {
        string assetPath = $"Assets/Environment/Butterflies/Butterfly_{index:00}.glb";
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (model == null)
            throw new InvalidOperationException(assetPath + " could not be loaded.");

        GameObject instance = PrefabUtility.InstantiatePrefab(model, scene) as GameObject;
        if (instance == null)
            throw new InvalidOperationException(assetPath + " could not be instantiated.");

        instance.name = $"Butterfly_{index:00}";
        instance.transform.SetParent(parent, true);
        instance.transform.position = startPoint.position;
        instance.transform.rotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one * scale;

        Transform leftWing = FindTransform(instance.transform, leftWingName);
        Transform rightWing = FindTransform(instance.transform, rightWingName);
        if (leftWing == null || rightWing == null || leftWing == rightWing)
            throw new InvalidOperationException($"Butterfly_{index:00} wing transforms are not independently available.");

        ButterflyWingFlap flap = instance.AddComponent<ButterflyWingFlap>();
        flap.leftWing = leftWing;
        flap.rightWing = rightWing;
        flap.leftAxis = leftWing.InverseTransformDirection(instance.transform.TransformDirection(Vector3.up)).normalized;
        flap.rightAxis = rightWing.InverseTransformDirection(instance.transform.TransformDirection(Vector3.up)).normalized;
        flap.leftSign = 1f;
        flap.rightSign = -1f;
        flap.flapAngle = flapAngle;
        flap.flapFrequency = flapFrequency;
        flap.randomizePhase = true;

        ButterflyNaturalMotion motion = instance.AddComponent<ButterflyNaturalMotion>();
        motion.habitat = habitat;
        motion.wingFlap = flap;
        motion.minMoveSpeed = minMoveSpeed;
        motion.maxMoveSpeed = maxMoveSpeed;
        motion.minPerchTime = minPerchTime;
        motion.maxPerchTime = maxPerchTime;

        EditorUtility.SetDirty(flap);
        EditorUtility.SetDirty(motion);
    }

    private static Transform FindTransform(Transform root, string name)
    {
        return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == name);
    }

    private static void StartRuntimeTest()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Unity is already in Play Mode.");

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveOpenScenes();
        File.WriteAllText(MonitorPath, "ARMED");
        if (File.Exists(RuntimeReportPath))
            File.Delete(RuntimeReportPath);
        ResetRuntimeState();
        File.WriteAllText(ResultPath, "OK\nSingle 8-second butterfly Play test started.\n");
        EditorApplication.isPlaying = true;
    }

    private static void MonitorRuntime()
    {
        if (!EditorApplication.isPlaying || !File.Exists(MonitorPath) || File.ReadAllText(MonitorPath).Trim() != "ARMED")
            return;

        ButterflyNaturalMotion[] butterflies = UnityEngine.Object.FindObjectsByType<ButterflyNaturalMotion>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (butterflies.Length == 0)
            return;

        if (!runtimeInitialized)
        {
            runtimeInitialized = true;
            runtimeStart = EditorApplication.timeSinceStartup;
            foreach (ButterflyNaturalMotion butterfly in butterflies)
            {
                startPositions[butterfly.name] = butterfly.transform.position;
                if (butterfly.wingFlap != null && butterfly.wingFlap.leftWing != null)
                    startWingRotations[butterfly.name] = butterfly.wingFlap.leftWing.localRotation;
            }
        }

        Camera camera = Camera.main;
        Plane[] planes = camera != null ? GeometryUtility.CalculateFrustumPlanes(camera) : null;
        var sampledAngles = new List<float>();

        foreach (ButterflyNaturalMotion butterfly in butterflies)
        {
            Vector3 position = butterfly.transform.position;
            if (position.x < -4.5f || position.x > 3.5f || position.y < -0.05f || position.y > 1.9f || position.z < 1.7f || position.z > 3.8f)
                remainedInHabitat = false;

            Renderer[] renderers = butterfly.GetComponentsInChildren<Renderer>(true);
            if (planes != null && renderers.Any(renderer => renderer.enabled && GeometryUtility.TestPlanesAABB(planes, renderer.bounds)))
                visibleButterflies.Add(butterfly.name);

            if (butterfly.wingFlap != null && butterfly.wingFlap.leftWing != null && startWingRotations.TryGetValue(butterfly.name, out Quaternion initialRotation))
            {
                float angle = Quaternion.Angle(initialRotation, butterfly.wingFlap.leftWing.localRotation);
                sampledAngles.Add(angle);
                if (angle > 8f)
                    movingWings.Add(butterfly.name);
            }
        }

        if (sampledAngles.Count == 3 && (sampledAngles.Max() - sampledAngles.Min()) > 4f)
            independentPhasesObserved = true;

        if (EditorApplication.timeSinceStartup - runtimeStart < 8d)
            return;

        bool slowMovement = butterflies.All(butterfly =>
            startPositions.TryGetValue(butterfly.name, out Vector3 start) &&
            Vector3.Distance(start, butterfly.transform.position) <= 6.2f);

        bool passed = butterflies.Length == 3 &&
                      visibleButterflies.Count == 3 &&
                      movingWings.Count == 3 &&
                      independentPhasesObserved &&
                      slowMovement &&
                      remainedInHabitat;

        File.WriteAllText(RuntimeReportPath,
            (passed ? "PASS" : "FAIL") + "\n" +
            $"Butterflies: {butterflies.Length}\n" +
            $"Visible: {visibleButterflies.Count}\n" +
            $"Wings moving: {movingWings.Count}\n" +
            $"Independent phases: {independentPhasesObserved}\n" +
            $"Slow movement: {slowMovement}\n" +
            $"Stayed in habitat: {remainedInHabitat}\n" +
            "Duration: 8 seconds\n");
        File.WriteAllText(MonitorPath, "DONE");
        EditorApplication.isPlaying = false;
    }

    private static void ResetRuntimeState()
    {
        runtimeInitialized = false;
        runtimeStart = 0d;
        startPositions.Clear();
        startWingRotations.Clear();
        visibleButterflies.Clear();
        movingWings.Clear();
        independentPhasesObserved = false;
        remainedInHabitat = true;
    }
}
#endif
