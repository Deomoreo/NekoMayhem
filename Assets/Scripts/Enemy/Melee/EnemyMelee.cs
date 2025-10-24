using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyMelee : MonoBehaviour
{
    public enum State { Idle, Chase, Telegraph, Attack, Recover }

    [Header("Riferimenti")]
    public Transform player;
    public LayerMask obstructionMask;
    public LayerMask playerLayer;

    [Header("Movimento")]
    public bool useNavMesh = true;
    public float desiredDistance = 2.0f;
    public float chaseSpeed = 3.0f;
    public float strafeSpeed = 1.5f;
    public float rotationSpeed = 540f;
    public float stopChaseRange = 12.0f;

    [Header("Ingaggio / Percezione")]
    public float aggroRange = 10.0f;
    public float losOriginHeight = 1.2f;

    [Header("Attacco (telegraph → hit → recover)")]
    public float telegraphTime = 0.35f;      // anticipo leggibile
    public float attackActiveTime = 0.18f;   // finestra hitbox attiva
    public float recoverTime = 0.40f;        // coda
    public float attackRange = 1.7f;
    public float attackConeDegrees = 120f;
    public float attackStepIn = 0.6f;        // micro-avanzo durante l'hit
    public float attackStepSpeed = 6.5f;

    [Header("Cooldown")]
    public float attackCooldownSeconds = 0.55f;    // cd “pieno”
    [Tooltip("Il cd scorre solo quando il player è in una zona di ingaggio: distanza ≤ attackRange+buffer, cono e LOS ok.")]
    public float cooldownGateExtraRange = 0.6f;    // buffer oltre l’attackRange
    public float cooldownWhileGatedMul = 1.0f;     // 1.0 = pieno; <1 = più lento
    public float cooldownDecayWhenUngated = 0.0f;  // opzionale: fa “risalire” (negativo) o “restare fermo” (0)
    [Tooltip("Se durante Telegraph il player rimane fuori dalla zona per più di così, cancelliamo l’attacco.")]
    public float telegraphGrace = 0.18f;

    [Header("Danno")]
    public float damage = 12f;
    public float poiseDamage = 20f;
    public float hitstunSeconds = 0.20f;
    public float knockbackForce = 3.0f;

    [Header("Hitbox geometria")]
    public float hitboxRadius = 0.35f;
    public float hitboxLength = 0.8f;
    public Vector3 hitboxLocalOffset = new Vector3(0f, 1.0f, 0.4f);

    [Header("Strafe / comportamento corto")]
    public float strafeChangeDirEvery = 1.0f;
    public float strafeMaxAngleOffset = 40f;

    [Header("Re-ingaggio dopo LOS persa")]
    [Tooltip("Dopo aver riacquisito il player, per questo tempo non facciamo subito ‘retrocedi se troppo vicino’: strafe invece di arretrare.")]
    public float freshChaseBackoffGrace = 0.75f;

    [Header("Debug")]
    public bool debugLogs = true;
    public bool drawGizmos = true;

    // internals
    State _state = State.Idle;
    float _stateEnterTime = 0f;
    CharacterController _cc;
    NavMeshAgent _agent;

    // Cooldown “gated”
    float _cooldownProgress = 1f; // 1 = pronto ad attaccare; 0 = appena uscito dal colpo
    float _lastGatedCheckAt = -999f;

    // gating tracker per Telegraph
    float _outOfGateSince = -1f;

    // strafe
    float _strafeTimer = 0f;
    int _strafeSign = 1;

    // ri-aggancio dopo LOS
    float _freshChaseUntil = -1f;
    float _lastHadLOSAt = -999f;

    // hitbox dedup
    readonly HashSet<Transform> _alreadyHit = new HashSet<Transform>();

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _agent = GetComponent<NavMeshAgent>();

        // Planner-only agent: muoviamo col CharacterController
        _agent.updatePosition = false;
        _agent.updateRotation = false;
        _agent.speed = chaseSpeed;
        _agent.acceleration = 40f;
    }

    void OnEnable()
    {
        if (useNavMesh) EnsureOnNavMesh();
        ChangeState(State.Idle);
        _cooldownProgress = 1f; // appena spawna è pronto (se in gate)
    }

    void Update()
    {
        if (!player)
        {
            if (debugLogs) Debug.LogWarning($"[Enemy] {name}: Player non assegnato → Idle.");
            ChangeState(State.Idle);
            return;
        }

        // aggiorna gating cooldown OGNI FRAME
        UpdateCooldownGated();

        switch (_state)
        {
            case State.Idle: TickIdle(); break;
            case State.Chase: TickChase(); break;
            case State.Telegraph: TickTelegraph(); break;
            case State.Attack: TickAttack(); break;
            case State.Recover: TickRecover(); break;
        }
    }

    // ===================== COOLDOWN GATED =====================

    void ResetCooldown() => _cooldownProgress = 0f;

    bool CooldownReady() => _cooldownProgress >= 1f;

    void UpdateCooldownGated()
    {
        bool gated = InAttackGate(); // player entro range+buffer, cono e LOS ok
        float dt = Time.deltaTime;

        if (gated)
        {
            _cooldownProgress += (dt / Mathf.Max(0.0001f, attackCooldownSeconds)) * Mathf.Max(0.01f, cooldownWhileGatedMul);
            if (_cooldownProgress > 1f) _cooldownProgress = 1f;
            _lastGatedCheckAt = Time.time;
        }
        else
        {
            if (cooldownDecayWhenUngated != 0f)
            {
                _cooldownProgress = Mathf.Clamp01(_cooldownProgress + cooldownDecayWhenUngated * dt);
            }
        }

        // tracking per telegraph grace
        if (gated)
            _outOfGateSince = -1f;
        else
        {
            if (_outOfGateSince < 0f) _outOfGateSince = Time.time;
        }
    }

    bool InAttackGate()
    {
        if (!player) return false;
        if (!HasLOS()) return false;

        Vector3 to = player.position - transform.position; to.y = 0f;
        float dist = to.magnitude;

        // distanza entro (attackRange + buffer)
        if (dist > (attackRange + Mathf.Max(0f, cooldownGateExtraRange))) return false;

        // entro il cono frontale
        float ang = Vector3.Angle(transform.forward, to.sqrMagnitude > 0.0001f ? to.normalized : transform.forward);
        if (ang > attackConeDegrees * 0.5f) return false;

        return true;
    }

    // ===================== STATES =====================

    void ChangeState(State s)
    {
        if (_state == s) return;
        _state = s;
        _stateEnterTime = Time.time;
        if (debugLogs) Debug.Log($"<b>[Enemy]</b> {name} → {_state}");

        // reset path quando non siamo in chase
        if (s == State.Telegraph || s == State.Attack || s == State.Recover)
            if (_agent) _agent.ResetPath();

        if (s == State.Attack) _alreadyHit.Clear();
        if (s == State.Chase) _freshChaseUntil = Time.time + freshChaseBackoffGrace;
    }

    void TickIdle()
    {
        if (CanSeeAndAggroPlayer())
        {
            ChangeState(State.Chase);
            return;
        }
        // opzionale: patrol
    }

    void TickChase()
    {
        Vector3 to = player.position - transform.position; to.y = 0f;
        float dist = to.magnitude;

        // traccia LOS
        bool nowLOS = HasLOS();
        if (nowLOS) _lastHadLOSAt = Time.time;

        // facing
        FaceTowards(player.position);

        // movimento
        if (useNavMesh && AgentReady())
        {
            _agent.speed = chaseSpeed;
            _agent.SetDestination(player.position);

            Vector3 v = _agent.desiredVelocity; v.y = 0f;

            // Mantieni distanza target — con GRACE all’ingaggio per evitare l'arretra subito
            bool inGrace = Time.time <= _freshChaseUntil;

            if (dist > desiredDistance + 0.2f)
            {
                MoveDelta(v.sqrMagnitude > 0.0001f ? v.normalized * (chaseSpeed * Time.deltaTime) : transform.forward * (chaseSpeed * Time.deltaTime));
            }
            else if (dist < desiredDistance - 0.2f)
            {
                if (inGrace)
                {
                    DoStrafeAround(player.position); // niente backoff immediato
                }
                else
                {
                    MoveDelta(-transform.forward * (chaseSpeed * 0.8f * Time.deltaTime));
                }
            }
            else
            {
                DoStrafeAround(player.position);
            }
        }
        else
        {
            // fallback senza navmesh
            if (dist > desiredDistance + 0.2f)
                MoveTowards(player.position, chaseSpeed);
            else if (dist < desiredDistance - 0.2f)
            {
                if (Time.time <= _freshChaseUntil) DoStrafeAround(player.position);
                else MoveDelta(-transform.forward * (chaseSpeed * 0.8f * Time.deltaTime));
            }
            else
                DoStrafeAround(player.position);
        }

        // entra in telegraph solo se:
        // - cooldown è PRONTO
        // - sei dentro la GATE (vicino+cono+LOS)
        if (CooldownReady() && InAttackGate())
        {
            ChangeState(State.Telegraph);
            return;
        }

        // troppo lontano? torna Idle
        if (dist > stopChaseRange)
        {
            if (debugLogs) Debug.Log($"[Enemy] {name} esce da Chase: dist {dist:0.0} > stopChaseRange {stopChaseRange}");
            ChangeState(State.Idle);
        }
    }

    void TickTelegraph()
    {
        FaceTowards(player.position);

        // se esci dalla gate per più di una “grace”, annulla il colpo
        if (_outOfGateSince > 0f && (Time.time - _outOfGateSince) >= telegraphGrace)
        {
            if (debugLogs) Debug.Log($"[Enemy] {name} Telegraph cancellato (fuori gate per {Time.time - _outOfGateSince:0.00}s).");
            ChangeState(State.Chase);
            return;
        }

        float t = Time.time - _stateEnterTime;
        if (t >= telegraphTime)
        {
            // safety: parti solo se ANCORA in gate
            if (InAttackGate())
                ChangeState(State.Attack);
            else
                ChangeState(State.Chase);
            return;
        }
    }

    void TickAttack()
    {
        float elapsed = Time.time - _stateEnterTime;

        // se durante l'attacco perdi la gate troppo a lungo, whiff-cancel
        if (_outOfGateSince > 0f && (Time.time - _outOfGateSince) >= telegraphGrace)
        {
            if (debugLogs) Debug.Log($"[Enemy] {name} Attack cancellato (fuori gate).");
            ChangeState(State.Recover);
            return;
        }

        // Step-in morbido durante la finestra
        Vector3 forward = (player.position - transform.position); forward.y = 0f;
        if (forward.sqrMagnitude > 0.0001f)
        {
            forward.Normalize();
            float step = Mathf.Min(attackStepIn, attackStepSpeed * Time.deltaTime);
            if (step > 0f) MoveDelta(forward * step);
            FaceTowards(player.position);
        }

        if (elapsed <= attackActiveTime)
        {
            DoMeleeHitbox();
        }
        else
        {
            ChangeState(State.Recover);
        }
    }

    void TickRecover()
    {
        float t = Time.time - _stateEnterTime;
        if (t >= recoverTime)
        {
            // al termine del recover, azzera il cooldown e lascia che riparta solo quando sei in gate
            ResetCooldown();

            // decidi successivo
            if (CanSeeAndAggroPlayer()) ChangeState(State.Chase);
            else ChangeState(State.Idle);
        }
    }

    // ===================== MOVIMENTO =====================

    bool AgentReady()
    {
        if (!useNavMesh || _agent == null) return false;
        if (!_agent.isOnNavMesh)
        {
            if (debugLogs) Debug.LogWarning($"[Enemy] {name}: NavMeshAgent NON su NavMesh. Provo warp…");
            EnsureOnNavMesh();
            return _agent.isOnNavMesh;
        }
        return true;
    }

    void EnsureOnNavMesh()
    {
        if (_agent == null) return;
        if (_agent.isOnNavMesh) return;

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
        {
            _agent.Warp(hit.position);
            if (debugLogs) Debug.Log($"[Enemy] {name}: Warp su NavMesh @ {hit.position}");
        }
        else
        {
            if (debugLogs) Debug.LogWarning($"[Enemy] {name}: Nessun NavMesh vicino (r=2m). Disabilito useNavMesh.");
            useNavMesh = false;
        }
    }

    void MoveTowards(Vector3 target, float speed)
    {
        Vector3 dir = (target - transform.position); dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
        {
            dir.Normalize();
            MoveDelta(dir * (speed * Time.deltaTime));
            FaceTowards(target);
        }
    }

    void MoveDelta(Vector3 delta)
    {
        if (_cc && _cc.enabled) _cc.Move(delta);
        else transform.position += delta;
    }

    void DoStrafeAround(Vector3 center)
    {
        _strafeTimer -= Time.deltaTime;
        if (_strafeTimer <= 0f)
        {
            _strafeSign = (Random.value < 0.5f) ? -1 : 1;
            _strafeTimer = strafeChangeDirEvery;
        }

        Vector3 to = center - transform.position; to.y = 0f;
        if (to.sqrMagnitude < 0.0001f) return;

        Vector3 right = new Vector3(to.z, 0f, -to.x).normalized * _strafeSign;
        MoveDelta(right * (strafeSpeed * Time.deltaTime));

        Quaternion look = Quaternion.LookRotation(Vector3.Slerp(to.normalized, right, strafeMaxAngleOffset / 90f));
        transform.rotation = Quaternion.RotateTowards(transform.rotation, look, rotationSpeed * Time.deltaTime);
    }

    void FaceTowards(Vector3 worldPos)
    {
        Vector3 to = worldPos - transform.position; to.y = 0f;
        if (to.sqrMagnitude < 0.0001f) return;
        Quaternion look = Quaternion.LookRotation(to.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, look, rotationSpeed * Time.deltaTime);
    }

    bool InFrontCone(Vector3 targetPos, float coneDeg)
    {
        Vector3 to = targetPos - transform.position; to.y = 0f;
        if (to.sqrMagnitude < 0.0001f) return true;
        float ang = Vector3.Angle(transform.forward, to.normalized);
        return ang <= coneDeg * 0.5f;
    }

    // ===================== SENSE / LOS =====================

    bool CanSeeAndAggroPlayer()
    {
        if (!player) return false;
        Vector3 to = player.position - transform.position; to.y = 0f;
        if (to.magnitude > aggroRange) return false;
        return HasLOS();
    }

    bool HasLOS()
    {
        if (!player) return false;
        Vector3 origin = transform.position + Vector3.up * losOriginHeight;
        Vector3 target = player.position + Vector3.up * 1.0f;
        Vector3 dir = (target - origin);
        float dist = dir.magnitude;
        if (dist <= 0.0001f) return true;

        bool blocked = Physics.Raycast(origin, dir.normalized, dist, obstructionMask, QueryTriggerInteraction.Ignore);
        bool has = !blocked;
        if (has) _lastHadLOSAt = Time.time;
        return has;
    }

    // ===================== MELEE HITBOX =====================

    void DoMeleeHitbox()
    {
        Vector3 basePos = transform.TransformPoint(hitboxLocalOffset);
        Vector3 forward = transform.forward;
        Vector3 p1 = basePos - forward * (hitboxLength * 0.5f);
        Vector3 p2 = basePos + forward * (hitboxLength * 0.5f);

        Collider[] hits = Physics.OverlapCapsule(p1, p2, hitboxRadius, playerLayer, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            Transform t = h.transform;
            if (_alreadyHit.Contains(t)) continue;
            _alreadyHit.Add(t);

            Damageable dmg = t.GetComponentInParent<Damageable>();
            if (!dmg) dmg = t.GetComponent<Damageable>();

            if (dmg)
            {
                Vector3 dir = (t.position - transform.position); dir.y = 0f;
                dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;

                dmg.ApplyHit(damage, poiseDamage, hitstunSeconds, dir);

                Rigidbody rb = t.GetComponent<Rigidbody>();
                if (rb) rb.AddForce(dir * knockbackForce, ForceMode.VelocityChange);
            }
            else
            {
                if (debugLogs) Debug.Log($"[Enemy] Hit {t.name} ma nessun Damageable trovato.");
            }
        }
    }

    // ===================== GIZMOS =====================

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        // Aggro
        Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, aggroRange);

        // Desired distance
        Gizmos.color = new Color(0.2f, 0.9f, 0.2f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, desiredDistance);

        // Hitbox preview
        Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
        Vector3 basePos = transform.TransformPoint(hitboxLocalOffset);
        Vector3 forward = transform.forward;
        Vector3 p1 = basePos - forward * (hitboxLength * 0.5f);
        Vector3 p2 = basePos + forward * (hitboxLength * 0.5f);
        DrawCapsuleGizmo(p1, p2, hitboxRadius);

        // Gate range
        Gizmos.color = new Color(0.1f, 0.6f, 1f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, attackRange + Mathf.Max(0f, cooldownGateExtraRange));
    }

    static void DrawCapsuleGizmo(Vector3 p1, Vector3 p2, float r, int seg = 12)
    {
        Vector3 up = (p2 - p1).normalized;
        Vector3 right = Vector3.Cross(up, Vector3.up);
        if (right == Vector3.zero) right = Vector3.Cross(up, Vector3.right);
        right.Normalize();
        Vector3 forward = Vector3.Cross(right, up).normalized;

        for (int i = 0; i < seg; i++)
        {
            float a0 = (i / (float)seg) * Mathf.PI * 2f;
            float a1 = ((i + 1) / (float)seg) * Mathf.PI * 2f;
            Vector3 off0 = right * Mathf.Cos(a0) * r + forward * Mathf.Sin(a0) * r;
            Vector3 off1 = right * Mathf.Cos(a1) * r + forward * Mathf.Sin(a1) * r;
            Gizmos.DrawLine(p1 + off0, p1 + off1);
            Gizmos.DrawLine(p2 + off0, p2 + off1);
            Gizmos.DrawLine(p1 + off0, p2 + off0);
        }
    }
}
