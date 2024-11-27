using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class OrbitCamera : MonoBehaviour
{
    [SerializeField]
    Transform focusObject = default;
    [SerializeField]
    LayerMask obstructionMask = -1;
    [SerializeField]
    Vector3 focusOffset = new Vector3(0, 1, 0);
    [SerializeField, Range(1f, 30f)]
    float distance = 10f;
    [SerializeField, Min(0f)]
    float focusRadius = 1f;
    [SerializeField, Range(0f, 1f)]
    float focusCentering = .9f;
    [SerializeField, Range(.1f, 5f)]
    float sensitivity = 1f;
    [SerializeField, Range(-89f, 89f)]
    float minVerticalAngle = -30f, maxVerticalAngle = 60f;

    float RotationSpeed => sensitivity * 360f;

    Vector3 focusPoint;
    Vector2 orbitAngles = new Vector2(45f, 0f);

    Camera regularCamera;

    Vector3 CameraHalfExtends
    {
        get
        {
            Vector3 halfExtends;

            halfExtends.y = regularCamera.nearClipPlane * Mathf.Tan(.5f * Mathf.Deg2Rad * regularCamera.fieldOfView);
            halfExtends.x = halfExtends.y * regularCamera.aspect;
            halfExtends.z = 0f;
            return halfExtends;
        }
    }

    private void Awake()
    {
        regularCamera = GetComponent<Camera>();

        focusPoint = focusObject.position + focusOffset;

        transform.localRotation = Quaternion.Euler(orbitAngles);
    }

    private void OnValidate()
    {
        if (maxVerticalAngle < minVerticalAngle)
        {
            maxVerticalAngle = minVerticalAngle;
        }
    }

    private void LateUpdate()
    {
        UpdateFocusPoint();

        Quaternion lookRotation;

        if (ManualRotation())
        {
            ConstrainAngles();
            lookRotation = Quaternion.Euler(orbitAngles);
        }

        else
        {
            lookRotation = transform.localRotation;
        }

        Vector3 lookDirection = lookRotation * Vector3.forward;
        Vector3 lookPosition = focusPoint - lookDirection * distance;

        Vector3 rectOffset = lookDirection * regularCamera.nearClipPlane;
        Vector3 rectPosition = lookPosition + rectOffset;
        Vector3 castFrom = focusObject.position + focusOffset;
        Vector3 castLine = rectPosition - castFrom;
        float castDistance = castLine.magnitude;
        Vector3 castDirection = castLine / castDistance;

        if (Physics.BoxCast(castFrom, CameraHalfExtends, castDirection, out RaycastHit hit, lookRotation, castDistance, obstructionMask))
        {
            rectPosition = castFrom + castDirection * hit.distance;
            lookPosition = rectPosition - rectOffset;
        }

        transform.SetPositionAndRotation(lookPosition, lookRotation);
    }

    void UpdateFocusPoint()
    {
        Vector3 targetPoint = focusObject.position + focusOffset;

        if (focusRadius > 0f)
        {
            float distance = Vector3.Distance(targetPoint, focusPoint);

            float t = 1f;

            if (distance > 0.01f && focusCentering > 0f)
            {
                t = Mathf.Pow(1f - focusCentering, Time.unscaledDeltaTime);
            }

            if (distance > focusRadius)
            {
                t = Mathf.Min(t, focusRadius / distance);
            }

            focusPoint = Vector3.Lerp(targetPoint, focusPoint, t);
        }

        else
        {
            focusPoint = targetPoint;
        }
    }

    bool ManualRotation()
    {
        Vector2 input = new Vector2(-Input.GetAxis("Mouse Y"), Input.GetAxis("Mouse X"));

        const float e = 0.001f;

        if (input.x < -e || input.x > e || input.y < -e || input.y > e)
        {
            orbitAngles += RotationSpeed * Time.unscaledDeltaTime * input;
            return true;
        }
        return false;
    }

    void ConstrainAngles()
    {
        orbitAngles.x = Mathf.Clamp(orbitAngles.x, minVerticalAngle, maxVerticalAngle);

        if (orbitAngles.y < 0f)
        {
            orbitAngles.y += 360f;
        }

        else if (orbitAngles.y >= 360f)
        {
            orbitAngles.y -= 360f;
        }
    }
}
