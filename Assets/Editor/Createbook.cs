using UnityEngine;
using UnityEditor;

public class BookSpawnerEditor : EditorWindow
{
    GameObject bookPrefab;
    int count = 10;
    Vector3 startPosition = Vector3.zero;
    Vector3 spacing = new Vector3(0.2f, 0, 0);
    Vector3 scale = Vector3.one;

    [MenuItem("Tools/Book Spawner")]
    public static void ShowWindow()
    {
        GetWindow<BookSpawnerEditor>("Book Spawner");
    }

    void OnGUI()
    {
        GUILayout.Label("Book Spawner Settings", EditorStyles.boldLabel);

        bookPrefab = (GameObject)EditorGUILayout.ObjectField("Book Prefab", bookPrefab, typeof(GameObject), false);
        count = EditorGUILayout.IntField("Number of Books", count);
        startPosition = EditorGUILayout.Vector3Field("Start Position", startPosition);
        spacing = EditorGUILayout.Vector3Field("Spacing (X,Y,Z)", spacing);
        scale = EditorGUILayout.Vector3Field("Book Scale", scale);

        if (GUILayout.Button("Spawn Books"))
        {
            SpawnBooks();
        }
    }

    void SpawnBooks()
    {
        if (bookPrefab == null)
        {
            Debug.LogError("Please assign a book prefab!");
            return;
        }

        // Ensure we get the latest prefab asset version
        GameObject prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(bookPrefab);
        if (prefabAsset == null)
            prefabAsset = bookPrefab; // fallback if already a prefab asset

        for (int i = 0; i < count; i++)
        {
            Vector3 pos = startPosition + spacing * i;
            GameObject book = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
            book.transform.position = pos;
            book.transform.localScale = scale;
            Undo.RegisterCreatedObjectUndo(book, "Spawn Books");
        }

        Debug.Log($"Spawned {count} books starting at {startPosition}");
    }
}
