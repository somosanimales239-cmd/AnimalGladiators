using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Fighter Setup")]
    [Tooltip("ON para el personaje controlado por el jugador. OFF para un rival/dummy que use el mismo script sin leer el mismo control.")]
    public bool acceptPlayerInput = true;

    [Tooltip("Rival al que este luchador debe mirar. Puede dejarse vacio hasta que exista el segundo personaje.")]
    public Transform opponent;

    [Tooltip("Hace que el luchador mire automaticamente hacia su rival y actualiza Forward/Backward si cambian de lado.")]
    public bool autoFaceOpponent = true;

    [Tooltip("Evita cambios de orientacion cuando ambos personajes estan practicamente en la misma posicion X.")]
    public float opponentFacingDeadZone = 0.05f;

    [Header("Bull Horn Rush")]
    [Tooltip("Activalo solamente en el toro. Permite probar BullHornRush con la tecla P aunque Accept Player Input este apagado.")]
    public bool enableBullHornRush = false;

    [Header("2D / Tekken Style Movement")]
    public float walkSpeed = 2.5f;
    public float runSpeed = 5.5f;
    public float gravity = -20f;

    [Tooltip("Limites horizontales temporales de la arena 2D.")]
    public float minX = -5.5f;
    public float maxX = 5.5f;

    [Tooltip("Mantiene al personaje en una sola linea de profundidad.")]
    public float laneLockSpeed = 15f;

    [Tooltip("Direccion inicial del luchador. Mas adelante mirara automaticamente al rival.")]
    public bool startFacingRight = true;

    [Header("Directional Jump Input")]
    [Tooltip("Cuanto movimiento horizontal necesita el stick para reconocer arriba+direccion.")]
    public float jumpDiagonalThreshold = 0.35f;

    [Tooltip("Espacio adicional obligatorio entre ambos CharacterControllers al aterrizar.")]
    public float landingSeparationPadding = 0.15f;

    [Tooltip("Velocidad horizontal usada para resolver suavemente el aterrizaje durante la caida.")]
    public float landingResolveSpeed = 12f;

    [Header("D-Pad Forward Run")]
    [Tooltip("Tiempo maximo entre dos toques hacia adelante en el D-Pad.")]
    public float dpadDoubleTapWindow = 0.30f;

    [Header("Backflip / R1")]
    public float backflipDistance = 2.8f;
    public float backflipDuration = 0.75f;

    [Header("Super Attack Charge / L2")]
    [Tooltip("Tiempo maximo que L2 puede mantener cargado el Super Attack.")]
    public float superChargeMaxTime = 3f;

    [Header("Combo 1 / Square + Triangle")]
    [Tooltip("Ventana para reconocer Square + Triangle como Combo1. Los ataques individuales esperan este tiempo antes de salir.")]
    public float combo1InputWindow = 0.12f;

    [Header("Combo 2 / X + Circle")]
    [Tooltip("Ventana para reconocer X + Circle como Combo2. Las patadas individuales esperan este tiempo antes de salir.")]
    public float combo2InputWindow = 0.12f;

    [Header("Square Triple Tap Combo")]
    [Tooltip("Cantidad de toques EXTRA de Square durante LightAttack para encadenar el segundo golpe. 2 = tres toques totales.")]
    public int squareFollowUpExtraTaps = 2;

    [Header("Square Five Tap Combo")]
    [Tooltip("Cantidad de toques EXTRA de Square para encadenar el tercer golpe. 4 = cinco toques totales.")]
    public int squareSecondFollowUpExtraTaps = 4;

    [Header("Triangle Triple Tap Combo")]
    [Tooltip("Cantidad de toques EXTRA de Triangle durante HeavyAttack para encadenar el segundo golpe. 2 = tres toques totales.")]
    public int triangleFollowUpExtraTaps = 2;

    [Header("Triangle Five Tap Combo")]
    [Tooltip("Cantidad de toques EXTRA de Triangle para encadenar el tercer golpe. 4 = cinco toques totales.")]
    public int triangleSecondFollowUpExtraTaps = 4;

    [Header("X Triple Tap Combo")]
    [Tooltip("Cantidad de toques EXTRA de X durante LightKick para encadenar el segundo movimiento. 2 = tres toques totales.")]
    public int xFollowUpExtraTaps = 2;

    private CharacterController controller;
    private Animator animator;

    private float verticalVelocity;
    private float laneZ;

    private bool isBlocking;
    private bool isCrouching;
    private bool isJumping;
    private bool isBackflipping;
    private bool isAttacking;
    private bool isSpecialAttacking;
    private bool isCombo2RootMotion;
    private bool isForwardCircleKickRootMotion;
    private bool isRunCircleSweepRootMotion;
    private bool isBullHornRushRootMotion;
    private bool isRunCircleSweepEntering;

    private bool isSuperCharging;
    private bool isSuperAttacking;
    private float superChargeTimer;

    private float lastDpadForwardTapTime = -10f;
    private bool dpadForwardRunActive;

    // Evita repetir el salto mientras se mantienen las dos direcciones.
    private bool previousKeyboardForwardJumpCombo;
    private bool previousKeyboardBackwardJumpCombo;
    private bool previousGamepadForwardJumpCombo;
    private bool previousGamepadBackwardJumpCombo;

    // Buffer para diferenciar:
    // Square solo       = LightAttack
    // Triangle solo     = HeavyAttack
    // Square + Triangle = Combo1
    private bool squareAttackPending;
    private bool triangleAttackPending;
    private float squareAttackPendingTimer;
    private float triangleAttackPendingTimer;

    // Square una vez = LightAttack normal.
    // Mientras LightAttack esta ejecutandose, dos toques EXTRA
    // de Square preparan SquareFollowUp.
    private bool isLightAttackChainActive;
    private int squareFollowUpTapCount;
    private bool squareFollowUpQueued;

    // Cinco toques totales de Square:
    // 1 = LightAttack
    // 2-3 = preparan SquareFollowUp
    // 4-5 = preparan SquareSecondFollowUp
    private bool squareSecondFollowUpQueued;

    // Triangle una vez = HeavyAttack normal.
    // Mientras HeavyAttack esta ejecutandose, dos toques EXTRA
    // de Triangle preparan TriangleFollowUp.
    private bool isHeavyAttackChainActive;
    private int triangleFollowUpTapCount;
    private bool triangleFollowUpQueued;
    private bool triangleSecondFollowUpQueued;

    // Buffer para diferenciar:
    // X solo      = LightKick
    // Circle solo = HeavyKick
    // X + Circle  = Combo2
    //
    // RunCircleSweep NO se decide aqui.
    // Se dispara aparte SOLO con Circle.wasPressedThisFrame
    // mientras running == true.
    private bool xKickPending;
    private bool circleKickPending;
    private float xKickPendingTimer;
    private float circleKickPendingTimer;

    // Cuenta los toques de X incluso durante la pequeña ventana
    // usada para distinguir X solo de X + Circle.
    private int xBufferedTapCount;

    // X una vez = LightKick normal.
    // Tres X totales = LightKick -> XFollowUp.
    private bool isLightKickChainActive;
    private int xFollowUpTapCount;
    private bool xFollowUpQueued;

    // =========================================================
    // ANIMATOR PARAMETERS
    //
    // Speed           = Float
    // WalkingBackward = Bool
    // Crouching       = Bool
    // JumpForward     = Trigger
    // JumpBackward    = Trigger
    // LightAttack     = Trigger
    // SquareFollowUpQueued = Bool
    // SquareSecondFollowUpQueued = Bool
    // HeavyAttack     = Trigger
    // TriangleFollowUpQueued = Bool
    // TriangleSecondFollowUpQueued = Bool
    // LightKick       = Trigger
    // XFollowUpQueued = Bool
    // HeavyKick       = Trigger
    // BackCircleKick  = Trigger
    // DownCircleKick  = Trigger
    // UpCircleKick    = Trigger
    // ForwardCircleKick = Trigger
    // RunCircleSweep  = Trigger
    // BullHornRush    = Trigger
    // SpecialAttack   = Trigger
    // Combo1          = Trigger
    // Combo2          = Trigger
    // SuperAttack     = Trigger
    // SuperCharging   = Bool
    // Blocking        = Bool
    // Backflip        = Trigger
    //
    // SALTOS:
    // NO existe salto vertical solo.
    // Arriba + Adelante dispara UNA sola animacion: JumpForward.
    // Arriba + Atras dispara UNA sola animacion: JumpBackward.
    // El codigo NO agrega altura ni desplazamiento al salto.
    // La animacion hace todo el movimiento visual.
    //
    // IMPORTANTE:
    // Los estados ofensivos completos deben tener:
    // Tag = Attack
    // =========================================================

    private static readonly int SpeedHash =
        Animator.StringToHash("Speed");

    private static readonly int WalkingBackwardHash =
        Animator.StringToHash("WalkingBackward");

    private static readonly int CrouchingHash =
        Animator.StringToHash("Crouching");

    private static readonly int JumpForwardHash =
        Animator.StringToHash("JumpForward");

    private static readonly int JumpBackwardHash =
        Animator.StringToHash("JumpBackward");

    private static readonly int LightAttackHash =
        Animator.StringToHash("LightAttack");

    private static readonly int SquareFollowUpQueuedHash =
        Animator.StringToHash("SquareFollowUpQueued");

    private static readonly int SquareSecondFollowUpQueuedHash =
        Animator.StringToHash("SquareSecondFollowUpQueued");

    private static readonly int HeavyAttackHash =
        Animator.StringToHash("HeavyAttack");

    private static readonly int TriangleFollowUpQueuedHash =
        Animator.StringToHash("TriangleFollowUpQueued");

    private static readonly int TriangleSecondFollowUpQueuedHash =
        Animator.StringToHash("TriangleSecondFollowUpQueued");

    private static readonly int LightKickHash =
        Animator.StringToHash("LightKick");

    private static readonly int XFollowUpQueuedHash =
        Animator.StringToHash("XFollowUpQueued");

    private static readonly int HeavyKickHash =
        Animator.StringToHash("HeavyKick");

    private static readonly int BackCircleKickHash =
        Animator.StringToHash("BackCircleKick");

    private static readonly int DownCircleKickHash =
        Animator.StringToHash("DownCircleKick");

    private static readonly int UpCircleKickHash =
        Animator.StringToHash("UpCircleKick");

    private static readonly int ForwardCircleKickHash =
        Animator.StringToHash("ForwardCircleKick");

    private static readonly int RunCircleSweepHash =
        Animator.StringToHash("RunCircleSweep");

    private static readonly int BullHornRushHash =
        Animator.StringToHash("BullHornRush");

    private static readonly int SpecialAttackHash =
        Animator.StringToHash("SpecialAttack");

    private static readonly int Combo1Hash =
        Animator.StringToHash("Combo1");

    private static readonly int Combo2Hash =
        Animator.StringToHash("Combo2");

    private static readonly int SuperAttackHash =
        Animator.StringToHash("SuperAttack");

    private static readonly int SuperChargingHash =
        Animator.StringToHash("SuperCharging");

    private static readonly int BlockingHash =
        Animator.StringToHash("Blocking");

    private static readonly int BackflipHash =
        Animator.StringToHash("Backflip");

    // =========================================================
    // PUBLIC COMBAT STATE
    // =========================================================

    public bool IsBlocking => isBlocking;
    public bool IsCrouching => isCrouching;
    public bool IsJumping => isJumping;
    public bool IsBackflipping => isBackflipping;
    public bool IsAttacking => isAttacking;
    public bool IsSpecialAttacking => isSpecialAttacking;
    public bool IsSuperCharging => isSuperCharging;
    public bool IsSuperAttacking => isSuperAttacking;

    public float CurrentSuperChargeRatio =>
        isSuperCharging
            ? Mathf.Clamp01(superChargeTimer / Mathf.Max(superChargeMaxTime, 0.01f))
            : 0f;

    public float LastSuperChargeRatio { get; private set; }


    private enum JumpType
    {
        Forward,
        Backward
    }

    private JumpType currentJumpType =
        JumpType.Forward;

    private bool jumpHadOpponentAtStart;
    private int jumpStartSide;
    private bool crossedOpponentDuringJump;


    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();

        laneZ = transform.position.z;

        if (animator == null)
        {
            Debug.LogError("NO SE ENCONTRO EL ANIMATOR EN EL PERSONAJE.");
        }
        else
        {
            // Necesario para que Animator.deltaPosition contenga
            // el movimiento real guardado dentro de JumpForward/JumpBackward.
            animator.applyRootMotion = true;
        }

        Vector3 facingDirection =
            startFacingRight ? Vector3.right : Vector3.left;

        transform.rotation =
            Quaternion.LookRotation(facingDirection, Vector3.up);
    }


    // =========================================================
    // ROOT MOTION PARA:
    // 1) JumpForward / JumpBackward
    // 2) R2 SpecialAttack
    // 3) Combo2
    // 4) ForwardCircleKick
    // 5) RunCircleSweep
    // 6) BullHornRush
    //
    // Estos clips ya contienen su propio desplazamiento.
    // Aqui NO inventamos movimiento adicional.
    // Solo aplicamos Animator.deltaPosition al CharacterController
    // para que el personaje TERMINE donde termina la animacion.
    // =========================================================

    private void OnAnimatorMove()
    {
        if (animator == null ||
            controller == null ||
            (!isJumping &&
             !isSpecialAttacking &&
             !isCombo2RootMotion &&
             !isForwardCircleKickRootMotion &&
             !isRunCircleSweepRootMotion &&
             !isBullHornRushRootMotion))
        {
            return;
        }

        Vector3 animationDelta =
            animator.deltaPosition;

        // El signo horizontal guardado dentro de cada FBX no decide
        // hacia que lado avanza el personaje. "Adelante" siempre es
        // hacia la posicion ACTUAL del rival, incluso despues de cruzarse.
        float directionTowardOpponent =
            startFacingRight ? 1f : -1f;

        if (opponent != null)
        {
            float opponentDeltaX =
                opponent.position.x - transform.position.x;

            if (Mathf.Abs(opponentDeltaX) > opponentFacingDeadZone)
            {
                directionTowardOpponent =
                    Mathf.Sign(opponentDeltaX);
            }
        }

        bool movingAwayFromOpponent =
            isJumping &&
            currentJumpType == JumpType.Backward;

        float horizontalDirection =
            movingAwayFromOpponent
                ? -directionTowardOpponent
                : directionTowardOpponent;

        animationDelta.x =
            Mathf.Abs(animationDelta.x) *
            horizontalDirection;

        if (isJumping)
        {
            UpdateJumpCrossingState(transform.position.x);

            float nextX = transform.position.x + animationDelta.x;
            UpdateJumpCrossingState(nextX);

            bool isDescendingRootMotion =
                animationDelta.y < -0.0001f;

            if (isDescendingRootMotion)
            {
                PreventRootMotionLandingOverlap(ref animationDelta);
            }
        }

        // Mantener el juego en una sola linea 2D.
        // Conservamos X/Y del propio clip:
        // - los saltos suben/bajan
        // - R2 corre, salta y cae
        // - Combo2 conserva el avance real de su propio clip
        // - ForwardCircleKick conserva la posicion final del clip
        // - RunCircleSweep conserva todo el impulso horizontal de la barrida
        // - BullHornRush conserva la carrera, embestida y posicion final del toro
        // Anulamos solamente la profundidad Z.
        animationDelta.z = 0f;

        // No dejar que el root motion salga de los limites de la arena.
        float desiredX =
            Mathf.Clamp(
                transform.position.x + animationDelta.x,
                minX,
                maxX
            );

        animationDelta.x =
            desiredX - transform.position.x;

        // El CharacterController aplica el movimiento del clip
        // respetando colisiones.
        controller.Move(animationDelta);
    }


    private void UpdateFacingOpponent()
    {
        if (!autoFaceOpponent || opponent == null)
        {
            return;
        }

        float deltaX =
            opponent.position.x - transform.position.x;

        if (Mathf.Abs(deltaX) <= opponentFacingDeadZone)
        {
            return;
        }

        // En un juego de lucha, "adelante" siempre debe ser hacia el rival.
        startFacingRight = deltaX > 0f;

        Vector3 facingDirection =
            startFacingRight ? Vector3.right : Vector3.left;

        transform.rotation =
            Quaternion.LookRotation(
                facingDirection,
                Vector3.up
            );
    }


    private void Update()
    {
        // Mantener a los dos luchadores mirandose.
        UpdateFacingOpponent();

        // Prueba temporal del ataque propio del toro.
        // Funciona incluso si Accept Player Input esta apagado.
        if (enableBullHornRush &&
            Keyboard.current != null &&
            Keyboard.current.pKey.wasPressedThisFrame)
        {
            StartBullHornRushAttack();
            return;
        }

        // El segundo personaje puede usar EXACTAMENTE este mismo
        // PlayerController sin responder al mismo gamepad.
        // Asi queda listo como rival/dummy hasta agregar Player 2 o AI.
        if (!acceptPlayerInput)
        {
            isBlocking = false;
            SetBackwardAnimation(false);
            SetCrouchAnimation(false);

            if (animator != null)
            {
                animator.SetFloat(SpeedHash, 0f);
                animator.SetBool(BlockingHash, false);
            }

            ApplyStationaryGravity();
            return;
        }

        // =====================================================
        // SUPER ATTACK CHARGE - L2
        // =====================================================

        if (isSuperCharging)
        {
            SetBackwardAnimation(false);
            SetCrouchAnimation(false);
            UpdateSuperCharge();
            ApplyStationaryGravity();
            return;
        }


        // =====================================================
        // DURANTE JUMP FORWARD / BACKWARD
        //
        // IMPORTANTE:
        // NO agregamos fuerza vertical ni movimiento horizontal.
        // La animacion se reproduce sola y el personaje queda
        // bloqueado hasta que esa animacion termine.
        // =====================================================

        if (isJumping)
        {
            // NO gravedad manual, NO fuerza de salto, NO movimiento extra.
            // OnAnimatorMove usa exactamente el movimiento guardado
            // dentro del clip JumpForward / JumpBackward.
            return;
        }


        // =====================================================
        // DURANTE BACKFLIP
        // =====================================================

        if (isBackflipping)
        {
            if (animator != null)
            {
                animator.SetFloat(SpeedHash, 0f);
                animator.SetBool(BlockingHash, false);
                animator.SetBool(WalkingBackwardHash, false);
                animator.SetBool(CrouchingHash, false);
            }

            return;
        }


        // =====================================================
        // DURANTE CUALQUIER ATAQUE
        // =====================================================

        if (isAttacking)
        {
            isBlocking = false;

            // Durante toda la cadena de Square seguimos escuchando:
            // 3 toques totales -> SquareFollowUp
            // 5 toques totales -> SquareSecondFollowUp
            HandleSquareFollowUpInput();

            // Durante toda la cadena de Triangle seguimos escuchando:
            // 3 toques totales -> TriangleFollowUp
            // 5 toques totales -> TriangleSecondFollowUp
            HandleTriangleFollowUpInput();

            // Durante LightKick seguimos escuchando X:
            // 3 toques totales -> XFollowUp
            HandleXFollowUpInput();

            if (animator != null)
            {
                // La barrida nace desde Running. Mientras Unity entra
                // en RunCircleSweep, NO debemos bajar Speed a 0.
                animator.SetFloat(
                    SpeedHash,
                    isRunCircleSweepEntering ? 2f : 0f
                );

                animator.SetBool(BlockingHash, false);
                animator.SetBool(WalkingBackwardHash, false);
                animator.SetBool(CrouchingHash, false);
            }

            if (isSpecialAttacking ||
                isCombo2RootMotion ||
                isForwardCircleKickRootMotion ||
                isRunCircleSweepRootMotion)
            {
                return;
            }

            ApplyStationaryGravity();
            return;
        }


        float moveAxis = 0f;
        bool running = false;
        bool blockHeld = false;
        bool crouchHeld = false;

        // +1 = hacia donde mira
        // -1 = hacia atras
        float forwardSign =
            startFacingRight ? 1f : -1f;


        // =====================================================
        // KEYBOARD - PRUEBAS
        // =====================================================

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed ||
                Keyboard.current.leftArrowKey.isPressed)
            {
                moveAxis -= 1f;
            }

            if (Keyboard.current.dKey.isPressed ||
                Keyboard.current.rightArrowKey.isPressed)
            {
                moveAxis += 1f;
            }

            if (Keyboard.current.leftShiftKey.isPressed ||
                Keyboard.current.rightShiftKey.isPressed)
            {
                if (moveAxis * forwardSign > 0.1f)
                {
                    running = true;
                }
            }

            // S o Flecha Abajo = crouch
            if (Keyboard.current.sKey.isPressed ||
                Keyboard.current.downArrowKey.isPressed)
            {
                crouchHeld = true;
            }

            // Q = block
            if (Keyboard.current.qKey.isPressed)
            {
                blockHeld = true;
            }

            // -------------------------------------------------
            // SALTOS DIRECCIONALES - TECLADO
            //
            // Arriba + Adelante = JumpForward
            // Arriba + Atras    = JumpBackward
            // Arriba SOLO       = no hace nada
            // -------------------------------------------------

            bool keyboardUpHeld =
                Keyboard.current.wKey.isPressed ||
                Keyboard.current.upArrowKey.isPressed;

            bool keyboardRightHeld =
                Keyboard.current.dKey.isPressed ||
                Keyboard.current.rightArrowKey.isPressed;

            bool keyboardLeftHeld =
                Keyboard.current.aKey.isPressed ||
                Keyboard.current.leftArrowKey.isPressed;

            bool keyboardForwardHeld =
                forwardSign > 0f
                    ? keyboardRightHeld
                    : keyboardLeftHeld;

            bool keyboardBackwardHeld =
                forwardSign > 0f
                    ? keyboardLeftHeld
                    : keyboardRightHeld;

            bool keyboardForwardJumpCombo =
                keyboardUpHeld && keyboardForwardHeld;

            bool keyboardBackwardJumpCombo =
                keyboardUpHeld && keyboardBackwardHeld;

            bool keyboardForwardJumpPressed =
                keyboardForwardJumpCombo &&
                !previousKeyboardForwardJumpCombo;

            bool keyboardBackwardJumpPressed =
                keyboardBackwardJumpCombo &&
                !previousKeyboardBackwardJumpCombo;

            previousKeyboardForwardJumpCombo =
                keyboardForwardJumpCombo;

            previousKeyboardBackwardJumpCombo =
                keyboardBackwardJumpCombo;

            if (!crouchHeld &&
                !blockHeld &&
                keyboardForwardJumpPressed)
            {
                StartJump(JumpType.Forward);
                return;
            }

            if (!crouchHeld &&
                !blockHeld &&
                keyboardBackwardJumpPressed)
            {
                StartJump(JumpType.Backward);
                return;
            }

            // Y = Backflip
            if (!crouchHeld &&
                !blockHeld &&
                Keyboard.current.yKey.wasPressedThisFrame)
            {
                StartBackflip();
                return;
            }

            // H = Super Attack Charge
            if (!crouchHeld &&
                !blockHeld &&
                Keyboard.current.hKey.wasPressedThisFrame)
            {
                StartSuperCharge();
                return;
            }

            // -------------------------------------------------
            // ABAJO + T = DownCircleKick (prueba teclado)
            //
            // Se procesa ANTES del bloqueo de crouch porque esta
            // patada nace precisamente de la direccion ABAJO.
            // -------------------------------------------------

            if (crouchHeld &&
                !blockHeld &&
                Keyboard.current.tKey.wasPressedThisFrame)
            {
                StartDownCircleKick();
                return;
            }

            if (!blockHeld && !crouchHeld)
            {
                // C = Combo1 directo para probar la animacion sin el control.
                if (Keyboard.current.cKey.wasPressedThisFrame)
                {
                    StartAttack(Combo1Hash);
                    return;
                }

                // V = Combo2 directo para probar la animacion sin el control.
                if (Keyboard.current.vKey.wasPressedThisFrame)
                {
                    StartAttack(Combo2Hash);
                    return;
                }

                // B = BackCircleKick directo para probar la animacion.
                if (Keyboard.current.bKey.wasPressedThisFrame)
                {
                    StartAttack(BackCircleKickHash);
                    return;
                }

                // J = DownCircleKick directo para probar la animacion.
                if (Keyboard.current.jKey.wasPressedThisFrame)
                {
                    StartDownCircleKick();
                    return;
                }

                // N = UpCircleKick directo para probar la animacion.
                if (Keyboard.current.nKey.wasPressedThisFrame)
                {
                    StartAttack(UpCircleKickHash);
                    return;
                }

                // M = ForwardCircleKick directo para probar la animacion.
                if (Keyboard.current.mKey.wasPressedThisFrame)
                {
                    StartAttack(ForwardCircleKickHash);
                    return;
                }

                // K = prueba de barrida mientras ya estas corriendo.
                if (Keyboard.current.kKey.wasPressedThisFrame &&
                    running &&
                    moveAxis * forwardSign > 0.1f)
                {
                    StartRunCircleSweep();
                    return;
                }

                if (Keyboard.current.fKey.wasPressedThisFrame)
                {
                    StartAttack(LightAttackHash);
                    return;
                }

                if (Keyboard.current.eKey.wasPressedThisFrame)
                {
                    StartAttack(HeavyAttackHash);
                    return;
                }

                if (Keyboard.current.rKey.wasPressedThisFrame)
                {
                    StartAttack(LightKickHash);
                    return;
                }

                if (Keyboard.current.tKey.wasPressedThisFrame)
                {
                    // Corriendo + T  = RunCircleSweep
                    // Arriba + T     = UpCircleKick
                    // Atras + T      = BackCircleKick
                    // Adelante + T   = ForwardCircleKick
                    // T solo         = HeavyKick
                    if (running &&
                        moveAxis * forwardSign > 0.1f)
                    {
                        StartRunCircleSweep();
                    }
                    else if (keyboardUpHeld)
                    {
                        StartAttack(UpCircleKickHash);
                    }
                    else if (moveAxis * forwardSign < -0.1f)
                    {
                        StartAttack(BackCircleKickHash);
                    }
                    else if (moveAxis * forwardSign > 0.1f)
                    {
                        StartAttack(ForwardCircleKickHash);
                    }
                    else
                    {
                        StartAttack(HeavyKickHash);
                    }

                    return;
                }

                if (Keyboard.current.gKey.wasPressedThisFrame)
                {
                    StartAttack(SpecialAttackHash);
                    return;
                }
            }
        }


        // =====================================================
        // PS5 DUALSENSE
        // =====================================================

        if (Gamepad.current != null)
        {
            Vector2 stick =
                Gamepad.current.leftStick.ReadValue();

            // -------------------------------------------------
            // ABAJO = CROUCH
            // -------------------------------------------------

            if (stick.y < -0.55f ||
                Gamepad.current.dpad.down.isPressed)
            {
                crouchHeld = true;
            }

            // -------------------------------------------------
            // L1 = BLOCK
            // -------------------------------------------------

            if (Gamepad.current.leftShoulder.isPressed)
            {
                blockHeld = true;
            }

            // -------------------------------------------------
            // SALTOS DIRECCIONALES - DUALSENSE
            //
            // Stick diagonal arriba+adelante = JumpForward
            // Stick diagonal arriba+atras    = JumpBackward
            // D-Pad ↑ + adelante             = JumpForward
            // D-Pad ↑ + atras                = JumpBackward
            // Arriba SOLO                    = no hace nada
            // -------------------------------------------------

            bool gamepadDpadForwardHeld =
                startFacingRight
                    ? Gamepad.current.dpad.right.isPressed
                    : Gamepad.current.dpad.left.isPressed;

            bool gamepadDpadBackwardHeld =
                startFacingRight
                    ? Gamepad.current.dpad.left.isPressed
                    : Gamepad.current.dpad.right.isPressed;

            bool stickUpHeld =
                stick.y > 0.65f;

            float relativeStickX =
                stick.x * forwardSign;

            bool stickForwardDiagonal =
                stickUpHeld &&
                relativeStickX > jumpDiagonalThreshold;

            bool stickBackwardDiagonal =
                stickUpHeld &&
                relativeStickX < -jumpDiagonalThreshold;

            bool dpadForwardDiagonal =
                Gamepad.current.dpad.up.isPressed &&
                gamepadDpadForwardHeld;

            bool dpadBackwardDiagonal =
                Gamepad.current.dpad.up.isPressed &&
                gamepadDpadBackwardHeld;

            bool gamepadForwardJumpCombo =
                stickForwardDiagonal ||
                dpadForwardDiagonal;

            bool gamepadBackwardJumpCombo =
                stickBackwardDiagonal ||
                dpadBackwardDiagonal;

            bool gamepadForwardJumpPressed =
                gamepadForwardJumpCombo &&
                !previousGamepadForwardJumpCombo;

            bool gamepadBackwardJumpPressed =
                gamepadBackwardJumpCombo &&
                !previousGamepadBackwardJumpCombo;

            previousGamepadForwardJumpCombo =
                gamepadForwardJumpCombo;

            previousGamepadBackwardJumpCombo =
                gamepadBackwardJumpCombo;

            if (!crouchHeld &&
                !blockHeld &&
                gamepadForwardJumpPressed)
            {
                StartJump(JumpType.Forward);
                return;
            }

            if (!crouchHeld &&
                !blockHeld &&
                gamepadBackwardJumpPressed)
            {
                StartJump(JumpType.Backward);
                return;
            }

            // -------------------------------------------------
            // JOYSTICK IZQUIERDO
            // -------------------------------------------------

            if (!crouchHeld &&
                Mathf.Abs(stick.x) > 0.1f)
            {
                moveAxis = stick.x;

                // L3 = Run, solo hacia adelante
                if (Gamepad.current.leftStickButton.isPressed &&
                    moveAxis * forwardSign > 0.1f)
                {
                    running = true;
                }
            }


            // -------------------------------------------------
            // D-PAD / FLECHAS
            //
            // Si startFacingRight = true:
            //   → = adelante
            //   ← = atras
            //
            // Si startFacingRight = false:
            //   ← = adelante
            //   → = atras
            //
            // Dos toques adelante + mantener segundo toque = Run
            // -------------------------------------------------

            bool dpadForwardHeld =
                startFacingRight
                    ? Gamepad.current.dpad.right.isPressed
                    : Gamepad.current.dpad.left.isPressed;

            bool dpadBackwardHeld =
                startFacingRight
                    ? Gamepad.current.dpad.left.isPressed
                    : Gamepad.current.dpad.right.isPressed;

            bool dpadForwardPressed =
                startFacingRight
                    ? Gamepad.current.dpad.right.wasPressedThisFrame
                    : Gamepad.current.dpad.left.wasPressedThisFrame;

            if (!crouchHeld && dpadForwardPressed)
            {
                if (Time.time - lastDpadForwardTapTime <= dpadDoubleTapWindow)
                {
                    dpadForwardRunActive = true;
                }
                else
                {
                    dpadForwardRunActive = false;
                }

                lastDpadForwardTapTime = Time.time;
            }

            if (!dpadForwardHeld)
            {
                dpadForwardRunActive = false;
            }

            // D-Pad tiene prioridad sobre joystick mientras se usa
            if (!crouchHeld && dpadForwardHeld && !dpadBackwardHeld)
            {
                moveAxis = forwardSign;

                if (dpadForwardRunActive)
                {
                    running = true;
                }
            }
            else if (!crouchHeld && dpadBackwardHeld && !dpadForwardHeld)
            {
                moveAxis = -forwardSign;
                running = false;
            }


            // -------------------------------------------------
            // R1 = BACKFLIP
            // -------------------------------------------------

            if (!crouchHeld &&
                !blockHeld &&
                Gamepad.current.rightShoulder.wasPressedThisFrame)
            {
                StartBackflip();
                return;
            }


            // -------------------------------------------------
            // L2 = SUPER ATTACK CHARGE
            // -------------------------------------------------

            if (!crouchHeld &&
                !blockHeld &&
                Gamepad.current.leftTrigger.wasPressedThisFrame)
            {
                StartSuperCharge();
                return;
            }


            // -------------------------------------------------
            // ABAJO + CIRCLE = DownCircleKick
            //
            // IMPORTANTE:
            // Se procesa antes del bloque "!crouchHeld" porque
            // ABAJO activa Crouch. Esta accion cancela Crouch y
            // reproduce directamente la patada.
            // -------------------------------------------------

            bool downHeldForCircleKick =
                stick.y < -0.55f ||
                Gamepad.current.dpad.down.isPressed;

            bool circlePressedForDownKick =
                Gamepad.current.buttonEast.wasPressedThisFrame;

            if (!blockHeld &&
                downHeldForCircleKick &&
                circlePressedForDownKick)
            {
                StartDownCircleKick();
                return;
            }

            if (!blockHeld && !crouchHeld)
            {
                // R2 = Special Attack
                if (Gamepad.current.rightTrigger.wasPressedThisFrame)
                {
                    StartAttack(SpecialAttackHash);
                    return;
                }

                // -------------------------------------------------
                // □ / △ / □+△
                //
                // Square solo       = LightAttack
                // Triangle solo     = HeavyAttack
                // Square + Triangle = Combo1
                //
                // Usa una ventana muy corta para que uno de los dos
                // botones pueda entrar algunos frames antes que el otro
                // sin disparar primero el golpe individual.
                // -------------------------------------------------

                if (HandleCombo1Input())
                {
                    return;
                }

                // -------------------------------------------------
                // ✕ / ○ / ✕+○
                //
                // X solo      = LightKick
                // Circle solo = HeavyKick
                // X + Circle  = Combo2
                //
                // Igual que Combo1, usa una ventana muy corta
                // para reconocer los dos botones sin disparar
                // primero una patada individual.
                // -------------------------------------------------

                bool backwardHeldForCircleKick =
                    (stick.x * forwardSign < -0.35f) ||
                    dpadBackwardHeld;

                bool forwardHeldForCircleKick =
                    (stick.x * forwardSign > 0.35f) ||
                    dpadForwardHeld;

                bool upHeldForCircleKick =
                    stick.y > 0.65f ||
                    Gamepad.current.dpad.up.isPressed;

                // -------------------------------------------------
                // CORRIENDO + CIRCLE = RunCircleSweep
                //
                // MUY IMPORTANTE:
                // Esta accion SOLO puede salir si Circle fue presionado
                // en ESTE frame mientras ya estamos corriendo hacia adelante.
                // Correr por si solo JAMAS puede disparar la barrida.
                //
                // Si X tambien esta presionado, dejamos que X+Circle
                // sea Combo2 y NO la barrida.
                // -------------------------------------------------

                bool runningForwardForSweep =
                    running &&
                    moveAxis * forwardSign > 0.1f;

                bool circlePressedForSweep =
                    Gamepad.current.buttonEast.wasPressedThisFrame;

                bool xHeldForCombo2 =
                    Gamepad.current.buttonSouth.isPressed;

                // La barrida SOLO sale cuando Circle se presiona
                // mientras el jugador ya mantiene la orden de correr.
                if (runningForwardForSweep &&
                    circlePressedForSweep &&
                    !xHeldForCombo2)
                {
                    ClearCombo2InputBuffer();
                    StartRunCircleSweep();
                    return;
                }

                if (HandleCombo2Input(
                        backwardHeldForCircleKick,
                        forwardHeldForCircleKick,
                        upHeldForCircleKick))
                {
                    return;
                }
            }
        }

        if (blockHeld || crouchHeld)
        {
            ClearCombo1InputBuffer();
            ClearCombo2InputBuffer();
        }

        // =====================================================
        // CROUCH / ABAJO
        // =====================================================

        isCrouching = crouchHeld && !blockHeld;

        SetCrouchAnimation(isCrouching);

        if (isCrouching)
        {
            moveAxis = 0f;
            running = false;
            SetBackwardAnimation(false);
        }


        // =====================================================
        // BLOCK
        // =====================================================

        isBlocking = blockHeld;

        if (animator != null)
        {
            animator.SetBool(BlockingHash, isBlocking);
        }

        if (isBlocking)
        {
            isCrouching = false;
            SetCrouchAnimation(false);

            moveAxis = 0f;
            running = false;
            SetBackwardAnimation(false);
        }


        // =====================================================
        // FORWARD / BACKWARD STATE
        // =====================================================

        moveAxis = Mathf.Clamp(moveAxis, -1f, 1f);

        bool moving =
            Mathf.Abs(moveAxis) > 0.01f;

        bool walkingBackward =
            moving &&
            !isBlocking &&
            !isCrouching &&
            moveAxis * forwardSign < -0.1f;

        if (walkingBackward)
        {
            running = false;
        }

        SetBackwardAnimation(walkingBackward);


        // =====================================================
        // HORIZONTAL MOVEMENT
        // =====================================================

        float currentSpeed =
            running && moving ? runSpeed : walkSpeed;

        float desiredDeltaX =
            moveAxis * currentSpeed * Time.deltaTime;

        float desiredX =
            Mathf.Clamp(
                transform.position.x + desiredDeltaX,
                minX,
                maxX
            );

        float deltaX =
            desiredX - transform.position.x;


        // =====================================================
        // GRAVITY
        // =====================================================

        if (controller.isGrounded &&
            verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        verticalVelocity +=
            gravity * Time.deltaTime;


        // =====================================================
        // LOCK Z + MOVE
        // =====================================================

        float zCorrection =
            (laneZ - transform.position.z) *
            laneLockSpeed *
            Time.deltaTime;

        Vector3 motion =
            new Vector3(
                deltaX,
                verticalVelocity * Time.deltaTime,
                zCorrection
            );

        controller.Move(motion);


        // =====================================================
        // ANIMACIONES DE MOVIMIENTO
        // =====================================================

        if (animator != null)
        {
            float animationSpeed = 0f;

            // Backward walk usa WalkingBackward,
            // por eso Speed queda en 0 mientras retrocede.
            if (moving &&
                !isBlocking &&
                !isCrouching &&
                !walkingBackward)
            {
                animationSpeed =
                    running ? 2f : 1f;
            }

            animator.SetFloat(
                SpeedHash,
                animationSpeed
            );
        }
    }


    // =========================================================
    // ABAJO + CIRCLE = DOWN CIRCLE KICK
    //
    // StartAttack() normalmente bloquea ataques mientras isCrouching.
    // Esta accion es la excepcion: nace desde ABAJO, por eso primero
    // sacamos al personaje de Crouch y luego usamos el sistema normal
    // de ataque.
    // =========================================================

    private void StartDownCircleKick()
    {
        if (animator == null ||
            isBlocking ||
            isJumping ||
            isBackflipping ||
            isAttacking ||
            isSuperCharging)
        {
            return;
        }

        ClearCombo1InputBuffer();
        ClearCombo2InputBuffer();

        // Cancelar Crouch inmediatamente para que no se mezcle
        // una animacion extra antes de la patada.
        isCrouching = false;
        animator.SetBool(CrouchingHash, false);

        StartAttack(DownCircleKickHash);
    }


    // =========================================================
    // SQUARE x3 = LIGHT ATTACK + FOLLOW UP
    //
    // Square 1:
    //   ejecuta LightAttack exactamente como hasta ahora.
    //
    // Square 2 + Square 3, mientras LightAttack esta activo:
    //   ponen SquareFollowUpQueued = true.
    //
    // El Animator decide el momento exacto de la transicion:
    // LightAttack -> SquareFollowUp
    // =========================================================

    private void HandleSquareFollowUpInput()
    {
        if (!isLightAttackChainActive ||
            animator == null ||
            squareSecondFollowUpQueued)
        {
            return;
        }

        bool extraSquarePressed = false;

        if (Gamepad.current != null &&
            Gamepad.current.buttonWest.wasPressedThisFrame)
        {
            extraSquarePressed = true;
        }

        // F repetido sirve para probarlo con teclado.
        if (Keyboard.current != null &&
            Keyboard.current.fKey.wasPressedThisFrame)
        {
            extraSquarePressed = true;
        }

        if (!extraSquarePressed)
        {
            return;
        }

        squareFollowUpTapCount++;

        // Tercer toque TOTAL:
        // El primer Square ya inició LightAttack.
        if (!squareFollowUpQueued &&
            squareFollowUpTapCount >=
                Mathf.Max(squareFollowUpExtraTaps, 1))
        {
            squareFollowUpQueued = true;

            animator.SetBool(
                SquareFollowUpQueuedHash,
                true
            );
        }

        // Quinto toque TOTAL:
        // Cuatro toques extra después del Square inicial.
        if (!squareSecondFollowUpQueued &&
            squareFollowUpTapCount >=
                Mathf.Max(squareSecondFollowUpExtraTaps, 2))
        {
            squareSecondFollowUpQueued = true;

            animator.SetBool(
                SquareSecondFollowUpQueuedHash,
                true
            );
        }
    }


    private void BeginSquareFollowUpWindow()
    {
        isLightAttackChainActive = true;
        squareFollowUpTapCount = 0;
        squareFollowUpQueued = false;
        squareSecondFollowUpQueued = false;

        if (animator != null)
        {
            animator.SetBool(
                SquareFollowUpQueuedHash,
                false
            );

            animator.SetBool(
                SquareSecondFollowUpQueuedHash,
                false
            );
        }
    }


    private void ResetSquareFollowUpWindow()
    {
        isLightAttackChainActive = false;
        squareFollowUpTapCount = 0;
        squareFollowUpQueued = false;
        squareSecondFollowUpQueued = false;

        if (animator != null)
        {
            animator.SetBool(
                SquareFollowUpQueuedHash,
                false
            );

            animator.SetBool(
                SquareSecondFollowUpQueuedHash,
                false
            );
        }
    }


    // =========================================================
    // TRIANGLE x3 = HEAVY ATTACK + FOLLOW UP
    //
    // Triangle 1:
    //   ejecuta HeavyAttack exactamente como hasta ahora.
    //
    // Triangle 2 + Triangle 3, mientras HeavyAttack esta activo:
    //   ponen TriangleFollowUpQueued = true.
    //
    // El Animator decide el momento exacto de la transicion:
    // HeavyAttack -> TriangleFollowUp
    // =========================================================

    private void HandleTriangleFollowUpInput()
    {
        if (!isHeavyAttackChainActive ||
            animator == null ||
            triangleSecondFollowUpQueued)
        {
            return;
        }

        bool extraTrianglePressed = false;

        if (Gamepad.current != null &&
            Gamepad.current.buttonNorth.wasPressedThisFrame)
        {
            extraTrianglePressed = true;
        }

        if (Keyboard.current != null &&
            Keyboard.current.eKey.wasPressedThisFrame)
        {
            extraTrianglePressed = true;
        }

        if (!extraTrianglePressed)
        {
            return;
        }

        triangleFollowUpTapCount++;

        if (!triangleFollowUpQueued &&
            triangleFollowUpTapCount >=
                Mathf.Max(triangleFollowUpExtraTaps, 1))
        {
            triangleFollowUpQueued = true;

            animator.SetBool(
                TriangleFollowUpQueuedHash,
                true
            );
        }

        if (!triangleSecondFollowUpQueued &&
            triangleFollowUpTapCount >=
                Mathf.Max(triangleSecondFollowUpExtraTaps, 2))
        {
            triangleSecondFollowUpQueued = true;

            animator.SetBool(
                TriangleSecondFollowUpQueuedHash,
                true
            );
        }
    }


    private void BeginTriangleFollowUpWindow()
    {
        isHeavyAttackChainActive = true;
        triangleFollowUpTapCount = 0;
        triangleFollowUpQueued = false;
        triangleSecondFollowUpQueued = false;

        if (animator != null)
        {
            animator.SetBool(
                TriangleFollowUpQueuedHash,
                false
            );

            animator.SetBool(
                TriangleSecondFollowUpQueuedHash,
                false
            );
        }
    }


    private void ResetTriangleFollowUpWindow()
    {
        isHeavyAttackChainActive = false;
        triangleFollowUpTapCount = 0;
        triangleFollowUpQueued = false;
        triangleSecondFollowUpQueued = false;

        if (animator != null)
        {
            animator.SetBool(
                TriangleFollowUpQueuedHash,
                false
            );

            animator.SetBool(
                TriangleSecondFollowUpQueuedHash,
                false
            );
        }
    }


    // =========================================================
    // COMBO 1 INPUT - SQUARE + TRIANGLE
    //
    // IMPORTANTE:
    // Para permitir una combinacion humana de dos botones,
    // Square y Triangle esperan combo1InputWindow segundos
    // antes de convertirse en sus ataques individuales.
    //
    // Si el segundo boton entra dentro de esa ventana:
    // SOLO se dispara Combo1.
    // =========================================================

    private bool HandleCombo1Input()
    {
        if (Gamepad.current == null)
        {
            ClearCombo1InputBuffer();
            return false;
        }

        bool squarePressed =
            Gamepad.current.buttonWest.wasPressedThisFrame;

        bool trianglePressed =
            Gamepad.current.buttonNorth.wasPressedThisFrame;

        bool squareHeld =
            Gamepad.current.buttonWest.isPressed;

        bool triangleHeld =
            Gamepad.current.buttonNorth.isPressed;

        // Los dos entraron en el mismo frame.
        if (squarePressed && trianglePressed)
        {
            ClearCombo1InputBuffer();
            StartAttack(Combo1Hash);
            return true;
        }

        // Square entra mientras Triangle ya estaba esperando
        // o sigue fisicamente presionado.
        if (squarePressed)
        {
            if (triangleAttackPending || triangleHeld)
            {
                ClearCombo1InputBuffer();
                StartAttack(Combo1Hash);
                return true;
            }

            squareAttackPending = true;
            squareAttackPendingTimer =
                Mathf.Max(combo1InputWindow, 0.01f);
        }

        // Triangle entra mientras Square ya estaba esperando
        // o sigue fisicamente presionado.
        if (trianglePressed)
        {
            if (squareAttackPending || squareHeld)
            {
                ClearCombo1InputBuffer();
                StartAttack(Combo1Hash);
                return true;
            }

            triangleAttackPending = true;
            triangleAttackPendingTimer =
                Mathf.Max(combo1InputWindow, 0.01f);
        }

        // Si Square quedo solo, despues de la ventana corta
        // se convierte en LightAttack.
        if (squareAttackPending)
        {
            squareAttackPendingTimer -= Time.deltaTime;

            if (squareAttackPendingTimer <= 0f)
            {
                squareAttackPending = false;
                squareAttackPendingTimer = 0f;

                StartAttack(LightAttackHash);
                return true;
            }
        }

        // Si Triangle quedo solo, despues de la ventana corta
        // se convierte en HeavyAttack.
        if (triangleAttackPending)
        {
            triangleAttackPendingTimer -= Time.deltaTime;

            if (triangleAttackPendingTimer <= 0f)
            {
                triangleAttackPending = false;
                triangleAttackPendingTimer = 0f;

                StartAttack(HeavyAttackHash);
                return true;
            }
        }

        return false;
    }


    private void ClearCombo1InputBuffer()
    {
        squareAttackPending = false;
        triangleAttackPending = false;

        squareAttackPendingTimer = 0f;
        triangleAttackPendingTimer = 0f;
    }


    // =========================================================
    // X x3 = LIGHT KICK + FOLLOW UP
    //
    // X 1:
    //   mantiene LightKick exactamente como ya funcionaba.
    //
    // X 2 + X 3:
    //   ponen XFollowUpQueued = true.
    //
    // La cuenta también conserva los toques que ocurran durante
    // la ventana corta usada para distinguir X de X + Circle.
    // =========================================================

    private void HandleXFollowUpInput()
    {
        if (!isLightKickChainActive ||
            xFollowUpQueued ||
            animator == null)
        {
            return;
        }

        bool extraXPressed = false;

        if (Gamepad.current != null &&
            Gamepad.current.buttonSouth.wasPressedThisFrame)
        {
            extraXPressed = true;
        }

        // R R R sirve para probarlo con teclado.
        if (Keyboard.current != null &&
            Keyboard.current.rKey.wasPressedThisFrame)
        {
            extraXPressed = true;
        }

        if (!extraXPressed)
        {
            return;
        }

        xFollowUpTapCount++;

        if (xFollowUpTapCount >=
            Mathf.Max(xFollowUpExtraTaps, 1))
        {
            xFollowUpQueued = true;

            animator.SetBool(
                XFollowUpQueuedHash,
                true
            );
        }
    }


    private void BeginXFollowUpWindow()
    {
        isLightKickChainActive = true;
        xFollowUpTapCount = 0;
        xFollowUpQueued = false;

        if (animator != null)
        {
            animator.SetBool(
                XFollowUpQueuedHash,
                false
            );
        }
    }


    private void ApplyBufferedXTapCount(int totalXTaps)
    {
        if (!isLightKickChainActive ||
            animator == null)
        {
            return;
        }

        // El primer toque ya fue el que inició LightKick.
        xFollowUpTapCount =
            Mathf.Max(totalXTaps - 1, 0);

        if (xFollowUpTapCount >=
            Mathf.Max(xFollowUpExtraTaps, 1))
        {
            xFollowUpQueued = true;

            animator.SetBool(
                XFollowUpQueuedHash,
                true
            );
        }
    }


    private void ResetXFollowUpWindow()
    {
        isLightKickChainActive = false;
        xFollowUpTapCount = 0;
        xFollowUpQueued = false;

        if (animator != null)
        {
            animator.SetBool(
                XFollowUpQueuedHash,
                false
            );
        }
    }


    // =========================================================
    // COMBO 2 INPUT - X + CIRCLE
    //
    // X solo      = LightKick
    // Circle solo = HeavyKick
    // X + Circle  = Combo2
    //
    // Si el segundo boton entra dentro de combo2InputWindow:
    // SOLO se dispara Combo2.
    // =========================================================

    private bool HandleCombo2Input(
        bool backwardHeldForCircleKick,
        bool forwardHeldForCircleKick,
        bool upHeldForCircleKick)
    {
        if (Gamepad.current == null)
        {
            ClearCombo2InputBuffer();
            return false;
        }

        bool xPressed =
            Gamepad.current.buttonSouth.wasPressedThisFrame;

        bool circlePressed =
            Gamepad.current.buttonEast.wasPressedThisFrame;

        bool xHeld =
            Gamepad.current.buttonSouth.isPressed;

        bool circleHeld =
            Gamepad.current.buttonEast.isPressed;

        // Los dos entraron en el mismo frame.
        // Combo2 tiene prioridad sobre Atras + Circle.
        if (xPressed && circlePressed)
        {
            ClearCombo2InputBuffer();
            StartAttack(Combo2Hash);
            return true;
        }

        // Arriba + Circle = nueva patada especial.
        // Solo se activa si X NO esta participando en Combo2.
        if (circlePressed &&
            upHeldForCircleKick &&
            !xHeld &&
            !xKickPending)
        {
            ClearCombo2InputBuffer();
            StartAttack(UpCircleKickHash);
            return true;
        }

        // Atras + Circle = patada especial hacia atras.
        // Solo se activa si X NO esta participando en Combo2.
        if (circlePressed &&
            backwardHeldForCircleKick &&
            !xHeld &&
            !xKickPending)
        {
            ClearCombo2InputBuffer();
            StartAttack(BackCircleKickHash);
            return true;
        }

        // Adelante + Circle = patada especial con avance.
        // Esta animacion usa Root Motion y conserva su posicion final.
        if (circlePressed &&
            forwardHeldForCircleKick &&
            !xHeld &&
            !xKickPending)
        {
            ClearCombo2InputBuffer();
            StartAttack(ForwardCircleKickHash);
            return true;
        }

        // X entra mientras Circle ya estaba esperando
        // o sigue fisicamente presionado.
        if (xPressed)
        {
            if (circleKickPending || circleHeld)
            {
                ClearCombo2InputBuffer();
                StartAttack(Combo2Hash);
                return true;
            }

            if (xKickPending)
            {
                xBufferedTapCount++;
            }
            else
            {
                xBufferedTapCount = 1;
            }

            xKickPending = true;
            xKickPendingTimer =
                Mathf.Max(combo2InputWindow, 0.01f);
        }

        // Circle entra mientras X ya estaba esperando
        // o sigue fisicamente presionado.
        if (circlePressed)
        {
            if (xKickPending || xHeld)
            {
                ClearCombo2InputBuffer();
                StartAttack(Combo2Hash);
                return true;
            }

            circleKickPending = true;
            circleKickPendingTimer =
                Mathf.Max(combo2InputWindow, 0.01f);
        }

        // Si Circle entro primero y dentro de la ventana
        // aparece Arriba, convertirlo en UpCircleKick.
        if (circleKickPending &&
            upHeldForCircleKick &&
            !xKickPending &&
            !xHeld)
        {
            ClearCombo2InputBuffer();
            StartAttack(UpCircleKickHash);
            return true;
        }

        // Si Circle entro primero y dentro de la ventana
        // aparece la direccion Atras, convertirlo en BackCircleKick.
        if (circleKickPending &&
            backwardHeldForCircleKick &&
            !xKickPending &&
            !xHeld)
        {
            ClearCombo2InputBuffer();
            StartAttack(BackCircleKickHash);
            return true;
        }

        // Si Circle entro primero y dentro de la ventana
        // aparece Adelante, convertirlo en ForwardCircleKick.
        if (circleKickPending &&
            forwardHeldForCircleKick &&
            !xKickPending &&
            !xHeld)
        {
            ClearCombo2InputBuffer();
            StartAttack(ForwardCircleKickHash);
            return true;
        }

        // X quedo solo -> LightKick.
        if (xKickPending)
        {
            xKickPendingTimer -= Time.deltaTime;

            if (xKickPendingTimer <= 0f)
            {
                int bufferedXTaps =
                    Mathf.Max(xBufferedTapCount, 1);

                xKickPending = false;
                xKickPendingTimer = 0f;

                StartAttack(LightKickHash);

                // Si el usuario ya tocó X dos o tres veces durante
                // la ventana X / X+Circle, no perdemos esos toques.
                ApplyBufferedXTapCount(bufferedXTaps);

                return true;
            }
        }

        // Circle quedo solo -> HeavyKick.
        if (circleKickPending)
        {
            circleKickPendingTimer -= Time.deltaTime;

            if (circleKickPendingTimer <= 0f)
            {
                circleKickPending = false;
                circleKickPendingTimer = 0f;

                StartAttack(HeavyKickHash);
                return true;
            }
        }

        return false;
    }


    private void ClearCombo2InputBuffer()
    {
        xKickPending = false;
        circleKickPending = false;

        xKickPendingTimer = 0f;
        circleKickPendingTimer = 0f;
        xBufferedTapCount = 0;
    }


    // =========================================================
    // WALK BACKWARD ANIMATION
    // =========================================================

    private void SetBackwardAnimation(bool value)
    {
        if (animator != null)
        {
            animator.SetBool(
                WalkingBackwardHash,
                value
            );
        }
    }


    // =========================================================
    // CROUCH ANIMATION
    // =========================================================

    private void SetCrouchAnimation(bool value)
    {
        if (animator != null)
        {
            animator.SetBool(
                CrouchingHash,
                value
            );
        }
    }


    // =========================================================
    // SALTOS DIRECCIONALES - ROOT MOTION DEL PROPIO CLIP
    //
    // NO agregamos fuerza, altura ni trayectoria inventada.
    // JumpForward / JumpBackward ya contienen el salto completo.
    // OnAnimatorMove aplica exactamente ese movimiento al personaje.
    //
    // En WolfAnimator:
    // JumpForward  -> Tag = Jump
    // JumpBackward -> Tag = Jump
    // =========================================================

    private void StartJump(JumpType jumpType)
    {
        ResetSquareFollowUpWindow();
        ResetTriangleFollowUpWindow();
        ResetXFollowUpWindow();
        ClearCombo2InputBuffer();
        ClearCombo1InputBuffer();

        if (animator == null ||
            isBlocking ||
            isCrouching ||
            isJumping ||
            isBackflipping ||
            isAttacking ||
            isSuperCharging)
        {
            return;
        }

        currentJumpType = jumpType;
        jumpHadOpponentAtStart = opponent != null;
        crossedOpponentDuringJump = false;

        if (jumpHadOpponentAtStart)
        {
            jumpStartSide = transform.position.x < opponent.position.x
                ? -1
                : 1;
        }
        else
        {
            jumpStartSide = 0;
        }

        isJumping = true;
        isBlocking = false;
        isCrouching = false;

        animator.SetBool(BlockingHash, false);
        animator.SetBool(WalkingBackwardHash, false);
        animator.SetBool(CrouchingHash, false);
        animator.SetFloat(SpeedHash, 0f);

        if (jumpType == JumpType.Forward)
        {
            animator.SetTrigger(JumpForwardHash);
        }
        else
        {
            animator.SetTrigger(JumpBackwardHash);
        }

        StartCoroutine(JumpAnimationLockRoutine());
    }


    private IEnumerator JumpAnimationLockRoutine()
    {
        // Da tiempo al Animator para entrar en el clip correcto.
        float waitForEnter = 0f;
        const float enterTimeout = 0.75f;
        bool enteredJumpState = false;

        while (waitForEnter < enterTimeout)
        {
            AnimatorStateInfo current =
                animator.GetCurrentAnimatorStateInfo(0);

            bool currentIsJump =
                current.IsTag("Jump");

            bool nextIsJump = false;

            if (animator.IsInTransition(0))
            {
                AnimatorStateInfo next =
                    animator.GetNextAnimatorStateInfo(0);

                nextIsJump =
                    next.IsTag("Jump");
            }

            if (currentIsJump || nextIsJump)
            {
                enteredJumpState = true;
                break;
            }

            waitForEnter += Time.deltaTime;
            yield return null;
        }

        if (!enteredJumpState)
        {
            Debug.LogWarning(
                "El salto no entro a un estado con Tag = Jump. " +
                "Pon Tag = Jump en JumpForward y JumpBackward."
            );

            isJumping = false;
            jumpHadOpponentAtStart = false;
            jumpStartSide = 0;
            crossedOpponentDuringJump = false;
            yield break;
        }

        // Mantiene todos los controles bloqueados mientras
        // la animacion del salto siga reproduciendose.
        while (true)
        {
            AnimatorStateInfo current =
                animator.GetCurrentAnimatorStateInfo(0);

            bool currentIsJump =
                current.IsTag("Jump");

            bool nextIsJump = false;

            if (animator.IsInTransition(0))
            {
                AnimatorStateInfo next =
                    animator.GetNextAnimatorStateInfo(0);

                nextIsJump =
                    next.IsTag("Jump");
            }

            if (!currentIsJump && !nextIsJump)
            {
                break;
            }

            yield return null;
        }

        isJumping = false;
        jumpHadOpponentAtStart = false;
        jumpStartSide = 0;
        crossedOpponentDuringJump = false;
        verticalVelocity = -2f;
    }


    private void UpdateJumpCrossingState(float predictedPlayerX)
    {
        if (!jumpHadOpponentAtStart || crossedOpponentDuringJump ||
            opponent == null)
        {
            return;
        }

        float opponentX = opponent.position.x;

        crossedOpponentDuringJump = jumpStartSide < 0
            ? predictedPlayerX > opponentX
            : predictedPlayerX < opponentX;
    }


    private void PreventRootMotionLandingOverlap(ref Vector3 animationDelta)
    {
        if (!jumpHadOpponentAtStart || opponent == null || controller == null)
        {
            return;
        }

        CharacterController opponentController =
            opponent.GetComponent<CharacterController>();

        if (opponentController == null || !opponentController.enabled)
        {
            return;
        }

        float minimumSeparation = GetLandingMinimumSeparation(
            opponentController
        );
        float nextX = transform.position.x + animationDelta.x;
        float opponentX = opponent.position.x;

        if (Mathf.Abs(nextX - opponentX) >= minimumSeparation)
        {
            return;
        }

        float safeX = Mathf.Clamp(
            GetLandingSafeX(opponentX, minimumSeparation),
            minX,
            maxX
        );

        // Separar primero en horizontal antes de aplicar la bajada del
// root motion. Asi el CharacterController no puede deslizarse
// sobre la capsula del rival y terminar parado encima.
Vector3 landingCorrection =
new Vector3(
safeX - transform.position.x,
0f,
0f
);

    controller.Move(landingCorrection);

    // La correccion X ya fue aplicada. El Move principal conserva
    // solamente la bajada Y del propio clip.
    animationDelta.x = 0f;

    }


    private float GetLandingMinimumSeparation(
        CharacterController opponentController
    )
    {
        return controller.radius +
               opponentController.radius +
               0.15f;
    }


    private float GetLandingSafeX(
        float opponentX,
        float minimumSeparation
    )
    {
        int landingSide = crossedOpponentDuringJump
            ? -jumpStartSide
            : jumpStartSide;

        return opponentX + landingSide * minimumSeparation;
    }


    // =========================================================
    // L2 SUPER ATTACK CHARGE
    // =========================================================

    private void StartSuperCharge()
    {
        ResetSquareFollowUpWindow();
        ResetTriangleFollowUpWindow();
        ResetXFollowUpWindow();
        ClearCombo2InputBuffer();
        ClearCombo1InputBuffer();

        if (animator == null ||
            isBlocking ||
            isCrouching ||
            isJumping ||
            isBackflipping ||
            isAttacking ||
            isSuperCharging)
        {
            return;
        }

        isSuperCharging = true;
        isAttacking = true;
        isSuperAttacking = false;
        isSpecialAttacking = false;
        isCombo2RootMotion = false;
        isForwardCircleKickRootMotion = false;
        isRunCircleSweepRootMotion = false;
        isRunCircleSweepEntering = false;
        isBlocking = false;

        superChargeTimer = 0f;
        LastSuperChargeRatio = 0f;

        animator.SetBool(BlockingHash, false);
        animator.SetBool(WalkingBackwardHash, false);
        animator.SetBool(CrouchingHash, false);
        animator.SetFloat(SpeedHash, 0f);
        animator.SetBool(SuperChargingHash, true);
    }


    private void UpdateSuperCharge()
    {
        superChargeTimer += Time.deltaTime;

        bool stillHoldingCharge = false;

        if (Gamepad.current != null &&
            Gamepad.current.leftTrigger.isPressed)
        {
            stillHoldingCharge = true;
        }

        if (Keyboard.current != null &&
            Keyboard.current.hKey.isPressed)
        {
            stillHoldingCharge = true;
        }

        bool reachedMaximumCharge =
            superChargeTimer >= superChargeMaxTime;

        if (!stillHoldingCharge ||
            reachedMaximumCharge)
        {
            ReleaseSuperAttack();
        }
    }


    private void ReleaseSuperAttack()
    {
        if (!isSuperCharging)
            return;

        LastSuperChargeRatio =
            Mathf.Clamp01(
                superChargeTimer /
                Mathf.Max(superChargeMaxTime, 0.01f)
            );

        isSuperCharging = false;
        isSuperAttacking = true;

        animator.SetBool(SuperChargingHash, false);
        animator.SetTrigger(SuperAttackHash);

        StartCoroutine(SuperAttackLockRoutine());
    }


    private IEnumerator SuperAttackLockRoutine()
    {
        float waitForEnter = 0f;
        const float enterTimeout = 0.75f;
        bool enteredAttackState = false;

        while (waitForEnter < enterTimeout)
        {
            AnimatorStateInfo current =
                animator.GetCurrentAnimatorStateInfo(0);

            bool currentIsAttack =
                current.IsTag("Attack");

            bool nextIsAttack = false;

            if (animator.IsInTransition(0))
            {
                AnimatorStateInfo next =
                    animator.GetNextAnimatorStateInfo(0);

                nextIsAttack =
                    next.IsTag("Attack");
            }

            if (currentIsAttack || nextIsAttack)
            {
                enteredAttackState = true;
                break;
            }

            waitForEnter += Time.deltaTime;
            yield return null;
        }

        if (!enteredAttackState)
        {
            Debug.LogWarning(
                "SuperAttack no entro a un estado con Tag = Attack. " +
                "Revisa el Tag de la caja SuperAttack."
            );

            isAttacking = false;
            isSuperAttacking = false;
            yield break;
        }

        while (true)
        {
            AnimatorStateInfo current =
                animator.GetCurrentAnimatorStateInfo(0);

            bool currentIsAttack =
                current.IsTag("Attack");

            bool nextIsAttack = false;

            if (animator.IsInTransition(0))
            {
                AnimatorStateInfo next =
                    animator.GetNextAnimatorStateInfo(0);

                nextIsAttack =
                    next.IsTag("Attack");
            }

            if (!currentIsAttack && !nextIsAttack)
            {
                break;
            }

            yield return null;
        }

        isAttacking = false;
        isSuperAttacking = false;
        isSpecialAttacking = false;
        isCombo2RootMotion = false;
        isForwardCircleKickRootMotion = false;
        isRunCircleSweepRootMotion = false;
        isRunCircleSweepEntering = false;
        verticalVelocity = -2f;
    }


    // =========================================================
    // RUN + CIRCLE = RUN CIRCLE SWEEP
    //
    // IMPORTANTE:
    // No usa StartAttack(), porque StartAttack() pone Speed=0.
    // Eso sacaba al Animator de Running antes de que pudiera
    // consumir la transicion Running -> RunCircleSweep.
    // =========================================================

    private void StartRunCircleSweep()
    {
        ResetSquareFollowUpWindow();
        ResetTriangleFollowUpWindow();
        ResetXFollowUpWindow();
        if (animator == null ||
            isBlocking ||
            isCrouching ||
            isJumping ||
            isBackflipping ||
            isAttacking ||
            isSuperCharging)
        {
            return;
        }

        ClearCombo1InputBuffer();
        ClearCombo2InputBuffer();

        isAttacking = true;
        isBlocking = false;

        isSpecialAttacking = false;
        isCombo2RootMotion = false;
        isForwardCircleKickRootMotion = false;

        isRunCircleSweepRootMotion = true;
        isRunCircleSweepEntering = true;

        animator.SetBool(BlockingHash, false);
        animator.SetBool(WalkingBackwardHash, false);
        animator.SetBool(CrouchingHash, false);

        // Mantener el estado Running vivo para que esta transición
        // pueda ser consumida inmediatamente.
        animator.SetFloat(SpeedHash, 2f);

        animator.ResetTrigger(RunCircleSweepHash);
        animator.SetTrigger(RunCircleSweepHash);

        StartCoroutine(RunCircleSweepLockRoutine());
    }


    private IEnumerator RunCircleSweepLockRoutine()
    {
        float waitForEnter = 0f;
        const float enterTimeout = 0.75f;
        bool enteredSweep = false;

        while (waitForEnter < enterTimeout)
        {
            AnimatorStateInfo current =
                animator.GetCurrentAnimatorStateInfo(0);

            bool currentIsAttack =
                current.IsTag("Attack");

            bool nextIsAttack = false;

            if (animator.IsInTransition(0))
            {
                AnimatorStateInfo next =
                    animator.GetNextAnimatorStateInfo(0);

                nextIsAttack =
                    next.IsTag("Attack");
            }

            if (currentIsAttack || nextIsAttack)
            {
                enteredSweep = true;
                isRunCircleSweepEntering = false;

                // Ya no puede quedar pendiente para una carrera futura.
                animator.ResetTrigger(RunCircleSweepHash);
                break;
            }

            // Todavia esperando: conservar Running.
            animator.SetFloat(SpeedHash, 2f);

            waitForEnter += Time.deltaTime;
            yield return null;
        }

        if (!enteredSweep)
        {
            animator.ResetTrigger(RunCircleSweepHash);

            isAttacking = false;
            isRunCircleSweepEntering = false;
            isRunCircleSweepRootMotion = false;

            Debug.LogWarning(
                "RunCircleSweep no pudo entrar. Revisa que exista " +
                "Running -> RunCircleSweep con Condition RunCircleSweep " +
                "y que RunCircleSweep tenga Tag = Attack."
            );

            yield break;
        }

        animator.SetFloat(SpeedHash, 0f);

        while (true)
        {
            AnimatorStateInfo current =
                animator.GetCurrentAnimatorStateInfo(0);

            bool currentIsAttack =
                current.IsTag("Attack");

            bool nextIsAttack = false;

            if (animator.IsInTransition(0))
            {
                AnimatorStateInfo next =
                    animator.GetNextAnimatorStateInfo(0);

                nextIsAttack =
                    next.IsTag("Attack");
            }

            if (!currentIsAttack && !nextIsAttack)
            {
                break;
            }

            yield return null;
        }

        isAttacking = false;
        isRunCircleSweepEntering = false;
        isRunCircleSweepRootMotion = false;

        verticalVelocity = -2f;
    }


    // =========================================================
    // BULL HORN RUSH
    //
    // Metodo publico para que mas adelante EnemyAI, Player 2
    // o un bridge externo pueda ordenar este ataque sin duplicar codigo.
    // =========================================================

    public void StartBullHornRushAttack()
    {
        if (!enableBullHornRush)
        {
            return;
        }

        StartAttack(BullHornRushHash);
    }


    // =========================================================
    // NORMAL / SPECIAL ATTACK LOCK
    // =========================================================

    private void StartAttack(int triggerHash)
    {
        ClearCombo2InputBuffer();
        ClearCombo1InputBuffer();

        if (animator == null ||
            isBlocking ||
            isCrouching ||
            isJumping ||
            isBackflipping ||
            isAttacking ||
            isSuperCharging)
        {
            return;
        }

        isAttacking = true;
        isBlocking = false;

        if (triggerHash == LightAttackHash)
        {
            BeginSquareFollowUpWindow();
            ResetTriangleFollowUpWindow();
            ResetXFollowUpWindow();
        }
        else if (triggerHash == HeavyAttackHash)
        {
            ResetSquareFollowUpWindow();
            BeginTriangleFollowUpWindow();
            ResetXFollowUpWindow();
        }
        else if (triggerHash == LightKickHash)
        {
            ResetSquareFollowUpWindow();
            ResetTriangleFollowUpWindow();
            BeginXFollowUpWindow();
        }
        else
        {
            ResetSquareFollowUpWindow();
            ResetTriangleFollowUpWindow();
            ResetXFollowUpWindow();
        }

        // R2 / SpecialAttack conserva su Root Motion.
        isSpecialAttacking =
            triggerHash == SpecialAttackHash;

        // Combo2 tambien conserva el desplazamiento real de su FBX.
        // Asi, si termina mas adelante, el CharacterController
        // termina exactamente en esa nueva posicion.
        isCombo2RootMotion =
            triggerHash == Combo2Hash;

        // Adelante + Circle tambien conserva el desplazamiento
        // horizontal de su propia animacion.
        isForwardCircleKickRootMotion =
            triggerHash == ForwardCircleKickHash;

        // BullHornRush usa exactamente el desplazamiento guardado
        // dentro de BullHornRush.anim.
        isBullHornRushRootMotion =
            triggerHash == BullHornRushHash;

        // RunCircleSweep usa StartRunCircleSweep(), no StartAttack().
        isRunCircleSweepRootMotion = false;
        isRunCircleSweepEntering = false;

        animator.SetBool(BlockingHash, false);
        animator.SetBool(WalkingBackwardHash, false);
        animator.SetBool(CrouchingHash, false);
        animator.SetFloat(SpeedHash, 0f);
        animator.SetTrigger(triggerHash);

        StartCoroutine(AttackLockRoutine());
    }


    private IEnumerator AttackLockRoutine()
    {
        float waitForEnter = 0f;
        const float enterTimeout = 0.75f;
        bool enteredAttackState = false;

        while (waitForEnter < enterTimeout)
        {
            AnimatorStateInfo current =
                animator.GetCurrentAnimatorStateInfo(0);

            bool currentIsAttack =
                current.IsTag("Attack");

            bool nextIsAttack = false;

            if (animator.IsInTransition(0))
            {
                AnimatorStateInfo next =
                    animator.GetNextAnimatorStateInfo(0);

                nextIsAttack =
                    next.IsTag("Attack");
            }

            if (currentIsAttack || nextIsAttack)
            {
                enteredAttackState = true;
                break;
            }

            waitForEnter += Time.deltaTime;
            yield return null;
        }

        if (!enteredAttackState)
        {
            Debug.LogWarning(
                "El ataque no entro a un estado con Tag = Attack. " +
                "Revisa el Tag del estado correspondiente en WolfAnimator."
            );

            isAttacking = false;
            isSpecialAttacking = false;
            isCombo2RootMotion = false;
            isForwardCircleKickRootMotion = false;
            isRunCircleSweepRootMotion = false;
            isRunCircleSweepEntering = false;
            isBullHornRushRootMotion = false;
            ResetSquareFollowUpWindow();
            ResetTriangleFollowUpWindow();
            ResetXFollowUpWindow();
            yield break;
        }

        while (true)
        {
            AnimatorStateInfo current =
                animator.GetCurrentAnimatorStateInfo(0);

            bool currentIsAttack =
                current.IsTag("Attack");

            bool nextIsAttack = false;

            if (animator.IsInTransition(0))
            {
                AnimatorStateInfo next =
                    animator.GetNextAnimatorStateInfo(0);

                nextIsAttack =
                    next.IsTag("Attack");
            }

            if (!currentIsAttack && !nextIsAttack)
            {
                break;
            }

            yield return null;
        }

        isAttacking = false;
        isSpecialAttacking = false;
        isCombo2RootMotion = false;
        isForwardCircleKickRootMotion = false;
        isRunCircleSweepRootMotion = false;
        isRunCircleSweepEntering = false;
        isBullHornRushRootMotion = false;
        ResetSquareFollowUpWindow();
        ResetTriangleFollowUpWindow();
        ResetXFollowUpWindow();
        verticalVelocity = -2f;
    }


    // =========================================================
    // STATIONARY GRAVITY
    // =========================================================

    private void ApplyStationaryGravity()
    {
        if (controller.isGrounded &&
            verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        verticalVelocity +=
            gravity * Time.deltaTime;

        float zCorrection =
            (laneZ - transform.position.z) *
            laneLockSpeed *
            Time.deltaTime;

        Vector3 motion =
            new Vector3(
                0f,
                verticalVelocity * Time.deltaTime,
                zCorrection
            );

        controller.Move(motion);
    }


    // =========================================================
    // R1 BACKFLIP
    // =========================================================

    private void StartBackflip()
    {
        ResetSquareFollowUpWindow();
        ResetTriangleFollowUpWindow();
        ResetXFollowUpWindow();
        ClearCombo2InputBuffer();
        ClearCombo1InputBuffer();

        if (animator == null ||
            isBlocking ||
            isCrouching ||
            isJumping ||
            isBackflipping ||
            isAttacking ||
            isSuperCharging)
        {
            return;
        }

        isBlocking = false;

        animator.SetBool(BlockingHash, false);
        animator.SetBool(WalkingBackwardHash, false);
        animator.SetBool(CrouchingHash, false);
        animator.SetFloat(SpeedHash, 0f);
        animator.SetTrigger(BackflipHash);

        StartCoroutine(BackflipRoutine());
    }


    private IEnumerator BackflipRoutine()
    {
        isBackflipping = true;

        float elapsed = 0f;

        float backwardSign =
            startFacingRight ? -1f : 1f;

        float backflipSpeed =
            backflipDistance /
            Mathf.Max(backflipDuration, 0.01f);

        while (elapsed < backflipDuration)
        {
            float delta = Time.deltaTime;

            float desiredDeltaX =
                backwardSign *
                backflipSpeed *
                delta;

            float desiredX =
                Mathf.Clamp(
                    transform.position.x + desiredDeltaX,
                    minX,
                    maxX
                );

            float deltaX =
                desiredX - transform.position.x;

            if (controller.isGrounded)
            {
                verticalVelocity = -2f;
            }
            else
            {
                verticalVelocity +=
                    gravity * delta;
            }

            float zCorrection =
                (laneZ - transform.position.z) *
                laneLockSpeed *
                delta;

            Vector3 motion =
                new Vector3(
                    deltaX,
                    verticalVelocity * delta,
                    zCorrection
                );

            controller.Move(motion);

            elapsed += delta;

            yield return null;
        }

        isBackflipping = false;
        verticalVelocity = -2f;
    }
}
