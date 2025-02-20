using UnityEngine;

public class SwordTrailController : MonoBehaviour
{
    public ParticleSystem swordTrail;

    [HideInInspector]
    public bool isTransforming = false;

    private void Start()
    {
        if (swordTrail != null && swordTrail.isPlaying)
        {
            swordTrail.Stop();
        }
    }

    public void StartTrail()
    {
        if (!isTransforming && swordTrail != null && !swordTrail.isPlaying)
        {
            swordTrail.Play();
        }
    }

    public void StopTrail()
    {
        if (swordTrail != null && swordTrail.isPlaying)
        {
            swordTrail.Stop();
        }
    }
}
