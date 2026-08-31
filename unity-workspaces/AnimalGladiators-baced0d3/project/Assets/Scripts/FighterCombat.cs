using UnityEngine;

[RequireComponent(typeof(PlayerController), typeof(Animator), typeof(CharacterController))]
[RequireComponent(typeof(AudioSource))]
public class FighterCombat : MonoBehaviour
{
    [Header("Automatic Attack Impact")]
    [Min(0f)]
    public float extraHitReach = 0.75f;

    [Header("Impact Timing")]
    [Tooltip("Core attacks connect close to the visible end of the strike.")]
    [Range(0.45f, 0.70f)]
    public float standardImpactNormalizedTime = 0.62f;

    [Tooltip("Final hit of Square x3 / Triangle x3, just before the combo transition.")]
    [Range(0.50f, 0.70f)]
    public float comboFinalImpactNormalizedTime = 0.66f;

    [Header("Hit Sound")]
    public AudioClip hitSound;

    [Range(0f, 1f)]
    public float hitSoundVolume = 0.8f;

    [Header("Dust VFX")]
    [Tooltip("Creates lightweight runtime dust effects for movement, normal hits and blocked hits.")]
    public bool enableDustVfx = true;

    [Min(0.02f)]
    public float movementDustInterval = 0.12f;

    [Min(0.01f)]
    public float movementDustMinSpeed = 0.20f;

    [Range(1, 20)]
    public int movementDustParticles = 4;

    [Range(1, 40)]
    public int hitDustParticles = 14;

    [Range(1, 40)]
    public int blockDustParticles = 18;

    private PlayerController playerController;
    private Animator animator;
    private CharacterController characterController;
    private AudioSource audioSource;

    private bool hasHitCurrentAttack;
    private bool wasInAttackState;
    private int currentAttackStateHash;
    private float nextMovementDustTime;

    private ParticleSystem movementDust;
    private ParticleSystem hitDust;
    private ParticleSystem blockDust;

    private static AudioClip generatedHitSound;
    private static Material sharedDustMaterial;
    private static Texture2D sharedDustTexture;

