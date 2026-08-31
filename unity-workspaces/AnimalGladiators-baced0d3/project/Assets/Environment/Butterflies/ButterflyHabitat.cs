using UnityEngine;

public class ButterflyHabitat : MonoBehaviour
{
    public Transform[] airPoints;
    public Transform[] perchPoints;

    public Transform GetRandomAirPoint()
    {
        if (airPoints == null || airPoints.Length == 0)
            return null;

        return airPoints[
            Random.Range(0, airPoints.Length)
        ];
    }

    public Transform GetRandomPerchPoint()
    {
        if (perchPoints == null || perchPoints.Length == 0)
            return null;

        return perchPoints[
            Random.Range(0, perchPoints.Length)
        ];
    }
}
