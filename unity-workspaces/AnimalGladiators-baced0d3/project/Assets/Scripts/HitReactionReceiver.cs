using UnityEngine;

[DisallowMultipleComponent]
public sealed class HitReactionReceiver : MonoBehaviour
{
    private const string HitReactionTrigger = "HitReaction";
    private Animator animator;
    private PlayerController playerController;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        playerController = GetComponent<PlayerController>();
        if (playerController == null)
            playerController = GetComponentInParent<PlayerController>();
    }

    public void ReceiveHit()
    {
        if (animator == null)
            return;

        if (playerController != null && playerController.IsBlocking)
            return;

        animator.ResetTrigger(HitReactionTrigger);
        animator.SetTrigger(HitReactionTrigger);
    }
}
