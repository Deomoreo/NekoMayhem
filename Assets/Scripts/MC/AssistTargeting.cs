using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Transform))]
public class AssistTargeting : MonoBehaviour
{
    [Header("Riferimenti")]
    public Transform player;
    public LayerMask enemyLayer = ~0;

    [Header("Ricerca Assist")]
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

    [Header("QUANDO girare verso il target")]
    public bool faceOnlyWhenInRangeOrAssist = true;
    public float attackFaceRange = 1.6f;

    [Header("Visibilità frazionaria")]
    public LayerMask obstructionMask;
    public float losOriginHeight = 1.1f;
    public int visSamplesVertical = 3;
    public int visSamplesHorizontal = 3;
    [Range(0f, 1f)] public float visibilityFractionThreshold = 0.8f;

    [Header("NavMesh (opzionale)")]
    public bool useNavMeshReachabilityCheck = false;
    public int navMeshAreaMask = NavMesh.AllAreas;

    [Header("Collisione durante dash (safety)")]
    public float capsuleRadius = 0.25f;
    public float capsuleHeight = 1.8f;
    public float dashCollisionBuffer = 0.04f;

    // Stato facing
    private bool _attackFacingActive;
    private Transform _attackFacingTarget;
    private Quaternion _desiredFacing;

    public Quaternion LastLockedRotation { get; private set; } = Quaternion.identity;
    public bool IsFacingActive => _attackFacingActive;

    [Header("Debug")]
    public bool drawRays = false;

    // ---------------- API ----------------

    public Transform AcquireAssistTarget()
    {
        if (player == null) return null;
        Collider[] hits = Physics.OverlapSphere(player.position, range, enemyLayer, QueryTriggerInteraction.Ignore);
        Transform best = null;
        float bestScore = float.NegativeInfinity;
        float half = coneDegrees * 0.5f;

        foreach (var c in hits)
        {
            Transform t = c.transform;
            if (t == player) continue;

            if (!IsEligibleForAssist(t, out float dist, out float absAngle, out float visFrac)) continue;

            float distTerm = 1f - Mathf.Clamp01(dist / range);
            float angleTerm = 1f - Mathf.Clamp01(absAngle / half);
            float visTerm = Mathf.Clamp01(visFrac);
            float score = 0.55f * distTerm + 0.30f * angleTerm + 0.15f * visTerm;
            if (score > bestScore) { bestScore = score; best = t; }
        }
        return best;
    }

    public bool IsEligibleForAssist(Transform t) => IsEligibleForAssist(t, out _, out _, out _);

    private bool IsEligibleForAssist(Transform t, out float dist, out float absAngle, out float visFrac)
    {
        dist = 0f; absAngle = 999f; visFrac = 0f;
        if (!t || !player) return false;

        Vector3 pPos = player.position;
        Vector3 tPos = t.position;
        dist = Vector3.Distance(pPos, tPos);
        if (dist <= minNoAssistDistance || dist > range) return false;

        Vector3 toXZ = new Vector3(tPos.x - pPos.x, 0f, tPos.z - pPos.z);
        Vector3 fwdXZ = new Vector3(player.forward.x, 0f, player.forward.z);
        absAngle = Mathf.Abs(Vector3.SignedAngle(fwdXZ.normalized, toXZ.normalized, Vector3.up));
        if (absAngle > coneDegrees * 0.5f) return false;

        visFrac = ComputeVisibilityFraction(t);
        if (visFrac < visibilityFractionThreshold) return false;

        if (useNavMeshReachabilityCheck && !IsTargetReachableOnNavMesh(t)) return false;
        return true;
    }

    public bool WithinAttackRange(Transform t)
    {
        if (!t || !player) return false;
        if (Vector3.Distance(player.position, t.position) > attackFaceRange) return false;
        float vis = ComputeVisibilityFraction(t);
        return vis >= visibilityFractionThreshold;
    }

    public bool ShouldFaceNow(Transform t)
    {
        if (!faceOnlyWhenInRangeOrAssist) return true;
        return WithinAttackRange(t) || IsEligibleForAssist(t);
    }

