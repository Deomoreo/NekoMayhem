using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class CatTransformation : MonoBehaviour
{
    public Transform activeModelFollower;  
    public Transform playerTransform;      

    private CatInputActions controls;
    public bool isQuadrupede = false;     

    [Header("Movement Controllers")]
    public MonoBehaviour bipedeController;
    public MonoBehaviour quadrupedeController;

    [Header("Player Models")]
    public GameObject bipedeModel;
    public GameObject quadrupedeModel;

    [Header("Visual Effects")]
    public ParticleSystem transformationEffect;

    [Header("Transformation Settings")]
    public float transformationDelay = 0.5f;  
    public float transformationCooldown = 1f;

    [Header("Sword Trail")]
    public SwordTrailController swordTrailController;

    private bool canTransform = true;         

    void Awake()
    {
        controls = new CatInputActions();
        if (playerTransform == null)
            Debug.LogError("Assegna il playerTransform in CatTransformation!");

        if (bipedeModel != null)
        {
            bipedeModel.transform.localPosition = Vector3.zero;
            bipedeModel.transform.localRotation = Quaternion.identity;
            bipedeModel.SetActive(true);
        }
        if (quadrupedeModel != null)
        {
            quadrupedeModel.transform.localPosition = Vector3.zero;
            quadrupedeModel.transform.localRotation = Quaternion.identity;
            quadrupedeModel.SetActive(false);
        }

        if (bipedeController != null)
            bipedeController.enabled = true;
        if (quadrupedeController != null)
            quadrupedeController.enabled = false;

        if (transformationEffect != null)
        {
            transformationEffect.transform.SetParent(activeModelFollower, false);
            transformationEffect.transform.localPosition = Vector3.zero;
            transformationEffect.Stop();
        }
    }

    void OnEnable()
    {
        controls.Enable();
        controls.Transformation.Newaction.performed += OnTransformPerformed;
    }

    void OnDisable()
    {
        controls.Transformation.Newaction.performed -= OnTransformPerformed;
        controls.Disable();
    }

    void Update()
    {
        if (transformationEffect != null && transformationEffect.isPlaying)
        {
            transformationEffect.transform.position = activeModelFollower.position;
        }
    }

    void OnTransformPerformed(InputAction.CallbackContext context)
    {
        if (!canTransform)
            return;

        StartCoroutine(PerformTransformation());
    }

    IEnumerator PerformTransformation()
    {
        canTransform = false;
        if (swordTrailController != null)
            swordTrailController.isTransforming = true;

        if (transformationEffect != null)
        {
            transformationEffect.transform.position = activeModelFollower.position;
            transformationEffect.Play();
        }

        yield return new WaitForSeconds(transformationDelay);

        Vector3 recordedPosition = activeModelFollower.position;
        Quaternion recordedRotation = activeModelFollower.rotation;

        GameObject currentModel = isQuadrupede ? quadrupedeModel : bipedeModel;
        GameObject targetModel = isQuadrupede ? bipedeModel : quadrupedeModel;

        if (currentModel != null)
            currentModel.SetActive(false);

        if (targetModel != null)
        {
            targetModel.SetActive(true);
            targetModel.transform.SetPositionAndRotation(recordedPosition, recordedRotation);
        }

        if (!isQuadrupede)
        {
            if (bipedeController != null) bipedeController.enabled = false;
            if (quadrupedeController != null) quadrupedeController.enabled = true;
            isQuadrupede = true;
            Debug.Log("Trasformazione in Quadrupede attivata.");
        }
        else
        {
            if (quadrupedeController != null) quadrupedeController.enabled = false;
            if (bipedeController != null) bipedeController.enabled = true;
            isQuadrupede = false;
            swordTrailController.StopTrail();
            Debug.Log("Trasformazione in Bipede attivata.");
        }

        if (transformationEffect != null)
            transformationEffect.Stop();

        if (swordTrailController != null)
            swordTrailController.isTransforming = false;

        yield return new WaitForSeconds(transformationCooldown);
        canTransform = true;
    }
}
