using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BallMovement : MonoBehaviour
{
    [Header("Ball")]
    [SerializeField]
    Transform playerInputSpace = default, ball = default;
    [SerializeField]
    [Tooltip("This is a readonly property used to update the balls rotation depending on the given radius")]
    float ballRadius = .5f;
    [SerializeField, Min(0f)]
    float ballAirRotation = .5f;
    [SerializeField]
    [Tooltip("Align the balls texture to the direction of movement")]
    bool alignBall = true;
    [SerializeField, Min(0f)]
    float ballAlignSpeed = 20f;

    [Header("Movement")]
    [SerializeField, Range(0f, 15f)]
    float maxSpeed = 5f;
    [SerializeField, Range(0f, 30f)]
    [Tooltip("How well the ball handles on the ground")]
    float accelerationGround = 10f;
    [SerializeField, Range(0f, 30f)]
    [Tooltip("How well the ball handles in the air")]
    float accelerationAir = 5f;
    [SerializeField, Range(0f, 90f)]
    float maxGroundAngle = 25f;
    [SerializeField, Range(0f, 5f)]
    [Tooltip("Percentage of maxSpeed that the ball will still stick to the ground when going over bumps/ slopes")]
    float maxSnapSpeed = .5f;
    [SerializeField, Min(0f)]
    float probeDistance = 1f;
    [SerializeField]
    LayerMask probeMask = -1;

    [Header("Jump")]
    [SerializeField, Range(0f, 10f)]
    float jumpHeight = 2f;
    [SerializeField, Range(0, 5)]
    int maxSteepJumps = 1;
    [SerializeField]
    [Tooltip("Steep geometry will now reset amount of jumps")]
    bool steepJumpReset;
    [SerializeField, Range(0, 5)]
    int maxAirJumps = 0;

    bool OnGround => groundContactCount > 0;
    bool OnSteep => steepContactCount > 0;
    float Acceleration => OnGround ? accelerationGround : accelerationAir;

    Vector3 playerInput;
    Vector3 velocity;
    Vector3 desiredVelocity;
    Vector3 connectionVelocity;
    Vector3 lastConnectionVelocity;
    Vector3 connectionWorldPosition;
    Vector3 connectionLocalPosition;
    Vector3 contactNormal;
    Vector3 lastContactNormal;
    Vector3 steepNormal;
    Vector3 lastSteepNormal;

    float minGroundDotProduct;

    int steepJumpPhase;
    int airJumpPhase;
    int groundContactCount;
    int steepContactCount;
    int stepsSinceLastGrounded;
    int stepsSinceLastJump;

    bool desiredJump;

    Rigidbody body, connectedBody, previousConnectedBody;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        OnValidate();
    }

    private void Update()
    {
        playerInput.x = Input.GetAxis("Horizontal");
        playerInput.y = 0f;
        playerInput.z = Input.GetAxis("Vertical");

        playerInput = Vector3.ClampMagnitude(playerInput, 1f);

        if (playerInputSpace)
        {
            Vector3 forward = playerInputSpace.forward;
            forward.y = 0f;
            forward.Normalize();
            Vector3 right = playerInputSpace.right;
            right.y = 0f;
            right.Normalize();

            desiredVelocity = (forward * playerInput.z + right * playerInput.x) * maxSpeed;
        }

        else
        {
            desiredVelocity = new Vector3(playerInput.x, 0, playerInput.z) * maxSpeed;
        }

        desiredJump |= Input.GetButtonDown("Jump");

        UpdateBall();
    }

    private void FixedUpdate()
    {
        UpdateState();
        AdjustVelocity();

        if (desiredJump)
        {
            desiredJump = false;
            Jump();
        }

        // Apply new velocity to rigidbody
        body.velocity = velocity;

        ClearState();
    }

    private void OnCollisionEnter(Collision collision)
    {
        EvaluateCollision(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        EvaluateCollision(collision);
    }

    private void OnValidate()
    {
        minGroundDotProduct = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);
    }

    void UpdateState()
    {
        stepsSinceLastGrounded += 1;
        stepsSinceLastJump += 1;
        velocity = body.velocity;

        if (OnGround || SnapToGround() || CheckSteepContacts())
        {
            stepsSinceLastGrounded = 0;

            if (stepsSinceLastJump > 1)
            {
                airJumpPhase = 0;
                steepJumpPhase = 0;
            }

            if (groundContactCount > 1)
            {
                contactNormal.Normalize();
            }
        }

        else
        {
            contactNormal = Vector3.up;
        }

        if (connectedBody)
        {
            if (connectedBody.isKinematic || connectedBody.mass >= body.mass)
            {
                UpdateConnectionState();
            }
        }
    }

    void ClearState()
    {
        groundContactCount = 0;
        steepContactCount = 0;

        lastContactNormal = contactNormal;
        contactNormal = Vector3.zero;

        lastSteepNormal = steepNormal;
        steepNormal = Vector3.zero;

        lastConnectionVelocity = connectionVelocity;
        connectionVelocity = Vector3.zero;

        previousConnectedBody = connectedBody;
        connectedBody = null;
    }

    void Jump()
    {
        Vector3 jumpDirection;

        if (OnGround || OnSteep && steepJumpReset)
        {
            jumpDirection = contactNormal;
            airJumpPhase = 0;
            steepJumpPhase = 0;
        }

        else if (maxSteepJumps > 0 && steepJumpPhase <= maxSteepJumps && OnSteep)
        {
            if (steepJumpPhase == 0)
            {
                steepJumpPhase = 1;
            }

            jumpDirection = steepNormal;
            steepJumpPhase += 1;
        }

        else if (maxAirJumps > 0 && airJumpPhase <= maxAirJumps)
        {
            if (airJumpPhase == 0)
            {
                airJumpPhase = 1;
            }

            jumpDirection = contactNormal;
            airJumpPhase += 1;
        }

        else
        {
            return;
        }

        stepsSinceLastJump = 0;

        float jumpSpeed = Mathf.Sqrt(-2f * Physics.gravity.y * jumpHeight);

        jumpDirection = (jumpDirection + Vector3.up).normalized;

        float alignedSpeed = Vector3.Dot(velocity, jumpDirection);

        if (alignedSpeed > 0f)
        {
            jumpSpeed = Mathf.Max(jumpSpeed - alignedSpeed, 0f);
        }

        velocity += jumpDirection * jumpSpeed;
    }

    void EvaluateCollision(Collision collision)
    {
        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector3 normal = collision.GetContact(i).normal;

            if (normal.y >= minGroundDotProduct)
            {
                groundContactCount += 1;
                contactNormal += normal;
                connectedBody = collision.rigidbody;
            }

            else if (normal.y > .01f)
            {
                steepContactCount += 1;
                steepNormal += normal;

                if (groundContactCount == 0)
                {
                    connectedBody = collision.rigidbody;
                }
            }
        }
    }

    Vector3 ProjectOnContactPlane(Vector3 vector)
    {
        return vector - contactNormal * Vector3.Dot(vector, contactNormal);
    }

    void AdjustVelocity()
    {
        Vector3 xAxis = ProjectOnContactPlane(Vector3.right).normalized;
        Vector3 zAxis = ProjectOnContactPlane(Vector3.forward).normalized;

        Vector3 relativeVelocity = velocity - connectionVelocity;

        float currentX = Vector3.Dot(relativeVelocity, xAxis);
        float currentZ = Vector3.Dot(relativeVelocity, zAxis);

        float maxSpeedChange = Acceleration * Time.deltaTime;

        Vector3 newVelocity = Vector3.MoveTowards(new Vector3(currentX, 0, currentZ), desiredVelocity, maxSpeedChange);

        velocity += xAxis * (newVelocity.x - currentX) + zAxis * (newVelocity.z - currentZ);
    }

    bool SnapToGround()
    {
        if (stepsSinceLastGrounded > 1 || stepsSinceLastJump <= 2)
        {
            return false;
        }

        float speed = velocity.magnitude;

        if (speed > (maxSnapSpeed * maxSpeed) - 0.01f)
        {
            return false;
        }

        if (!Physics.Raycast(body.position, Vector3.down, out RaycastHit hit, probeDistance, probeMask))
        {
            return false;
        }

        if (hit.normal.y < minGroundDotProduct)
        {
            return false;
        }

        groundContactCount = 1;
        contactNormal = hit.normal;
        float dot = Vector3.Dot(velocity, hit.normal);

        if (dot > 0f)
        {
            velocity = (velocity - hit.normal * dot).normalized * speed;
        }

        connectedBody = hit.rigidbody;
        return true;
    }

    bool CheckSteepContacts()
    {
        if (steepContactCount > 1)
        {
            steepNormal.Normalize();

            if (steepNormal.y >= minGroundDotProduct)
            {
                groundContactCount = 1;
                contactNormal = steepNormal;
                return true;
            }
        }

        return false;
    }

    void UpdateConnectionState()
    {
        if (connectedBody == previousConnectedBody)
        {
            Vector3 connectionMovement = connectedBody.transform.TransformPoint(connectionLocalPosition) - connectionWorldPosition;
            connectionVelocity = connectionMovement / Time.deltaTime;
        }

        connectionWorldPosition = body.position;
        connectionLocalPosition = connectedBody.transform.InverseTransformPoint(connectionWorldPosition);
    }

    void UpdateBall()
    {
        Vector3 movement = (body.velocity - lastConnectionVelocity) * Time.deltaTime;
        float distance = movement.magnitude;

        Quaternion rotation = ball.localRotation;

        if (connectedBody && connectedBody == previousConnectedBody)
        {
            rotation = Quaternion.Euler(connectedBody.angularVelocity * (Mathf.Rad2Deg * Time.deltaTime)) * rotation;

            if (distance < 0.001f)
            {
                ball.localRotation = rotation;
                return;
            }
        }

        else if (distance < 0.001f)
        {
            return;
        }

        Vector3 rotationPlaneNormal = lastContactNormal;

        float rotationFactor = 1f;

        if (!OnGround)
        {
            if (OnSteep)
            {
                lastContactNormal = lastSteepNormal;
            }

            else
            {
                rotationFactor = ballAirRotation;
            }
        }

        float angle = distance * rotationFactor * (180f / Mathf.PI) / ballRadius;

        movement -= rotationPlaneNormal * Vector3.Dot(movement, rotationPlaneNormal);

        Vector3 rotationAxis = Vector3.Cross(rotationPlaneNormal, movement).normalized;

        rotation = Quaternion.Euler(rotationAxis * angle) * ball.localRotation;

        if (ballAlignSpeed > 0f && alignBall)
        {
            rotation = AlignBallRotation(rotationAxis, rotation, distance);
        }

        ball.localRotation = rotation;
    }

    Quaternion AlignBallRotation(Vector3 rotationAxis, Quaternion rotation, float traveledDistance)
    {
        Vector3 ballAxis = ball.up;
        float dot = Mathf.Clamp(Vector3.Dot(ballAxis, rotationAxis), -1f, 1f);
        float angle = Mathf.Acos(dot) * Mathf.Rad2Deg;
        float maxAngle = ballAlignSpeed * traveledDistance;

        Quaternion newAlignment =
            Quaternion.FromToRotation(ballAxis, rotationAxis) * rotation;
        if (angle <= maxAngle)
        {
            return newAlignment;
        }
        else
        {
            return Quaternion.SlerpUnclamped(
                rotation, newAlignment, maxAngle / angle
            );
        }
    }

    public void PreventSnapToGround()
    {
        stepsSinceLastJump = -1;
    }
}
