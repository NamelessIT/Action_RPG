using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float smoothSpeed = 0.1f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    //void Start()
    //{

    //}

    // Update is called once per frame
    //void Update()
    //{
        
    //}
    void LateUpdate()
    {
        if (target != null)
        {
            Vector3 desiredPos = new Vector3(target.position.x, target.position.y, transform.position.z);
            transform.position = Vector3.Lerp(transform.position, desiredPos, smoothSpeed);
        }
    }
}
