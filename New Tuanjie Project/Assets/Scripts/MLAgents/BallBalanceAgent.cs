using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class BallBalanceAgent : Agent
{
    [SerializeField] private Transform platform;
    [SerializeField] private Rigidbody platformRigidbody;
    [SerializeField] private Rigidbody ballRigidbody;
    [SerializeField] private float maxTiltAngle = 10f;
    [SerializeField] private float tiltResponsiveness = 8f;
    [SerializeField] private float resetHeight = 1.2f;
    [SerializeField] private float failHeight = -1.0f;
    [SerializeField] private float platformLimit = 3.8f;
    [SerializeField] private float spawnRadius = 0.75f;

    private float currentTiltX;
    private float currentTiltZ;

    private Vector3 platformStartPosition;
    private Quaternion platformStartRotation;
    private float worldFailHeight;

    public override void Initialize()
    {
        if (platform == null)
        {
            platform = transform;
        }

        if (platformRigidbody == null)
        {
            platformRigidbody = platform.GetComponent<Rigidbody>();
        }

        platformStartPosition = platform.position;
        platformStartRotation = platform.rotation;
        worldFailHeight = platformStartPosition.y - 1.0f;

        // Keep episodes bounded so training can always recover from bad physics states.
        if (MaxStep <= 0)
        {
            MaxStep = 2000;
        }
    }

    public override void OnEpisodeBegin()
    {
        platform.position = platformStartPosition;
        platform.rotation = platformStartRotation;
        currentTiltX = 0f;
        currentTiltZ = 0f;

        if (platformRigidbody != null)
        {
            platformRigidbody.velocity = Vector3.zero;
            platformRigidbody.angularVelocity = Vector3.zero;
        }

        if (ballRigidbody != null)
        {
            Vector2 spawnOffset = Random.insideUnitCircle * spawnRadius;
            Vector3 randomOffset = new(spawnOffset.x, 0f, spawnOffset.y);
            ballRigidbody.transform.position = platform.position + Vector3.up * resetHeight + randomOffset;
            ballRigidbody.transform.rotation = Quaternion.identity;
            ballRigidbody.velocity = Vector3.zero;
            ballRigidbody.angularVelocity = Vector3.zero;
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 localBallPosition = platform.InverseTransformPoint(ballRigidbody.transform.position);
        sensor.AddObservation(localBallPosition / platformLimit);
        sensor.AddObservation(ballRigidbody.velocity / 10f);
        sensor.AddObservation(platform.right.y);
        sensor.AddObservation(platform.forward.y);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (ballRigidbody == null || platform == null)
        {
            EndEpisode();
            return;
        }

        float rotateX = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float rotateZ = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);

        if (platformRigidbody != null)
        {
            currentTiltX = Mathf.Lerp(currentTiltX, rotateX * maxTiltAngle, Time.fixedDeltaTime * tiltResponsiveness);
            currentTiltZ = Mathf.Lerp(currentTiltZ, rotateZ * maxTiltAngle, Time.fixedDeltaTime * tiltResponsiveness);

            Quaternion targetRotation = platformStartRotation * Quaternion.Euler(currentTiltX, 0f, currentTiltZ);
            platformRigidbody.MoveRotation(targetRotation);
            platformRigidbody.angularVelocity = Vector3.zero;
        }

        Vector3 worldBallPosition = ballRigidbody.transform.position;
        Vector3 localBallPosition = platform.InverseTransformPoint(worldBallPosition);

        if (HasInvalidState(worldBallPosition, localBallPosition))
        {
            AddReward(-1f);
            EndEpisode();
            return;
        }

        float distanceFromCenter = new Vector2(localBallPosition.x, localBallPosition.z).magnitude;
        if (distanceFromCenter > platformLimit * 0.85f)
        {
            AddReward(-0.5f);
            EndEpisode();
            return;
        }

        float centerReward = Mathf.Clamp01(1f - distanceFromCenter / platformLimit);
        float velocityPenalty = Mathf.Clamp01(ballRigidbody.velocity.magnitude / 10f);
        float tiltPenalty = (Mathf.Abs(currentTiltX) + Mathf.Abs(currentTiltZ)) / (maxTiltAngle * 2f);

        AddReward(0.005f);
        AddReward(centerReward * 0.02f);
        AddReward(-velocityPenalty * 0.003f);
        AddReward(-tiltPenalty * 0.002f);
    }

    private bool HasInvalidState(Vector3 worldBallPosition, Vector3 localBallPosition)
    {
        if (float.IsNaN(worldBallPosition.x) || float.IsNaN(worldBallPosition.y) || float.IsNaN(worldBallPosition.z))
        {
            return true;
        }

        if (float.IsNaN(localBallPosition.x) || float.IsNaN(localBallPosition.y) || float.IsNaN(localBallPosition.z))
        {
            return true;
        }

        if (ballRigidbody.velocity.sqrMagnitude > 400f)
        {
            return true;
        }

        if (worldBallPosition.y < Mathf.Max(failHeight, worldFailHeight))
        {
            return true;
        }

        if (Mathf.Abs(localBallPosition.x) > platformLimit || Mathf.Abs(localBallPosition.z) > platformLimit)
        {
            return true;
        }

        return false;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetAxis("Vertical");
        continuousActions[1] = -Input.GetAxis("Horizontal");
    }
}
