using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CanvasBillboard : MonoBehaviour
{
    private Transform cameraTransform;

    void Start()
    {
        Camera mainCamera = Camera.main;

        if (mainCamera != null)
        {
            cameraTransform = mainCamera.transform;
        }
    }

    void LateUpdate()
    {
        if (cameraTransform == null) return;

        Vector3 direction = transform.position - cameraTransform.position;
        direction.y = 0;

        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }
}