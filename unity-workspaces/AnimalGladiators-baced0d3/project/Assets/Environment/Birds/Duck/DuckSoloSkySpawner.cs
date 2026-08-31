using UnityEngine;

public class DuckSoloSkySpawner : MonoBehaviour
{
    public GameObject duckPrefab;
    public int duckCount = 15;

    void Start()
    {
        if (duckPrefab == null)
            return;

        for (int i = 0; i < duckCount; i++)
        {
            GameObject duck =
                Instantiate(
                    duckPrefab,
                    Vector3.zero,
                    Quaternion.identity,
                    transform
                );

            duck.name =
                "SoloDuck_" + (i + 1).ToString("00");

            if (duck.GetComponent<DuckSoloRandomFlight>() == null)
            {
                duck.AddComponent<DuckSoloRandomFlight>();
            }
        }
    }
}
