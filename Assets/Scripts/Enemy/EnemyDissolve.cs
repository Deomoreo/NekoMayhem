using System.Collections;
using UnityEngine;

public class EnemyDissolve : MonoBehaviour
{
    // Materiale template che usa il tuo dissolve shader
    public Material dissolveMaterialTemplate;
    // Durata dell'effetto di dissolvenza in secondi
    public float dissolveDuration = 2f;

    private Renderer rend;
    private Material dissolveMat;

    private void Awake()
    {
        // Usa GetComponentInChildren per prendere il primo Renderer disponibile
        rend = GetComponentInChildren<Renderer>();
        if (rend == null)
        {
            Debug.LogError("Nessun Renderer trovato su " + gameObject.name);
        }
    }

    public void StartDissolve()
    {
        
        dissolveMaterialTemplate.SetFloat("_DissolveAmount", 0f);
        // Assegna il nuovo materiale al Renderer
        rend.material = dissolveMaterialTemplate;

        StartCoroutine(DissolveRoutine());
    }

    private IEnumerator DissolveRoutine()
    {
        float timer = 0f;
        while (timer < dissolveDuration)
        {
            timer += Time.deltaTime;
            float dissolveValue = Mathf.Clamp01(timer / dissolveDuration);
            Debug.Log("Dissolve value: " + dissolveValue);
            dissolveMaterialTemplate.SetFloat("_DissolveAmount", dissolveValue);
            yield return null;
        }
        Destroy(gameObject);
    }
}
