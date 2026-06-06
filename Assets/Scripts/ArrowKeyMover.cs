using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class ArrowKeyMover : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float rotationSpeed = 720f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private string idleStateName = "Idle_Normal_SwordAndShield";
    [SerializeField] private string moveStateName = "MoveFWD_Normal_InPlace_SwordAndShield";
    [SerializeField] private string attackStateName = "Attack01_SwordAndShiled";
    [SerializeField] private string defendStateName = "Defend_SwordAndShield";
    [SerializeField] private float attackDuration = 0.8f;
    [SerializeField] private float defendDuration = 0.7f;

    private CharacterController controller;
    private Animator cachedAnimator;
    private float verticalVelocity;
    private bool isMoving;
    private float actionTimer;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        cachedAnimator = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        PlayState(idleStateName);
    }

    private void Update()
    {
        if (actionTimer > 0f)
        {
            actionTimer -= Time.deltaTime;
            if (actionTimer <= 0f)
            {
                actionTimer = 0f;
                PlayState(isMoving ? moveStateName : idleStateName);
            }
        }

        if (Input.GetKeyDown(KeyCode.Q))
        {
            TriggerAction(attackStateName, attackDuration);
            return;
        }

        if (Input.GetKeyDown(KeyCode.W))
        {
            TriggerAction(defendStateName, defendDuration);
            return;
        }

        if (actionTimer > 0f)
        {
            return;
        }

        Vector3 input = Vector3.zero;

        if (Input.GetKey(KeyCode.UpArrow))
        {
            input.z += 1f;
        }

        if (Input.GetKey(KeyCode.DownArrow))
        {
            input.z -= 1f;
        }

        if (Input.GetKey(KeyCode.LeftArrow))
        {
            input.x -= 1f;
        }

        if (Input.GetKey(KeyCode.RightArrow))
        {
            input.x += 1f;
        }

        input = Vector3.ClampMagnitude(input, 1f);

        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 motion = new Vector3(input.x, 0f, input.z) * moveSpeed;
        motion.y = verticalVelocity;
        controller.Move(motion * Time.deltaTime);

        Vector3 flatMotion = new Vector3(input.x, 0f, input.z);
        bool hasMovementInput = flatMotion.sqrMagnitude > 0.0001f;

        if (hasMovementInput != isMoving)
        {
            isMoving = hasMovementInput;
            PlayState(isMoving ? moveStateName : idleStateName);
        }

        if (hasMovementInput)
        {
            Quaternion targetRotation = Quaternion.LookRotation(flatMotion, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime);
        }
    }

    private void PlayState(string stateName)
    {
        if (cachedAnimator == null || string.IsNullOrEmpty(stateName))
        {
            return;
        }

        cachedAnimator.Play(stateName, 0, 0f);
    }

    private void TriggerAction(string stateName, float duration)
    {
        actionTimer = Mathf.Max(0.1f, duration);
        isMoving = false;
        PlayState(stateName);
    }

    public void WarpTo(Vector3 position)
    {
        verticalVelocity = 0f;
        isMoving = false;
        actionTimer = 0f;

        if (controller != null)
        {
            controller.enabled = false;
        }

        transform.position = position;
        transform.rotation = Quaternion.identity;

        if (controller != null)
        {
            controller.enabled = true;
        }

        PlayState(idleStateName);
    }
}
