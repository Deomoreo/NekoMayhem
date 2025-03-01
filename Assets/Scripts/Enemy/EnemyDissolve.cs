using System.Collections;
using UnityEngine;

public class EnemyDissolve : MonoBehaviour
{
    public SkinnedMeshRenderer skinnedMesh;
    public Material[] dissolveMaterialTemplate;
    public float totalDissolveTime = 2f;
    public float refreshRate = 0.2f;

    void Start()
    {
        if (skinnedMesh != null)
            dissolveMaterialTemplate = skinnedMesh.materials;
    }
    public void StartDissolve()
    {
        StartCoroutine(DissolveRoutine());
    }

    private IEnumerator DissolveRoutine()
    {
        float timer = 0f;
        while (timer < totalDissolveTime)
        {
            timer += Time.deltaTime;
            float dissolveValue = Mathf.Clamp01(timer / totalDissolveTime);
            for (int i = 0; i < dissolveMaterialTemplate.Length; i++)
            {
                dissolveMaterialTemplate[i].SetFloat("_DissolveAmount", dissolveValue);
            }
            yield return new WaitForSeconds(refreshRate);
        }
    }
}
