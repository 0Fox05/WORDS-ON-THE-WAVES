using UnityEngine;
using UnityEditor;

public class MultiSectionBackdrop : EditorWindow
{
    private GameObject targetPlane;
    private float targetHeight = 30f;
    private int sections = 5; // number of curve segments
    private float curvePower = 2f;

    [MenuItem("Tools/Multi‑Section Backdrop")]
    public static void ShowWindow() => GetWindow<MultiSectionBackdrop>("Multi‑Section Backdrop");

    void OnGUI()
    {
        targetPlane = (GameObject)EditorGUILayout.ObjectField("Target Plane", targetPlane, typeof(GameObject), true);
        targetHeight = EditorGUILayout.FloatField("Target Height (Y)", targetHeight);
        sections = EditorGUILayout.IntSlider("Curve Sections", sections, 2, 10);
        curvePower = EditorGUILayout.Slider("Curve Power", curvePower, 0.5f, 5f);

        if (GUILayout.Button("Curve It") && targetPlane != null)
            CurvePlane();
    }

    void CurvePlane()
    {
        MeshFilter mf = targetPlane.GetComponent<MeshFilter>();
        if (mf == null) { Debug.LogError("Target must have a MeshFilter!"); return; }

        // Create a unique copy of the mesh so only this object is affected
        Mesh mesh = Instantiate(mf.sharedMesh);
        mf.mesh = mesh; // assign the copy back to the MeshFilter

        Vector3[] verts = mesh.vertices;

        float zMin = float.MaxValue, zMax = float.MinValue;
        foreach (var v in verts) { zMin = Mathf.Min(zMin, v.z); zMax = Mathf.Max(zMax, v.z); }

        for (int i = 0; i < verts.Length; i++)
        {
            float t = Mathf.InverseLerp(zMin, zMax, verts[i].z);
            float eased = Mathf.Pow(Mathf.SmoothStep(0, 1, t), curvePower);
            float sectionFactor = Mathf.Floor(t * sections) / sections;
            verts[i].y = Mathf.Lerp(0, targetHeight, eased * sectionFactor);
        }

        mesh.vertices = verts;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        Debug.Log("Backdrop curved smoothly with " + sections + " sections.");
    }
}
