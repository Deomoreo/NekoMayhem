using UnityEngine;

public class ActiveModelFollow : MonoBehaviour
{
    public GameObject bipedeModel;
    public GameObject quadrupedeModel;

    public float followSpeed = 20f;

    private GameObject GetActiveModel()
    {
        if (bipedeModel != null && bipedeModel.activeSelf)
            return bipedeModel;
        if (quadrupedeModel != null && quadrupedeModel.activeSelf)
            return quadrupedeModel;
        return null;
    }

    void LateUpdate()
    {
        GameObject activeModel = GetActiveModel();
        if (activeModel != null)
        {
            transform.position = Vector3.Lerp(transform.position, activeModel.transform.position, Time.deltaTime * followSpeed);
            transform.rotation = Quaternion.Lerp(transform.rotation, activeModel.transform.rotation, Time.deltaTime * followSpeed);
        }
    }
}
