using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class PressurePlateMass : MonoBehaviour
{
    [Header("Doors")]
    [Tooltip("Doors opened by this pressure plate.")]
    [SerializeField] private SimpleMassDoor[] doors;

    [Tooltip("If true, doors close again when the mass on the plate drops below the required amount.")]
    [SerializeField] private bool closeWhenReleased = true;

    [Tooltip("If true, doors stay open forever after this plate opens them once.")]
    [SerializeField] private bool stayOpenOnceOpened = false;

    [Header("Mass Requirement")]
    [Tooltip("Total Rigidbody mass required to press this plate.")]
    [SerializeField] private float requiredMass = 20f;

    [Tooltip("Only objects on these layers count toward the pressure plate mass.")]
    [SerializeField] private LayerMask activatorLayers = ~0;

    [Tooltip("If true, the player uses Player Mass Override instead of Rigidbody.mass.")]
    [SerializeField] private bool usePlayerMassOverride = true;

    [Tooltip("Mass used for the player if Use Player Mass Override is enabled.")]
    [SerializeField] private float playerMassOverride = 80f;

    [Tooltip("If true, kinematic Rigidbodies can count toward the plate mass.")]
    [SerializeField] private bool includeKinematicBodies = true;

    [Tooltip("If true, sleeping Rigidbodies still count while sitting on the plate.")]
    [SerializeField] private bool includeSleepingBodies = true;

    [Header("Detection")]
    [Tooltip("How high above the pressure plate to scan for objects.")]
    [SerializeField] private float sensorHeight = 0.5f;

    [Tooltip("Extra width added to the mass detection area.")]
    [SerializeField] private float sensorExtraWidth = 0.08f;

    [Tooltip("Extra depth added to the mass detection area.")]
    [SerializeField] private float sensorExtraDepth = 0.08f;

    [Tooltip("Usually keep this on Ignore so trigger volumes do not count as physical mass.")]
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("Press / Release")]
    [Tooltip("Mass ratio required before the plate presses. 1 means Required Mass exactly.")]
    [Range(0f, 1f)]
    [SerializeField] private float pressThreshold = 1f;

    [Tooltip("Mass ratio required before the plate releases. Keep lower than Press Threshold to prevent jitter.")]
    [Range(0f, 1f)]
    [SerializeField] private float releaseThreshold = 0.85f;

    [Tooltip("Delay before releasing after mass is removed.")]
    [SerializeField] private float releaseDelay = 0.12f;

    [Tooltip("Delay before sending open/close commands to doors. Helps prevent door jitter.")]
    [SerializeField] private float doorCommandDebounce = 0.08f;

    [Header("Plate Movement")]
    [Tooltip("How far the whole pressure plate moves down when fully pressed.")]
    [SerializeField] private float pressedDownDistance = 0.12f;

    [Tooltip("How quickly the pressure plate moves down.")]
    [SerializeField] private float pressMoveSpeed = 8f;

    [Tooltip("How quickly the pressure plate moves back up.")]
    [SerializeField] private float releaseMoveSpeed = 8f;

    [Tooltip("If true, a kinematic Rigidbody is automatically added/used so the plate moves more safely with physics.")]
    [SerializeField] private bool useKinematicRigidbodyForPlate = true;

    [Header("Door Delay")]
    [Tooltip("Optional delay before doors open after the plate is pressed.")]
    [SerializeField] private float openDelay = 0f;

    [Tooltip("Optional delay before doors close after the plate is released.")]
    [SerializeField] private float closeDelay = 0f;

    [Header("Audio Optional")]
    [Tooltip("Optional sound played when the plate becomes pressed.")]
    [SerializeField] private AudioSource pressedSound;

    [Tooltip("Optional sound played when the plate releases.")]
    [SerializeField] private AudioSource releasedSound;

    [Header("Debug")]
    [Tooltip("Shows debug information in the Game view.")]
    [SerializeField] private bool showDebugPanel = false;

    [Tooltip("Draws the mass detection area in the Scene view.")]
    [SerializeField] private bool drawGizmos = true;

    private readonly Dictionary<Rigidbody, float> bodiesOnPlate = new Dictionary<Rigidbody, float>();

    private BoxCollider plateCollider;
    private Rigidbody plateRigidbody;

    private Vector3 closedWorldPosition;
    private Vector3 pressedWorldPosition;

    private bool isPressed;
    private bool desiredDoorOpen;
    private bool stableDoorOpen;
    private bool lastCommandedDoorOpen;
    private bool hasCommandedDoorState;
    private bool hasOpenedOnce;

    private float targetPressedAmount;
    private float currentPressedAmount;

    private float releaseTimer;
    private float openTimer;
    private float closeTimer;
    private float commandDebounceTimer;

    public float RequiredMass => requiredMass;
    public float CurrentMass { get; private set; }
    public float PressedAmount => currentPressedAmount;
    public float TargetPressedAmount => targetPressedAmount;
    public bool IsPressed => isPressed;
    public bool DesiredDoorOpen => desiredDoorOpen;
    public int ObjectCount => bodiesOnPlate.Count;

    private void Reset()
    {
        plateCollider = GetComponent<BoxCollider>();
        plateCollider.isTrigger = false;

        requiredMass = 20f;
        sensorHeight = 0.5f;
        sensorExtraWidth = 0.08f;
        sensorExtraDepth = 0.08f;
        pressThreshold = 1f;
        releaseThreshold = 0.85f;
        releaseDelay = 0.12f;
        doorCommandDebounce = 0.08f;
        pressedDownDistance = 0.12f;
        pressMoveSpeed = 8f;
        releaseMoveSpeed = 8f;

        SetupPlateRigidbody();
    }

    private void Awake()
    {
        plateCollider = GetComponent<BoxCollider>();
        plateCollider.isTrigger = false;

        SetupPlateRigidbody();

        closedWorldPosition = transform.position;
        pressedWorldPosition = closedWorldPosition - transform.up * pressedDownDistance;

        desiredDoorOpen = false;
        stableDoorOpen = false;
        lastCommandedDoorOpen = false;
    }

    private void FixedUpdate()
    {
        ScanForMass();
        UpdatePressedState();
        UpdateDoorState();
        MovePressurePlate();
    }

    private void SetupPlateRigidbody()
    {
        if (!useKinematicRigidbodyForPlate)
        {
            return;
        }

        plateRigidbody = GetComponent<Rigidbody>();

        if (plateRigidbody == null)
        {
            plateRigidbody = gameObject.AddComponent<Rigidbody>();
        }

        plateRigidbody.isKinematic = true;
        plateRigidbody.useGravity = false;
        plateRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        plateRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    private void ScanForMass()
    {
        bodiesOnPlate.Clear();

        GetSensorBox(out Vector3 center, out Vector3 halfExtents, out Quaternion rotation);

        Collider[] hits = Physics.OverlapBox(
            center,
            halfExtents,
            rotation,
            activatorLayers,
            triggerInteraction
        );

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];

            if (hit == null)
            {
                continue;
            }

            if (hit == plateCollider || hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            Rigidbody body = hit.attachedRigidbody;

            if (body == null)
            {
                continue;
            }

            if (!includeKinematicBodies && body.isKinematic)
            {
                continue;
            }

            if (!includeSleepingBodies && body.IsSleeping())
            {
                continue;
            }

            if (!bodiesOnPlate.ContainsKey(body))
            {
                bodiesOnPlate.Add(body, GetMassForBody(body));
            }
        }

        CurrentMass = 0f;

        foreach (float mass in bodiesOnPlate.Values)
        {
            CurrentMass += mass;
        }

        if (requiredMass <= 0.001f)
        {
            targetPressedAmount = CurrentMass > 0f ? 1f : 0f;
        }
        else
        {
            targetPressedAmount = Mathf.Clamp01(CurrentMass / requiredMass);
        }
    }

    private float GetMassForBody(Rigidbody body)
    {
        if (usePlayerMassOverride && body.GetComponentInParent<CoopFirstPersonController>() != null)
        {
            return playerMassOverride;
        }

        return Mathf.Max(0f, body.mass);
    }

    private void UpdatePressedState()
    {
        if (!isPressed)
        {
            if (targetPressedAmount >= pressThreshold)
            {
                isPressed = true;
                releaseTimer = releaseDelay;
                PlayPressedSound();
            }

            return;
        }

        if (targetPressedAmount >= releaseThreshold)
        {
            releaseTimer = releaseDelay;
            return;
        }

        if (releaseTimer > 0f)
        {
            releaseTimer -= Time.fixedDeltaTime;
            return;
        }

        isPressed = false;
        PlayReleasedSound();
    }

    private void UpdateDoorState()
    {
        UpdateDesiredDoorState();
        UpdateStableDoorState();
        ApplyDoorCommandIfNeeded();
    }

    private void UpdateDesiredDoorState()
    {
        if (stayOpenOnceOpened && hasOpenedOnce)
        {
            desiredDoorOpen = true;
            return;
        }

        if (isPressed)
        {
            closeTimer = closeDelay;

            if (openDelay > 0f)
            {
                openTimer += Time.fixedDeltaTime;
                desiredDoorOpen = openTimer >= openDelay;
            }
            else
            {
                desiredDoorOpen = true;
            }

            return;
        }

        openTimer = 0f;

        if (!closeWhenReleased)
        {
            return;
        }

        if (closeDelay > 0f)
        {
            closeTimer -= Time.fixedDeltaTime;
            desiredDoorOpen = closeTimer > 0f;
        }
        else
        {
            desiredDoorOpen = false;
        }
    }

    private void UpdateStableDoorState()
    {
        if (desiredDoorOpen == stableDoorOpen)
        {
            commandDebounceTimer = 0f;
            return;
        }

        commandDebounceTimer += Time.fixedDeltaTime;

        if (commandDebounceTimer >= doorCommandDebounce)
        {
            stableDoorOpen = desiredDoorOpen;
            commandDebounceTimer = 0f;
        }
    }

    private void ApplyDoorCommandIfNeeded()
    {
        if (hasCommandedDoorState && stableDoorOpen == lastCommandedDoorOpen)
        {
            return;
        }

        hasCommandedDoorState = true;
        lastCommandedDoorOpen = stableDoorOpen;

        if (stableDoorOpen)
        {
            hasOpenedOnce = true;
        }

        if (doors == null)
        {
            return;
        }

        foreach (SimpleMassDoor door in doors)
        {
            if (door == null)
            {
                continue;
            }

            door.SetOpen(stableDoorOpen);
        }
    }

    private void MovePressurePlate()
    {
        float speed = targetPressedAmount > currentPressedAmount
            ? pressMoveSpeed
            : releaseMoveSpeed;

        currentPressedAmount = Mathf.Lerp(
            currentPressedAmount,
            targetPressedAmount,
            1f - Mathf.Exp(-speed * Time.fixedDeltaTime)
        );

        Vector3 targetPosition = Vector3.Lerp(
            closedWorldPosition,
            pressedWorldPosition,
            currentPressedAmount
        );

        if (plateRigidbody != null && plateRigidbody.isKinematic)
        {
            plateRigidbody.MovePosition(targetPosition);
        }
        else
        {
            transform.position = targetPosition;
        }
    }

    private void GetSensorBox(out Vector3 center, out Vector3 halfExtents, out Quaternion rotation)
    {
        Vector3 localCenter = plateCollider.center;
        Vector3 localSize = plateCollider.size;

        localCenter.y += localSize.y * 0.5f + sensorHeight * 0.5f;

        localSize.x += sensorExtraWidth;
        localSize.z += sensorExtraDepth;
        localSize.y = sensorHeight;

        center = transform.TransformPoint(localCenter);
        halfExtents = Vector3.Scale(localSize * 0.5f, AbsVector(transform.lossyScale));
        rotation = transform.rotation;
    }

    private Vector3 AbsVector(Vector3 value)
    {
        return new Vector3(
            Mathf.Abs(value.x),
            Mathf.Abs(value.y),
            Mathf.Abs(value.z)
        );
    }

    private void PlayPressedSound()
    {
        if (pressedSound != null)
        {
            pressedSound.Play();
        }
    }

    private void PlayReleasedSound()
    {
        if (releasedSound != null)
        {
            releasedSound.Play();
        }
    }

    private void OnGUI()
    {
        if (!showDebugPanel)
        {
            return;
        }

        string text =
            $"Pressure Plate Door\n" +
            $"Name: {name}\n" +
            $"Mass: {CurrentMass:F1} / {requiredMass:F1}\n" +
            $"Pressed: {currentPressedAmount:P0}\n" +
            $"Target Pressed: {targetPressedAmount:P0}\n" +
            $"Is Pressed: {isPressed}\n" +
            $"Door Open: {stableDoorOpen}\n" +
            $"Bodies: {bodiesOnPlate.Count}\n" +
            $"Doors: {(doors != null ? doors.Length : 0)}";

        GUI.Box(new Rect(20f, 380f, 300f, 205f), text);
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos)
        {
            return;
        }

        BoxCollider box = GetComponent<BoxCollider>();

        if (box == null)
        {
            return;
        }

        plateCollider = box;

        GetSensorBox(out Vector3 center, out Vector3 halfExtents, out Quaternion rotation);

        Gizmos.color = Application.isPlaying && isPressed
            ? Color.green
            : Color.yellow;

        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2f);
        Gizmos.matrix = oldMatrix;
    }
}