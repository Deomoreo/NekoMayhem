using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// AssistTargeting minimal:
/// - Ruota verso il target durante attacco/dash.
/// - Line of Sight + (opz) NavMesh.
/// - A fine attacco IMPOSTA la rotazione finale e RILASCIA subito ogni stato.
[RequireComponent(typeof(Transform))]
public class AssistTargeting : MonoBehaviour
{
    [Header("Refs")]
    public Transform player;
    public LayerMask enemyLayer = ~0;

    [Header("Ricerca")]
    public float coneDegrees = 60f;
    public float range = 2.5f;
    public float minNoAssistDistance = 0.6f;

    [Header("Dash/Assist")]
    public float stopDistance = 0.9f;
    public float dashSpeed = 8f;
    public float maxDashTime = 0.35f;

    [Header("Facing durante attacco")]
    public bool faceTargetDuringAttack = true;
    public float faceMaintainRadius = 3.0f;
    public float facingSlerpSpeed = 20f;

    [Header("LOS / Ostacoli")]
    public LayerMask obstructionMask;
    public float losOriginHeight = 1.1f;
    public float losTargetHeight = 0.9f;

    [Header("NavMesh (opzionale)")]
    public bool useNavMeshReachabilityCheck = false;
    public int navMeshAreaMask = NavMesh.AllAreas;

    // Stato facing (solo mentre si attacca)
    private bool _attackFacingActive;
    private Transform _attackFacingTarget;
    private Quaternion _desiredFacing;

    /// Ultima rotazione buona calcolata verso il target (esposta).
    public Quaternion LastLockedRotation { get; private set; }

    [Header("Debug")]
    public bool drawRays = true;
    public Color rangeColor = new Color(0f, 1f, 1f, 0.15f);
    public Color coneColor = new Color(0f, 0.8f, 1f, 0.9f);

    // ---------------- API ----------------

    public Transform AcquireAssistTarget()
    {
        if (player == null) return null;

        Collider[] hits = Physics.OverlapSphere(player.position, range, enemyLayer);
        Transform best = null;
        float bestScore = float.NegativeInfinity;

        Vector3 playerForward = player.forward;
        Vector3 playerPos = player.position;
        float half = coneDegrees * 0.5f;

        foreach (var c in hits)
        {
            Transform t = c.transform;
            if (t == player) continue;

            Vector3 dir = t.position - playerPos;
            float dist = dir.magnitude;
            if (dist < Mathf.Epsilon) continue;
            if (dist <= minNoAssistDistance) continue;

            Vector3 dirXZ = new Vector3(dir.x, 0f, dir.z);
            Vector3 fwdXZ = new Vector3(playerForward.x, 0f, playerForward.z);
            float absAngle = Mathf.Abs(Vector3.SignedAngle(fwdXZ.normalized, dirXZ.normalized, Vector3.up));
            if (absAngle > half) continue;

            if (!HasLineOfSight(t)) continue;
            if (useNavMeshReachabilityCheck && !IsTargetReachableOnNavMesh(t)) continue;

            float normDist = Mathf.Clamp01(dist / range);
            float normAngle = Mathf.Clamp01(absAngle / half);
            float score = (1f - normDist) * 0.7f + (1f - normAngle) * 0.3f;

            if (score > bestScore) { bestScore = score; best = t; }
        }

        return best;
    }

