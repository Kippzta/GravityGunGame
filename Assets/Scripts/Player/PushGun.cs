using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
public class PushGun : MonoBehaviour
{
    [Header("References")]
    public Camera playerCamera;
    public Rigidbody playerRb;

    [Header("PushGun Settings")]
    public float maxRocketJumpForce = 5f;
    public float minRocketJumpForce = 2f;
    public float chargeSpeed = 1f;


    [Header("Explosion Settings")]
    public float explosionForce = 300.0f;
    public bool useExplosionForce = true;
    public float objectForceMultiplier = 4f;


    [Header("Cooldown Settings")]
    [Tooltip("How many seconds between shooting")]
    public float fireCooldown = 0.5f;
    private float cooldownTimer = 0f;

    [Header("Separate Radius Settings")]
    public float objectExplosionRadius = 1f;

    [Tooltip("Radien på den osynliga sfären som kollar efter SPELAREN (för Rocket Jumps).")]
    public float playerExplosionRadius = 5f;


    [Header("Debug")]
    public float currentCharge = 0f;
    private bool isCharging = false;
    public bool showExplosionGizmo = true;
    private Vector3 lastExplosionPoint;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        // Räkna ner timern varje frame om den är över 0
        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;
        }

        // 1. Kolla om vänster musknapp hålls ner JUST NU
        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            // Vi får BARA börja ladda om timern har gått i mål (är 0 eller mindre)
            if (!isCharging && cooldownTimer <= 0f)
            {
                isCharging = true;
                currentCharge = 0f;
                Debug.Log("Loading weapon...");
            }

            // Öka laddningen så länge knappen är intryckt och vi faktiskt laddar
            if (isCharging)
            {
                currentCharge += chargeSpeed * Time.deltaTime;
                currentCharge = Mathf.Clamp(currentCharge, 0f, 1f);
            }
        }
        else
        {
            // 2. Om knappen INTE är intryckt, men vi höll på och ladda -> Skjut!
            if (isCharging)
            {
                FirePushShot();
            }
        }
    }



    private void FirePushShot()
    {

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, 200f))
        {
            Vector3 explosionPoint = hit.point;
            lastExplosionPoint = explosionPoint; // För vår röda/blå debug-sfär

            if (useExplosionForce)
            {
                int playerLayerIndex = LayerMask.NameToLayer("Player");

                // =================================================================
                // 1. SPELAR-CHECK (Använder den STORA playerExplosionRadius)
                // =================================================================
                Collider[] playerColliders = Physics.OverlapSphere(explosionPoint, playerExplosionRadius);
                foreach (var hitCollider in playerColliders)
                {
                    Rigidbody rb = hitCollider.GetComponent<Rigidbody>();
                    if (rb != null && (hitCollider.gameObject.layer == playerLayerIndex || rb == playerRb))
                    {
                        // ROCKET JUMP
                        Vector3 pushDirection = (playerRb.position - explosionPoint).normalized;

                        // Mjuk övergång baserat på laddning (0 till 1)
                        float finalPlayerForce = Mathf.Lerp(minRocketJumpForce, maxRocketJumpForce, currentCharge);

                        playerRb.AddForce(pushDirection * finalPlayerForce, ForceMode.VelocityChange);
                        Debug.Log($"Spelare Rocket Jump! Laddning: {currentCharge * 100}%, Kraft: {finalPlayerForce}");
                        break;
                    }
                }

                // =================================================================
                // 2. OBJEKT-CHECK (Använder den röda objectExplosionRadius)
                // =================================================================
                Collider[] objectColliders = Physics.OverlapSphere(explosionPoint, objectExplosionRadius);
                foreach (var hitCollider in objectColliders)
                {
                    Rigidbody rb = hitCollider.GetComponent<Rigidbody>();
                    if (rb != null && hitCollider.gameObject.layer != playerLayerIndex && rb != playerRb)
                    {
                        Vector3 radialNormal = (hitCollider.transform.position - explosionPoint).normalized;
                        Vector3 objectPushDir = radialNormal;
                        objectPushDir.y += 0.3f;
                        objectPushDir = objectPushDir.normalized;

                        // KUB-KRAFT: Använder Lerp här med så ett snabbt klick inte blir 0 i kraft!
                        // Vid snabbt klick (charge=0) får de 1x kraft. Vid full laddning (charge=1) får de max multiplier (t.ex. 4x).
                        float currentMultiplier = Mathf.Lerp(1f, objectForceMultiplier, currentCharge);
                        float finalObjectForce = explosionForce * currentMultiplier;

                        // Dynamisk storleks-offset för flippen
                        float halfCubeHeight = hitCollider.bounds.extents.y;
                        Vector3 dynamicOffset = new Vector3(0f, -halfCubeHeight * 0.8f, 0f);

                        Vector3 offsetPoint = rb.worldCenterOfMass + dynamicOffset;
                        Vector3 offsetDir = (offsetPoint - explosionPoint).normalized;

                        rb.AddForceAtPosition(offsetDir * finalObjectForce, offsetPoint, ForceMode.Impulse);

                        Vector3 rotationAxis = Vector3.Cross(Vector3.up, radialNormal);
                        rb.AddTorque(rotationAxis * finalObjectForce * 0.5f, ForceMode.Impulse);

                        Debug.Log($"Objekt ({hitCollider.name}) träffat! Multiplier: {currentMultiplier}");
                    }
                }
            }

            cooldownTimer = fireCooldown;

            // =================================================================
            // Vi nollställer laddningen EFTER att skotten har avfyrats
            // =================================================================
            isCharging = false;
            currentCharge = 0f;
        }
    }

    private void OnDrawGizmos()
    {
        if (showExplosionGizmo && lastExplosionPoint != Vector3.zero)
        {
            // 1. Blå sfär för SPELAREN (Rocket Jump radie)
            Gizmos.color = new Color(0f, 0.5f, 1f, 0.15f); // Genomskinlig blå
            Gizmos.DrawSphere(lastExplosionPoint, playerExplosionRadius);
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(lastExplosionPoint, playerExplosionRadius);

            // 2. Röd sfär för OBJEKTEN (Kuber etc.)
            Gizmos.color = new Color(1f, 0f, 0f, 0.2f); // Genomskinlig röd
            Gizmos.DrawSphere(lastExplosionPoint, objectExplosionRadius);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(lastExplosionPoint, objectExplosionRadius);
        }
    }
}
