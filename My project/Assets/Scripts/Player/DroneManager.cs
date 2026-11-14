using UnityEngine;

/// <summary>
/// Savaş sahnesine girildiğinde Hangar'dan seçilen sol ve sağ Destek Droneları'nı oluşturur ve yönetir.
/// </summary>
public class DroneManager : MonoBehaviour
{
    public static DroneManager Instance { get; private set; }

    private DroneCompanion _leftDrone;
    private DroneCompanion _rightDrone;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);
    }

    public static DroneManager EnsureInstance()
    {
        if (Instance != null) return Instance;

        GameObject go = GameObject.Find("DroneManager");
        if (go == null) go = new GameObject("DroneManager");
        Instance = go.GetComponent<DroneManager>();
        if (Instance == null) Instance = go.AddComponent<DroneManager>();
        return Instance;
    }

    /// <summary>
    /// Oyuncunun mevcut uçağına sol ve sağ droneları takar.
    /// </summary>
    public void SpawnDronesForPlayer(Transform playerTransform)
    {
        ClearDrones();

        if (playerTransform == null) return;

        HangarManager hm = HangarManager.EnsureInstance();
        hm.LoadHangarData();

        // 1. Sol Kanat Dronu (Uçak 0.72 ölçeğiyle kusursuz kanat mesafesi)
        if (hm.leftDroneType != DroneCompanion.DroneType.None && hm.IsDroneUnlocked(hm.leftDroneType))
        {
            GameObject lObj = new GameObject("Drone_LeftWing");
            lObj.transform.position = playerTransform.position + new Vector3(-1.18f, -0.16f, 0f);
            _leftDrone = lObj.AddComponent<DroneCompanion>();
            _leftDrone.Initialize(playerTransform, hm.leftDroneType, left: true);
        }

        // 2. Sağ Kanat Dronu
        if (hm.rightDroneType != DroneCompanion.DroneType.None && hm.IsDroneUnlocked(hm.rightDroneType))
        {
            GameObject rObj = new GameObject("Drone_RightWing");
            rObj.transform.position = playerTransform.position + new Vector3(1.18f, -0.16f, 0f);
            _rightDrone = rObj.AddComponent<DroneCompanion>();
            _rightDrone.Initialize(playerTransform, hm.rightDroneType, left: false);
        }
    }

    public void ClearDrones()
    {
        if (_leftDrone != null) Destroy(_leftDrone.gameObject);
        if (_rightDrone != null) Destroy(_rightDrone.gameObject);
    }
}

