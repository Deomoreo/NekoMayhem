using UnityEngine;

public class UpgradeTester : MonoBehaviour
{
    public PlayerStatUpgrade forzaUpgrade;
    public WeaponUpgrade spadaUpgrade;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.U))
        {
            Debug.Log("Applicando upgrade Forza...");
            forzaUpgrade.ApplyUpgrade();
        }

        if (Input.GetKeyDown(KeyCode.I))
        {
            Debug.Log("Applicando upgrade Spada...");
            spadaUpgrade.ApplyUpgrade();
        }
    }
}