    public IEnumerator DoAssistCoroutine(Transform target)
    {
        if (player == null || target == null) yield break;
        if (!IsEligibleForAssist(target)) yield break;

        if (faceTargetDuringAttack) StartAttackFacing(target);

        float elapsed = 0f;
        while (elapsed < maxDashTime)
        {
            if (target == null) break;
            if (ComputeVisibilityFraction(target) < visibilityFractionThreshold) break;

            Vector3 pPos = player.position;
            Vector3 to = target.position - pPos;
            float dist = to.magnitude;
            if (dist <= stopDistance) break;

            Vector3 dir = to.normalized;
            float step = dashSpeed * Time.deltaTime;

            if (CapsuleHitAhead(pPos, dir, step + dashCollisionBuffer, out float hitDist))
            {
                float allowed = Mathf.Max(0f, hitDist - dashCollisionBuffer);
                player.position = pPos + dir * allowed;
                break;
            }
            else
            {
                Vector3 next = pPos + dir * step; next.y = pPos.y;
                player.position = next;
            }

            if (_attackFacingActive)
            {
                Vector3 toFlat = target.position - player.position; toFlat.y = 0f;
                if (toFlat.sqrMagnitude > 0.0001f)
                {
                    _desiredFacing = Quaternion.LookRotation(toFlat.normalized, Vector3.up);
                    LastLockedRotation = _desiredFacing;
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        EndAttackFacing(applyFinalFacing: true);
    }

    public void StartAttackFacing(Transform target)
    {
        if (!faceTargetDuringAttack || player == null || target == null) return;
        if (!ShouldFaceNow(target)) return;

        _attackFacingTarget = target;
        Vector3 to = target.position - player.position; to.y = 0f;
        if (to.sqrMagnitude > 0.0001f)
        {
            _desiredFacing = Quaternion.LookRotation(to.normalized, Vector3.up);
            LastLockedRotation = _desiredFacing;
            player.rotation = _desiredFacing;
            _attackFacingActive = true;
        }
    }

    public void EndAttackFacing(bool applyFinalFacing)
    {
        if (_attackFacingActive && applyFinalFacing && player != null)
            player.rotation = LastLockedRotation;

        _attackFacingActive = false;
        _attackFacingTarget = null;
    }

    public void FullReleaseFacing()
    {
        _attackFacingActive = false;
        _attackFacingTarget = null;
    }

    // -------- Visibilità frazionaria --------
    public float ComputeVisibilityFraction(Transform target)
    {
        if (!target || !player) return 0f;

        Collider col = target.GetComponentInChildren<Collider>();
        if (!col)
            return RayVisible(player.position + Vector3.up * losOriginHeight, target.position + Vector3.up * 0.9f) ? 1f : 0f;

        Bounds b = col.bounds;
        int vCount = Mathf.Max(1, visSamplesVertical);
        int hCount = Mathf.Max(1, visSamplesHorizontal);

        Vector3 origin = player.position + Vector3.up * losOriginHeight;
        int visible = 0, total = vCount * hCount;

        for (int iv = 0; iv < vCount; iv++)
        {
            float tv = (vCount == 1) ? 0.5f : iv / (float)(vCount - 1);
            float y = Mathf.Lerp(b.min.y + 0.1f, b.max.y - 0.1f, tv);

            for (int ih = 0; ih < hCount; ih++)
            {
                float th = (hCount == 1) ? 0.5f : ih / (float)(hCount - 1);
                float x = Mathf.Lerp(b.min.x + 0.1f, b.max.x - 0.1f, th);
                float z = Mathf.Lerp(b.min.z + 0.1f, b.max.z - 0.1f, th);
                if (RayVisible(origin, new Vector3(x, y, z))) visible++;
            }
        }
        return Mathf.Clamp01(visible / (float)total);
    }

    private bool RayVisible(Vector3 origin, Vector3 targetPoint)
    {
        Vector3 dir = targetPoint - origin;
        float dist = dir.magnitude;
        if (dist <= Mathf.Epsilon) return true;
        return !Physics.Raycast(origin, dir.normalized, dist, obstructionMask, QueryTriggerInteraction.Ignore);
    }

    // -------- NavMesh --------
    private bool IsTargetReachableOnNavMesh(Transform target)
    {
        if (target == null) return false;
        if (!NavMesh.SamplePosition(player.position, out NavMeshHit fromHit, 1.5f, navMeshAreaMask)) return false;
        if (!NavMesh.SamplePosition(target.position, out NavMeshHit toHit, 1.5f, navMeshAreaMask)) return false;
        var path = new NavMeshPath();
        bool ok = NavMesh.CalculatePath(fromHit.position, toHit.position, navMeshAreaMask, path);
        return ok && path.status == NavMeshPathStatus.PathComplete;
    }

    // -------- Collision safety --------
    private bool CapsuleHitAhead(Vector3 pPos, Vector3 dir, float distance, out float hitDistance)
    {
        float half = Mathf.Max(0f, (capsuleHeight * 0.5f) - capsuleRadius);
        Vector3 p1 = pPos + Vector3.up * capsuleRadius;
        Vector3 p2 = pPos + Vector3.up * (capsuleRadius + half * 2f);
        bool hit = Physics.CapsuleCast(p1, p2, capsuleRadius, dir, out RaycastHit hi, distance, obstructionMask, QueryTriggerInteraction.Ignore);
        hitDistance = hit ? hi.distance : distance;
        return hit;
    }

    // -------- Aggiornamento facing --------
    private void LateUpdate()
    {
        if (!_attackFacingActive || player == null || _attackFacingTarget == null) return;
        if (!ShouldFaceNow(_attackFacingTarget)) { _attackFacingActive = false; _attackFacingTarget = null; return; }

        if (Vector3.Distance(player.position, _attackFacingTarget.position) > faceMaintainRadius) return;

        Vector3 to = _attackFacingTarget.position - player.position; to.y = 0f;
        if (to.sqrMagnitude > 0.0001f)
        {
            _desiredFacing = Quaternion.LookRotation(to.normalized, Vector3.up);
            LastLockedRotation = _desiredFacing;
            player.rotation = Quaternion.Slerp(player.rotation, _desiredFacing, facingSlerpSpeed * Time.deltaTime);
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
        if (attackFaceRange < 0f) attackFaceRange = 0f;
        visSamplesVertical = Mathf.Max(1, visSamplesVertical);
        visSamplesHorizontal = Mathf.Max(1, visSamplesHorizontal);
        capsuleHeight = Mathf.Max(capsuleHeight, capsuleRadius * 2f + 0.01f);
    }
#endif
}
