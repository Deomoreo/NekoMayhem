using UnityEngine;

public class AssistDebugGizmos : MonoBehaviour
{
    public Transform player;           // drag & drop il Player (root)
    public float coneDegrees = 60f;    // come AssistAngleLight
    public float range = 2.5f;         // come AssistRange

    void OnDrawGizmosSelected()
    {
        if (!player) return;

        // Range
        Gizmos.color = new Color(0f, 1f, 1f, 0.15f);
        Gizmos.DrawWireSphere(player.position, range);

        // Cono frontale
        Vector3 fwd = player.forward;
        Vector3 pos = player.position; pos.y += 1.0f;
        float half = coneDegrees * 0.5f;

        // due bordi del cono sul piano XZ
        Quaternion qL = Quaternion.AngleAxis(-half, Vector3.up);
        Quaternion qR = Quaternion.AngleAxis(+half, Vector3.up);
        Vector3 left = qL * fwd;
        Vector3 right = qR * fwd;

        Gizmos.color = new Color(0f, 0.8f, 1f, 0.9f);
        Gizmos.DrawLine(pos, pos + left * range);
        Gizmos.DrawLine(pos, pos + right * range);

        // raggiera
        int rays = 8;
        for (int i = 0; i <= rays; i++)
        {
            float t = Mathf.Lerp(-half, half, i / (float)rays);
            Vector3 dir = Quaternion.AngleAxis(t, Vector3.up) * fwd;
            Gizmos.DrawLine(pos, pos + dir * range);
        }
    }
}
