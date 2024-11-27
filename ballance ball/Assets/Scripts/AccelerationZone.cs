using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AccelerationZone : MonoBehaviour
{
    public float speed;
    public float acceleration;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerStay(Collider other)
    {
        Rigidbody rb = other.attachedRigidbody;

        if (rb)
        {
            Accelerate(rb);
        }
    }

    void Accelerate(Rigidbody rb)
    {
        if (rb.TryGetComponent(out BallMovement ball))
        {
            ball.PreventSnapToGround();
        }

        Vector3 velocity = transform.InverseTransformDirection(rb.velocity);

        if (velocity.y >= speed)
        {
            return;
        }

        if (acceleration > 0)
        {
            velocity.y = Mathf.MoveTowards(velocity.y, speed, acceleration * Time.fixedDeltaTime);
        }

        else
        {
            velocity.y = speed;
        }

        rb.velocity = transform.TransformDirection(velocity);
    }
}