    public IEnumerator DoAssistCoroutine(Transform target)
    {
        if (player == null || target == null) yield break;
        if (!HasLineOfSight(target)) yield break;
        if (useNavMeshReachabilityCheck && !IsTargetReachableOnNavMesh(target)) yield break;

        if (faceTargetDuringAttack) StartAttackFacing(target);

        float elapsed = 0f;
        while (elapsed < maxDashTime)
        {
            if (target == null) break;
            if (!HasLineOfSight(target)) break;

            Vector3 d = target.position - player.position;
            if (d.magnitude <= stopDistance) break;

            Vector3 next = Vector3.MoveTowards(player.position, target.position, dashSpeed * Time.deltaTime);
            next.y = player.position.y;
            player.position = next;

            if (_attackFacingActive)
            {
                Vector3 to = target.position - player.position; to.y = 0f;
                if (to.sqrMagnitude > 0.0001f)
                {
                    _desiredFacing = Quaternion.LookRotation(to.normalized, Vector3.up);
                    LastLockedRotation = _desiredFacing;
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Fine dash: imposta una volta la rotazione buona e rilascia tutto.
        EndAttackFacing(applyFinalFacing: true);
    }

    public void StartAttackFacing(Transform target)
    {
        if (!faceTargetDuringAttack || player == null || target == null) return;

        _attackFacingTarget = target;
        Vector3 to = target.position - player.position; to.y = 0f;
        if (to.sqrMagnitude > 0.0001f)
        {
            _desiredFacing = Quaternion.LookRotation(to.normalized, Vector3.up);
            LastLockedRotation = _desiredFacing;
            player.rotation = _desiredFacing;
        }
        _attackFacingActive = true;
    }

    /// Imposta (se richiesto) la rotazione finale e DISATTIVA qualsiasi aggiornamento.
    public void EndAttackFacing(bool applyFinalFacing)
    {
        if (applyFinalFacing && player != null) player.rotation = LastLockedRotation;
        _attackFacingActive = false;
        _attackFacingTarget = null;
    }

    /// Killswitch totale (se vuoi forzare il rilascio da fuori).
    public void FullReleaseFacing()
    {
        _attackFacingActive = false;
        _attackFacingTarget = null;
    }

    // ---------------- LOS / NavMesh ----------------

    private bool HasLineOfSight(Transform target)
    {
        if (player == null || target == null) return false;

        Vector3 origin = player.position + Vector3.up * losOriginHeight;
        Vector3 targetPoint = target.position + Vector3.up * losTargetHeight;
        Vector3 dir = targetPoint - origin;
        float dist = dir.magnitude;
        if (dist <= Mathf.Epsilon) return true;

        return !Physics.Raycast(origin, dir.normalized, dist, obstructionMask, QueryTriggerInteraction.Ignore);
    }

    private bool IsTargetReachableOnNavMesh(Transform target)
    {
        if (target == null) return false;
        if (!NavMesh.SamplePosition(player.position, out NavMeshHit fromHit, 1.5f, navMeshAreaMask)) return false;
        if (!NavMesh.SamplePosition(target.position, out NavMeshHit toHit, 1.5f, navMeshAreaMask)) return false;

        var path = new NavMeshPath();
        bool ok = NavMesh.CalculatePath(fromHit.position, toHit.position, navMeshAreaMask, path);
        return ok && path.status == NavMeshPathStatus.PathComplete;
    }

    // ---------------- Update (solo DURANTE l’attacco) ----------------
    private void LateUpdate()
    {
        if (!_attackFacingActive || player == null || _attackFacingTarget == null) return;

        // Mantieni facing SOLO mentre sei in attacco e target è valido+nel raggio+LOS
        if (Vector3.Distance(player.position, _attackFacingTarget.position) > faceMaintainRadius) return;
        if (!HasLineOfSight(_attackFacingTarget)) return;

        Vector3 to = _attackFacingTarget.position - player.position; to.y = 0f;
        if (to.sqrMagnitude > 0.0001f)
        {
            _desiredFacing = Quaternion.LookRotation(to.normalized, Vector3.up);
            LastLockedRotation = _desiredFacing;
            player.rotation = Quaternion.Slerp(player.rotation, _desiredFacing, facingSlerpSpeed * Time.deltaTime);
        }
    }

    // ---------------- Gizmos ----------------
    private void OnDrawGizmosSelected()
    {
        if (player == null) return;
        Gizmos.color = rangeColor;
        Gizmos.DrawWireSphere(player.position, range);

        Vector3 fwd = player.forward;
        Vector3 pos = player.position; pos.y += 1.0f;
        float half = coneDegrees * 0.5f;

        Quaternion qL = Quaternion.AngleAxis(-half, Vector3.up);
        Quaternion qR = Quaternion.AngleAxis(+half, Vector3.up);
        Vector3 left = qL * fwd;
        Vector3 right = qR * fwd;

        Gizmos.color = coneColor;
        Gizmos.DrawLine(pos, pos + left * range);
        Gizmos.DrawLine(pos, pos + right * range);

        int rays = 8;
        for (int i = 0; i <= rays; i++)
        {
            float t = Mathf.Lerp(-half, half, i / (float)rays);
            Vector3 dir = Quaternion.AngleAxis(t, Vector3.up) * fwd;
            Gizmos.DrawLine(pos, pos + dir * range);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (range < 0.1f) range = 0.1f;
        if (coneDegrees < 1f) coneDegrees = 1f;
        if (minNoAssistDistance < 0f) minNoAssistDistance = 0f;
        if (facingSlerpSpeed < 0f) facingSlerpSpeed = 0f;
        if (faceMaintainRadius < 0f) faceMaintainRadius = 0f;
    }
#endif
}
