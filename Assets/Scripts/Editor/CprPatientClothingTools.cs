using System;
using System.Collections.Generic;
using System.Linq;
using FirstAidVR.Rcp;
using UnityEditor;
using UnityEngine;

public static class CprPatientClothingTools
{
    public const string MeshPath = "Assets/Resources/RCP/PatientClothedBody.asset";

    [MenuItem("Primeros auxilios/RCP/Regenerar piel cubierta por ropa")]
    public static void Bake()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hombre/Hombre_Paciente.Fbx");
        var skin = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(s => s.name == "CC_Base_Body");
        var source = skin.sharedMesh;
        var mesh = UnityEngine.Object.Instantiate(source);
        mesh.name = "Paciente vestido · piel visible";
        // The passive patient uses bone animation, with no facial blendshape weights. Avoid
        // duplicating hundreds of unused expression targets in the mobile clothing mesh.
        if (Enumerable.Range(0, source.blendShapeCount).Any(i => Mathf.Abs(skin.GetBlendShapeWeight(i)) > .001f))
            throw new InvalidOperationException("Hornea primero las expresiones activas del paciente.");
        mesh.ClearBlendShapes();
        var bones = skin.bones;
        int neckIndex = Array.FindIndex(bones, b => b.name == "CC_Base_NeckTwist01");
        int headIndex = Array.FindIndex(bones, b => b.name == "CC_Base_Head");
        if (neckIndex < 0 || headIndex < 0) throw new InvalidOperationException("No se encuentra la base del cuello del paciente.");
        // Bind-pose coordinates match the mesh even when the scene instance is lying down.
        var neck = source.bindposes[neckIndex].inverse.MultiplyPoint3x4(Vector3.zero);
        var head = source.bindposes[headIndex].inverse.MultiplyPoint3x4(Vector3.zero);
        var up = (head - neck).normalized;
        var vertices = source.vertices;
        for (int slot = 0; slot < source.subMeshCount; slot++)
        {
            string material = skin.sharedMaterials[slot].name;
            if (material == "Std_Skin_Leg") mesh.SetTriangles(Array.Empty<int>(), slot, false);
            if (material != "Std_Skin_Body") continue;
            // Keep a collar overlap below the neck so the neckline never opens into a hole.
            var triangles = source.GetTriangles(slot);
            var visible = new List<int>();
            for (int i = 0; i < triangles.Length; i += 3)
                if (Enumerable.Range(0, 3).Any(j => Vector3.Dot(vertices[triangles[i+j]] - neck, up) > -.02f))
                { visible.Add(triangles[i]); visible.Add(triangles[i+1]); visible.Add(triangles[i+2]); }
            mesh.SetTriangles(visible, slot, false);
        }
        mesh.bounds = source.bounds;
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if (existing == null) { AssetDatabase.CreateAsset(mesh, MeshPath); existing = mesh; }
        else { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); }
        var settings = Resources.Load<CprTrainingSettings>("RCP/TrainingSettings");
        settings.clothedPatientBody = existing;
        EditorUtility.SetDirty(existing); EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets();
        Debug.Log("RCP: piel cubierta horneada, sin materiales vacíos y con cuello visible. " + MeshPath);
    }
}
