using UnityEngine;

public class SoloDuckSkyManagerV2 : MonoBehaviour
{
    [Header("Template")]
    public GameObject duckTemplate;

    [Header("Duck Count")]
    public int duckCount = 15;

    [Header("Visible Sky Width")]
    public float initialLeftX = -6.2f;
    public float initialRightX = 6.2f;

    public float leftEdge = -8.5f;
    public float rightEdge = 8.5f;

    [Header("Different Heights")]
    public float minLocalY = -1.25f;
    public float maxLocalY = 1.25f;

    [Header("Depth")]
    public float minLocalZ = -0.45f;
    public float maxLocalZ = 0.45f;

    [Header("Size")]
    public float minScaleMultiplier = 0.55f;
    public float maxScaleMultiplier = 0.95f;

    [Header("Slow Individual Speeds")]
    public float minSpeed = 0.18f;
    public float maxSpeed = 0.38f;

    [Header("Independent Wing Speed")]
    public float minAnimationSpeed = 0.72f;
    public float maxAnimationSpeed = 1.30f;

    [Header("Random Loop Delay")]
    public float minRespawnDelay = 4f;
    public float maxRespawnDelay = 19f;

    void Start()
    {
        if (duckTemplate == null)
        {
            Debug.LogError(
                "SoloDuckSkyManagerV2: Duck Template is NOT assigned."
            );

            return;
        }

        for (int i = 0; i < duckCount; i++)
        {
            GameObject duck =
                Instantiate(
                    duckTemplate,
                    transform
                );

            duck.name =
                "SoloDuck_" + (i + 1).ToString("00");

            duck.SetActive(false);

            SoloDuckAgentV2 agent =
                duck.GetComponent<SoloDuckAgentV2>();

            if (agent == null)
                agent = duck.AddComponent<SoloDuckAgentV2>();

            duck.SetActive(true);

            agent.Initialize(
                this,
                i,
                duckCount
            );
        }

        Debug.Log(
            "SoloDuckSkyManagerV2: 15 individual ducks created."
        );
    }
}
