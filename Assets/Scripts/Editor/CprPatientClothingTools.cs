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
        // Keep the patient's eyelids closed in the optimized mesh used at runtime. The scene
        // already sets Eye_Blink_L/R to 100, but Prepare swaps in this mesh without blendshapes.
        if (Enumerable.Range(0, source.blendShapeCount).Any(i => Mathf.Abs(skin.GetBlendShapeWeight(i)) > .001f))
            throw new InvalidOperationException("Hornea primero las expresiones activas del paciente.");
        BakeClosedEyelids(source, mesh);
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
            // Keep only skin at the neck. A broad height cutoff also retains triangles
            // at the shoulders, where they can poke through the shirt when arms move.
            var triangles = source.GetTriangles(slot);
            var visible = new List<int>();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                bool atNeck = true;
                for (int j = 0; j < 3; j++)
                {
                    var offset = vertices[triangles[i+j]] - neck;
                    var height = Vector3.Dot(offset, up);
                    var radius = (offset - height * up).magnitude;
                    if (height < -.025f || (height < .035f && radius > .115f))
                    { atNeck = false; break; }
                }
                if (atNeck)
                { visible.Add(triangles[i]); visible.Add(triangles[i+1]); visible.Add(triangles[i+2]); }
            }
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

    private static void BakeClosedEyelids(Mesh source, Mesh destination)
    {
        var vertices = source.vertices;
        var normals = source.normals;
        var tangents = source.tangents;
        foreach (var name in new[] { "Eye_Blink_L", "Eye_Blink_R" })
        {
            int index = source.GetBlendShapeIndex(name);
            if (index < 0 || source.GetBlendShapeFrameCount(index) != 1)
                throw new InvalidOperationException("No se encuentra la forma de párpado esperada: " + name);
            float frameWeight = source.GetBlendShapeFrameWeight(index, 0);
            if (frameWeight <= 0) throw new InvalidOperationException("Peso de párpado inválido: " + name);
            float factor = 100f / frameWeight;
            var deltaVertices = new Vector3[vertices.Length];
            var deltaNormals = new Vector3[vertices.Length];
            var deltaTangents = new Vector3[vertices.Length];
            source.GetBlendShapeFrameVertices(index, 0, deltaVertices, deltaNormals, deltaTangents);
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] += deltaVertices[i] * factor;
                if (normals.Length == vertices.Length) normals[i] += deltaNormals[i] * factor;
                if (tangents.Length == vertices.Length)
                {
                    var tangent = tangents[i];
                    var xyz = new Vector3(tangent.x, tangent.y, tangent.z) + deltaTangents[i] * factor;
                    tangents[i] = new Vector4(xyz.x, xyz.y, xyz.z, tangent.w);
                }
            }
        }
        destination.vertices = vertices;
        if (normals.Length == vertices.Length) destination.normals = normals;
        if (tangents.Length == vertices.Length) destination.tangents = tangents;
    }
}
