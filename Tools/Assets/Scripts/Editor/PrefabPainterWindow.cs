using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class PrefabPainterWindow : EditorWindow
{
    [Header("Prefab Settings")]
    private List<GameObject> availablePrefabs = new List<GameObject>();
    private int selectedPrefabIndex = 0;

    [Header("Scene Settings")]
    private Transform parentObject;
    private bool toolEnabled = false;
    private bool alignToSurfaceNormal = true;

    private GameObject lastSpawnedObject;
    private Vector3 lastSpawnPosition;
    private bool isDraggingRotation = false;

    private Vector2 scrollPosition;

    [MenuItem("Tools/Prefab Painter Window")]
    public static void OpenWindow()
    {
        PrefabPainterWindow window = GetWindow<PrefabPainterWindow>();
        window.titleContent = new GUIContent("Prefab Painter");
        window.Show();
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void OnGUI()
    {
        DrawHeader();
        DrawToolToggle();
        DrawPrefabList();
        DrawSceneSettings();
        DrawHelpBox();
    }

    private void DrawHeader()
    {
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Prefab Painter Tool", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Esta herramienta permite instanciar prefabs en la escena haciendo click en la Scene View. " +
            "Después de instanciar, puedes arrastrar el ratón para orientar el último objeto. \n" + 
            "Deshacer: Ctrl + Z \n" +
            "Rehacer: Ctrl + Y ",
            MessageType.Info
        );

        EditorGUILayout.Space();
    }

    private void DrawToolToggle()
    {
        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = toolEnabled ? Color.green : Color.gray;

        if (GUILayout.Button(toolEnabled ? "Tool Enabled" : "Tool Disabled", GUILayout.Height(30)))
        {
            toolEnabled = !toolEnabled;
            SceneView.RepaintAll();
        }

        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
    }

    private void DrawPrefabList()
    {
        EditorGUILayout.LabelField("Available Prefabs", EditorStyles.boldLabel);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(160));

        for (int i = 0; i < availablePrefabs.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();

            availablePrefabs[i] = (GameObject)EditorGUILayout.ObjectField(
                availablePrefabs[i],
                typeof(GameObject),
                false
            );

            if (GUILayout.Toggle(selectedPrefabIndex == i, "Active", "Button", GUILayout.Width(70)))
            {
                selectedPrefabIndex = i;
            }

            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                availablePrefabs.RemoveAt(i);

                if (selectedPrefabIndex >= availablePrefabs.Count)
                {
                    selectedPrefabIndex = Mathf.Max(0, availablePrefabs.Count - 1);
                }

                GUIUtility.ExitGUI();
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();

        if (GUILayout.Button("Add Prefab Slot"))
        {
            availablePrefabs.Add(null);
        }

        EditorGUILayout.Space();
    }

    private void DrawSceneSettings()
    {
        EditorGUILayout.LabelField("Scene Settings", EditorStyles.boldLabel);

        parentObject = (Transform)EditorGUILayout.ObjectField(
            "Parent Object",
            parentObject,
            typeof(Transform),
            true
        );

        alignToSurfaceNormal = EditorGUILayout.Toggle(
            "Align To Surface Normal",
            alignToSurfaceNormal
        );

        EditorGUILayout.Space();

        GUI.enabled = lastSpawnedObject != null;

        if (GUILayout.Button("Select Last Spawned Object"))
        {
            Selection.activeGameObject = lastSpawnedObject;
        }

        GUI.enabled = true;

        EditorGUILayout.Space();
    }

    private void DrawHelpBox()
    {
        EditorGUILayout.HelpBox(
            "Uso:\n" +
            "1. Añade uno o más prefabs.\n" +
            "2. Marca uno como Active.\n" +
            "3. Asigna un Parent Object si quieres organizar las instancias.\n" +
            "4. Activa la herramienta.\n" +
            "5. Haz click en la Scene View para instanciar.\n" +
            "6. Arrastra el ratón sin soltar para rotar el objeto.\n" +
            "7. Usa Ctrl+Z para deshacer.",
            MessageType.None
        );
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!toolEnabled)
        {
            return;
        }

        Event currentEvent = Event.current;

        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        DrawScenePreview();

        switch (currentEvent.type)
        {
            case EventType.MouseDown:
                if (currentEvent.button == 0 && !currentEvent.alt)
                {
                    TrySpawnPrefab(currentEvent.mousePosition);
                    currentEvent.Use();
                }
                break;

            case EventType.MouseDrag:
                if (currentEvent.button == 0 && lastSpawnedObject != null)
                {
                    RotateLastSpawnedObject(currentEvent.mousePosition);
                    currentEvent.Use();
                }
                break;

            case EventType.MouseUp:
                if (currentEvent.button == 0)
                {
                    isDraggingRotation = false;
                    lastSpawnedObject = null;
                    currentEvent.Use();
                }
                break;
        }

        sceneView.Repaint();
    }

    private void DrawScenePreview()
    {
        Handles.BeginGUI();

        GUILayout.BeginArea(new Rect(10, 10, 280, 80), "Prefab Painter Active", "Window");

        string prefabName = GetActivePrefab() != null ? GetActivePrefab().name : "None";

        GUILayout.Label("Active Prefab: " + prefabName);
        GUILayout.Label("Left Click: Spawn");
        GUILayout.Label("Drag: Rotate last object");

        GUILayout.EndArea();

        Handles.EndGUI();
    }

    private void TrySpawnPrefab(Vector2 mousePosition)
    {
        GameObject activePrefab = GetActivePrefab();

        if (activePrefab == null)
        {
            EditorUtility.DisplayDialog(
                "No Active Prefab",
                "You need to assign and select an active prefab before spawning.",
                "OK"
            );

            return;
        }

        Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hitInfo, 10000f))
        {
            SpawnPrefabAtPoint(activePrefab, hitInfo.point, hitInfo.normal);
        }
        else
        {
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

            if (groundPlane.Raycast(ray, out float distance))
            {
                Vector3 spawnPosition = ray.GetPoint(distance);
                SpawnPrefabAtPoint(activePrefab, spawnPosition, Vector3.up);
            }
        }
    }

    private void SpawnPrefabAtPoint(GameObject prefab, Vector3 position, Vector3 normal)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

        if (instance == null)
        {
            instance = Instantiate(prefab);
        }

        Undo.RegisterCreatedObjectUndo(instance, "Spawn Prefab");

        instance.transform.position = position;

        if (alignToSurfaceNormal)
        {
            instance.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal);
        }
        else
        {
            instance.transform.rotation = Quaternion.identity;
        }

        if (parentObject != null)
        {
            Undo.SetTransformParent(instance.transform, parentObject, "Set Prefab Parent");
        }

        EditorUtility.SetDirty(instance);

        lastSpawnedObject = instance;
        lastSpawnPosition = position;
        isDraggingRotation = true;

        Selection.activeGameObject = instance;
    }

    private void RotateLastSpawnedObject(Vector2 mousePosition)
    {
        if (!isDraggingRotation || lastSpawnedObject == null)
        {
            return;
        }

        Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);

        Plane rotationPlane = new Plane(Vector3.up, lastSpawnPosition);

        if (rotationPlane.Raycast(ray, out float distance))
        {
            Vector3 hitPoint = ray.GetPoint(distance);
            Vector3 direction = hitPoint - lastSpawnPosition;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.001f)
            {
                Undo.RecordObject(lastSpawnedObject.transform, "Rotate Spawned Prefab");

                Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                lastSpawnedObject.transform.rotation = targetRotation;

                EditorUtility.SetDirty(lastSpawnedObject);
            }
        }
    }

    private GameObject GetActivePrefab()
    {
        if (availablePrefabs.Count == 0)
        {
            return null;
        }

        if (selectedPrefabIndex < 0 || selectedPrefabIndex >= availablePrefabs.Count)
        {
            return null;
        }

        return availablePrefabs[selectedPrefabIndex];
    }
}