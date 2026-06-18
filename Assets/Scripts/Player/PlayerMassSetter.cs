using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMassSetter : MonoBehaviour
{
    [SerializeField] private float playerMass = 80f;

    private void Awake()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.mass = playerMass;
    }
}