    private static readonly int LightAttackStateHash =
        Animator.StringToHash("LightAttack");
    private static readonly int SquareFollowUpStateHash =
        Animator.StringToHash("SquareFollowUp");
    private static readonly int SquareSecondFollowUpStateHash =
        Animator.StringToHash("SquareSecondFollowUp");
    private static readonly int LightKickStateHash =
        Animator.StringToHash("Armature|Armature|Boxing_Guard_Right_Straight_Kick|baselayer");
    private static readonly int HeavyAttackStateHash =
        Animator.StringToHash("Armature|Armature|Punch_Combo|baselayer");
    private static readonly int TriangleFollowUpStateHash =
        Animator.StringToHash("TriangleFollowUp");
    private static readonly int TriangleSecondFollowUpStateHash =
        Animator.StringToHash("TriangleSecondFollowUp");

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        animator = GetComponent<Animator>();
        characterController = GetComponent<CharacterController>();
        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        if (enableDustVfx)
        {
            EnsureDustSystems();
        }
    }

    private void Update()
    {
        if (animator == null || playerController == null || characterController == null)
        {
            return;
        }

        if (enableDustVfx)
        {
            EnsureDustSystems();
            UpdateMovementDust();
        }

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        bool isAttackState = state.IsTag("Attack");

        if (!isAttackState)
        {
            wasInAttackState = false;
            return;
        }

        if (!wasInAttackState || currentAttackStateHash != state.fullPathHash)
        {
            wasInAttackState = true;
            currentAttackStateHash = state.fullPathHash;
            hasHitCurrentAttack = false;
        }

        if (!hasHitCurrentAttack && IsImpactMoment(state))
        {
            TryHitOpponent(state);
        }
    }

    private bool IsImpactMoment(AnimatorStateInfo state)
    {
        if (!ShouldTriggerHitReaction(state))
        {
            return true;
        }

        float normalized =
            state.normalizedTime - Mathf.Floor(state.normalizedTime);

        float threshold =
            state.shortNameHash == SquareFollowUpStateHash ||
            state.shortNameHash == TriangleFollowUpStateHash
                ? comboFinalImpactNormalizedTime
                : standardImpactNormalizedTime;

        return normalized >= threshold;
    }

    private bool ShouldTriggerHitReaction(AnimatorStateInfo state)
    {
        int hash = state.shortNameHash;

        return
            hash == LightAttackStateHash ||
            hash == LightKickStateHash ||
            hash == SquareFollowUpStateHash ||
            hash == TriangleFollowUpStateHash;
    }

    private void UpdateMovementDust()
    {
        if (movementDust == null || !characterController.isGrounded)
        {
            return;
        }

        float horizontalSpeed = Mathf.Abs(characterController.velocity.x);
        if (horizontalSpeed < movementDustMinSpeed || Time.time < nextMovementDustTime)
        {
            return;
        }

        nextMovementDustTime = Time.time + movementDustInterval;

        Bounds bounds = characterController.bounds;
        Vector3 feet = new Vector3(
            bounds.center.x,
            bounds.min.y + 0.025f,
            bounds.center.z
        );

        movementDust.transform.position = feet;

        ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams();
        emit.position = feet;
        emit.applyShapeToPosition = true;

        movementDust.Emit(emit, Mathf.Max(1, movementDustParticles));
    }

    private void TryHitOpponent(AnimatorStateInfo attackState)
    {
        Transform opponent = playerController.opponent;

        if (opponent == null || opponent == transform)
        {
            return;
        }

        CharacterController opponentController =
            opponent.GetComponent<CharacterController>();

        if (opponentController == null || !opponentController.enabled)
        {
            return;
        }

        Bounds attackerBounds = characterController.bounds;
        Bounds opponentBounds = opponentController.bounds;
        float opponentDeltaX = opponentBounds.center.x - attackerBounds.center.x;

        if (Mathf.Approximately(opponentDeltaX, 0f))
        {
            return;
        }

        float forwardDirection = Mathf.Sign(opponentDeltaX);
        float horizontalGap = forwardDirection > 0f
            ? opponentBounds.min.x - attackerBounds.max.x
            : attackerBounds.min.x - opponentBounds.max.x;

        bool verticalOverlap =
            attackerBounds.max.y >= opponentBounds.min.y &&
            opponentBounds.max.y >= attackerBounds.min.y;

        bool depthOverlap =
            attackerBounds.max.z >= opponentBounds.min.z &&
            opponentBounds.max.z >= attackerBounds.min.z;

        bool opponentIsInFront = opponentDeltaX * forwardDirection > 0f;

        if (!opponentIsInFront || !verticalOverlap || !depthOverlap ||
            horizontalGap > extraHitReach)
        {
            return;
        }

        hasHitCurrentAttack = true;
        PlayHitSound();

        if (enableDustVfx)
        {
            EnsureDustSystems();

            PlayerController opponentPlayer =
                opponent.GetComponent<PlayerController>();

            bool blocked =
                opponentPlayer != null &&
                opponentPlayer.IsBlocking;

            Vector3 impactPoint = GetImpactPoint(
                attackerBounds,
                opponentBounds,
                forwardDirection
            );

            PlayImpactDust(impactPoint, blocked);
        }

        if (ShouldTriggerHitReaction(attackState))
        {
            PlayerController reactionOpponentPlayer =
                opponent.GetComponent<PlayerController>();

            bool reactionBlocked =
                reactionOpponentPlayer != null &&
                reactionOpponentPlayer.IsBlocking;

            if (!reactionBlocked)
            {
                HitReactionReceiver receiver =
                    opponent.GetComponent<HitReactionReceiver>();

                if (receiver == null)
                {
                    receiver =
                        opponent.gameObject.AddComponent<HitReactionReceiver>();
                }

                receiver.ReceiveHit();
            }
        }

        Debug.Log(
            $"HIT: {gameObject.name} -> {opponent.gameObject.name}",
            this
        );
    }

    private Vector3 GetImpactPoint(
        Bounds attackerBounds,
        Bounds opponentBounds,
        float forwardDirection)
    {
        float x = forwardDirection > 0f
            ? (attackerBounds.max.x + opponentBounds.min.x) * 0.5f
            : (attackerBounds.min.x + opponentBounds.max.x) * 0.5f;

        float overlapBottom = Mathf.Max(attackerBounds.min.y, opponentBounds.min.y);
        float overlapTop = Mathf.Min(attackerBounds.max.y, opponentBounds.max.y);

        float y;
        if (overlapTop > overlapBottom)
        {
            y = Mathf.Lerp(overlapBottom, overlapTop, 0.58f);
        }
        else
        {
            y = opponentBounds.center.y;
        }

        float z = (attackerBounds.center.z + opponentBounds.center.z) * 0.5f;

        return new Vector3(x, y, z);
    }

    private void PlayImpactDust(Vector3 position, bool blocked)
    {
        ParticleSystem system = blocked ? blockDust : hitDust;
        if (system == null)
        {
            return;
        }

        system.transform.position = position;

        ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams();
        emit.position = position;
        emit.applyShapeToPosition = true;

        int count = blocked
            ? Mathf.Max(1, blockDustParticles)
            : Mathf.Max(1, hitDustParticles);

        system.Emit(emit, count);
    }

    private void EnsureDustSystems()
    {
        if (movementDust != null && hitDust != null && blockDust != null)
        {
            return;
        }

        Material dustMaterial = GetSharedDustMaterial();

        if (movementDust == null)
        {
            movementDust = CreateDustSystem(
                "Dust_Movement",
                new Color(0.64f, 0.48f, 0.31f, 0.34f),
                new Color(0.82f, 0.69f, 0.50f, 0.10f),
                0.34f,
                0.58f,
                0.10f,
                0.24f,
                0.42f,
                0.68f,
                dustMaterial
            );
        }

        if (hitDust == null)
        {
            hitDust = CreateDustSystem(
                "Dust_Hit_Impact",
                new Color(0.76f, 0.61f, 0.42f, 0.72f),
                new Color(0.93f, 0.84f, 0.67f, 0.14f),
                1.55f,
                2.45f,
                0.08f,
                0.24f,
                0.22f,
                0.42f,
                dustMaterial
            );
        }

        if (blockDust == null)
        {
            blockDust = CreateDustSystem(
                "Dust_Block_Impact",
                new Color(0.55f, 0.57f, 0.58f, 0.82f),
                new Color(0.82f, 0.84f, 0.85f, 0.12f),
                0.75f,
                1.35f,
                0.07f,
                0.18f,
                0.14f,
                0.28f,
                dustMaterial
            );
        }
    }

    private ParticleSystem CreateDustSystem(
        string systemName,
        Color startColorA,
        Color startColorB,
        float speedMin,
        float speedMax,
        float sizeMin,
        float sizeMax,
        float lifeMin,
        float lifeMax,
        Material material)
    {
        GameObject vfxObject = new GameObject(systemName);
        vfxObject.transform.SetParent(transform, false);

        ParticleSystem system = vfxObject.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = system.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 96;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(startColorA, startColorB);
        main.gravityModifier = 0.18f;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = systemName == "Dust_Movement" ? 0.16f : 0.11f;
        shape.radiusThickness = 1f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
            system.colorOverLifetime;
        colorOverLifetime.enabled = true;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.85f, 0f),
                new GradientAlphaKey(0.36f, 0.45f),
                new GradientAlphaKey(0f, 1f)
            }
        );

        colorOverLifetime.color = gradient;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime =
            system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
            1f,
            new AnimationCurve(
                new Keyframe(0f, 0.65f),
                new Keyframe(0.35f, 1f),
                new Keyframe(1f, 1.35f)
            )
        );

        ParticleSystemRenderer renderer =
            vfxObject.GetComponent<ParticleSystemRenderer>();

        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        if (material != null)
        {
            renderer.sharedMaterial = material;
        }

        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return system;
    }

    private static Material GetSharedDustMaterial()
    {
        if (sharedDustMaterial != null)
        {
            return sharedDustMaterial;
        }

        Shader shader =
            Shader.Find("Sprites/Default") ??
            Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
            Shader.Find("Particles/Standard Unlit");

        if (shader == null)
        {
            return null;
        }

        sharedDustMaterial = new Material(shader)
        {
            name = "Runtime_Dust_Soft_Material",
            hideFlags = HideFlags.HideAndDontSave
        };

        sharedDustTexture = BuildSoftDustTexture();

        if (sharedDustMaterial.HasProperty("_MainTex"))
        {
            sharedDustMaterial.SetTexture("_MainTex", sharedDustTexture);
        }

        if (sharedDustMaterial.HasProperty("_BaseMap"))
        {
            sharedDustMaterial.SetTexture("_BaseMap", sharedDustTexture);
        }

        if (sharedDustMaterial.HasProperty("_Color"))
        {
            sharedDustMaterial.SetColor("_Color", Color.white);
        }

        if (sharedDustMaterial.HasProperty("_BaseColor"))
        {
            sharedDustMaterial.SetColor("_BaseColor", Color.white);
        }

        return sharedDustMaterial;
    }

    private static Texture2D BuildSoftDustTexture()
    {
        const int size = 32;

        Texture2D texture = new Texture2D(
            size,
            size,
            TextureFormat.RGBA32,
            false,
            true
        );

        texture.name = "Runtime_Dust_Soft_Texture";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.hideFlags = HideFlags.HideAndDontSave;

        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = ((x + 0.5f) / size) * 2f - 1f;
                float ny = ((y + 0.5f) / size) * 2f - 1f;
                float distance = Mathf.Sqrt(nx * nx + ny * ny);

                float alpha = Mathf.Clamp01(1f - distance);
                alpha = alpha * alpha * (3f - 2f * alpha);

                pixels[y * size + x] =
                    new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private void PlayHitSound()
    {
        if (audioSource == null)
        {
            return;
        }

        AudioClip clip = hitSound != null
            ? hitSound
            : GetGeneratedHitSound();

        audioSource.PlayOneShot(clip, hitSoundVolume);
    }

    private static AudioClip GetGeneratedHitSound()
    {
        if (generatedHitSound != null)
        {
            return generatedHitSound;
        }

        const int sampleRate = 44100;
        const float duration = 0.09f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)sampleRate;
            float normalizedTime = time / duration;
            float envelope = Mathf.Pow(1f - normalizedTime, 3f);
            float lowImpact = Mathf.Sin(2f * Mathf.PI * 115f * time);
            float click = Mathf.Sin(2f * Mathf.PI * 860f * time) *
                          Mathf.Clamp01(1f - normalizedTime * 8f);

            samples[i] = (lowImpact * 0.75f + click * 0.25f) * envelope;
        }

        generatedHitSound = AudioClip.Create(
            "GeneratedFighterHit",
            sampleCount,
            1,
            sampleRate,
            false
        );

        generatedHitSound.SetData(samples, 0);
        return generatedHitSound;
    }
}
