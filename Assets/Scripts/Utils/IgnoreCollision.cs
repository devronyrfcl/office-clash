using Unity.VisualScripting;
using UnityEngine;

public class IgnoreCollision : MonoBehaviour
{
    
    [SerializeField] private GameObject[] objectsToIgnore;

    
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //ignore collision between this object and the specified objects
        foreach (GameObject obj in objectsToIgnore)        {
            Physics.IgnoreCollision(GetComponent<Collider>(), obj.GetComponent<Collider>());
        }
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
