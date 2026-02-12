using UnityEngine;

public class DoorTeleport : MonoBehaviour
{
    [Header("Teleport Settings")]
    public Transform teleportDestination;

    [Header("Mesh Management")]
    public GameObject[] indoorObjects;  // object indoor 
    public GameObject[] outdoorObjects; // object outdoor 

    [Header("Optional")]
    public KeyCode interactKey = KeyCode.E;
    public bool requireKeyPress = false;
    public bool toggleMeshes = false; 

    private GameObject playerInRange;

    void Start()
    {
        Collider doorCollider = GetComponent<Collider>();
        if (doorCollider == null)
        {
            doorCollider = gameObject.AddComponent<BoxCollider>();
        }
        doorCollider.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = other.gameObject;

            if (!requireKeyPress)
            {
                TeleportPlayer(other.gameObject);
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = null;
        }
    }

    void Update()
    {
        if (requireKeyPress && playerInRange != null && Input.GetKeyDown(interactKey))
        {
            TeleportPlayer(playerInRange);
        }
    }

    void TeleportPlayer(GameObject player)
    {
        if (teleportDestination != null)
        {
            player.transform.position = teleportDestination.position;
            player.transform.rotation = teleportDestination.rotation;

            if (toggleMeshes)
            {
                // Hiện indoor
                foreach (GameObject obj in indoorObjects)
                {
                    if (obj != null) obj.SetActive(true);
                }

                // Ẩn outdoor
                foreach (GameObject obj in outdoorObjects)
                {
                    if (obj != null) obj.SetActive(false);
                }
            }
        }
    }
}