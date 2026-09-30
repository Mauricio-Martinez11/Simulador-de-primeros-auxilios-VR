using System;
using System.Collections;
using System.Linq;
using FirstAidVR.Rcp;
using Oculus.Interaction;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class CprPresentationChecks
{
    public static int Passed { get; private set; }

    public static IEnumerator Run()
    {
        Passed = 0;
        var settings = Resources.Load<CprTrainingSettings>("RCP/TrainingSettings");
        Check(settings.clothedPatientBody != null, "El APK incluye la malla de piel cubierta");
        using (var f = new CprTransportChecks.Fixture())
        {
            var skin = f.patient.GetComponentsInChildren<SkinnedMeshRenderer>().Single(s => s.name == "CC_Base_Body");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hombre/Hombre_Paciente.Fbx")
                .GetComponentsInChildren<SkinnedMeshRenderer>().Single(s => s.name == "CC_Base_Body").sharedMesh;
            Check(skin.sharedMesh == settings.clothedPatientBody, "El paciente usa la malla preparada");
            Check(f.patient.GetComponentsInChildren<SkinnedMeshRenderer>().All(s => s.sharedMaterials.All(m => m != null && m.shader != null)), "Ningún material vacío produce rosa");
            Check(skin.sharedMesh.GetIndexCount(1) > 0 && skin.sharedMesh.GetIndexCount(1) < source.GetIndexCount(1), "Se conserva el cuello y se omite el torso cubierto");
            Check(skin.sharedMesh.GetIndexCount(3) == 0, "La piel no atraviesa los jeans");
            foreach (int slot in new[] { 0, 2, 4, 5 })
                Check(skin.sharedMesh.GetTriangles(slot).SequenceEqual(source.GetTriangles(slot)), "Cabeza, brazos, manos y detalles intactos: " + slot);
            Check(skin.sharedMesh.bindposes.SequenceEqual(source.bindposes), "Los huesos de la piel siguen alineados con la ropa");
            UnityEngine.Object.DestroyImmediate(f.mover);
            UnityEngine.Object.DestroyImmediate(f.host.GetComponent<CprPatientRagdoll>());
            Check(skin.sharedMesh == source, "Al retirar el ragdoll se restaura la malla original");
        }

        var host = new GameObject("Prueba UI RCP");
        var cameraObject = new GameObject("Cámara UI prueba");
        var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
        camera.transform.position = new Vector3(0, 0, -2);
        var existingEvents = EventSystem.current;
        try
        {
            var view = host.AddComponent<CprTutorialView>(); view.Build(settings, camera);
            var flow = new CprFlow(settings);
            view.Show(flow, "Prueba"); view.SetButtonsEnabled(true);
            yield return null; yield return null;
            var canvas = host.GetComponentInChildren<Canvas>();
            var ray = host.GetComponentInChildren<RayInteractable>();
            var primary = canvas.transform.Find("Continuar").GetComponent<Button>();
            var secondary = canvas.transform.Find("Menú").GetComponent<Button>();
            Check(ray != null && ray.PointableElement is PointableCanvas, "El rayo usa el puente de eventos de Meta");
            Check(canvas.GetComponent<GraphicRaycaster>() != null && primary.image.raycastTarget && secondary.image.raycastTarget, "Los botones reciben eventos UI");
            Check(UnityEngine.Object.FindObjectsOfType<PointableCanvasModule>().Length == 1, "Un solo módulo de eventos");
            bool gazeDidNothing = true;
            for (int i = 0; i < 200; i++)
                gazeDidNothing &= view.UpdateButtons(new Ray(camera.transform.position, primary.transform.position-camera.transform.position), .02f, .8f) == -1;
            Check(gazeDidNothing, "Mirar el botón durante cuatro segundos no confirma");

            // Drive the ISDK event path (not Button.onClick.Invoke), with independent pointer ids
            // for the left/right rays. Unity's EventSystem performs raycasts and clicks itself.
            int pointer = 100;
            foreach (var button in new[] { primary, secondary, primary, secondary })
            {
                view.PanelPosition += new Vector3(.12f, .03f, .06f);
                view.Show(flow, "Prueba"); Canvas.ForceUpdateCanvases();
                var direction = button.transform.position - camera.transform.position;
                Check(ray.Raycast(new Ray(camera.transform.position, direction.normalized), out var hit, 5, false), "La superficie sigue al panel al moverlo");
                var pose = new Pose(hit.Point, Quaternion.identity);
                ray.PublishPointerEvent(new PointerEvent(pointer, PointerEventType.Hover, pose));
                yield return null; yield return null;
                ray.PublishPointerEvent(new PointerEvent(pointer, PointerEventType.Select, pose));
                yield return null; yield return null;
                Check(view.UpdateButtons(default, .02f, .8f) == -1, "Mantener pulsado no repite ni confirma antes de soltar");
                ray.PublishPointerEvent(new PointerEvent(pointer, PointerEventType.Unselect, pose));
                yield return null; yield return null;
                Check(view.UpdateButtons(default, .02f, .8f) == (button == primary ? 0 : 1), "Clic láser llega al botón correcto");
                Check(view.UpdateButtons(default, .02f, .8f) == -1, "Un clic produce una acción");
                ray.PublishPointerEvent(new PointerEvent(pointer++, PointerEventType.Unhover, pose));
                yield return null;
            }
            // The native module has to be reused if a second panel is made in the scene.
            var second = new GameObject("Otro panel");
            second.AddComponent<CprTutorialView>().Build(settings, camera);
            yield return null;
            Check(UnityEngine.Object.FindObjectsOfType<PointableCanvasModule>().Length == 1, "Los paneles comparten el módulo");
            UnityEngine.Object.DestroyImmediate(second);
            view.SetButtonsEnabled(false);
            ExecuteEvents.Execute(primary.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
            view.SetButtonsEnabled(true);
            Check(view.UpdateButtons(default, .02f, .8f) == -1, "No se arrastran clics de un paso sin foco o en espera");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(cameraObject);
            if (existingEvents == null && EventSystem.current != null) UnityEngine.Object.DestroyImmediate(EventSystem.current.gameObject);
        }
    }

    private static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException("Presentación Quest: " + message);
        Passed++;
    }
}
