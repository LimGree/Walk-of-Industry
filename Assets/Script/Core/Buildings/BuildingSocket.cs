using UnityEngine;

public enum SocketType
{
    Input,
    Output
}

/// <summary>
/// I/O точка здания. Связи считает BuildingLinker по соседним клеткам.
/// </summary>
public class BuildingSocket : MonoBehaviour
{
    public SocketType socketType = SocketType.Output;

    [Header("Connections")]
    public BuildingSocket connectedSocket;

    [Header("Visual (optional)")]
    public GameObject connectionIndicator;

    [Header("Debug")]
    public bool showDebug = false;

    public BuildingBase Owner => GetComponentInParent<BuildingBase>();

    public bool IsConnected => connectedSocket != null;

    public void ConnectSocket(BuildingSocket other)
    {
        if (other == null) return;

        if (connectedSocket != null && connectedSocket != other)
            DisconnectSocket();
        if (other.connectedSocket != null && other.connectedSocket != this)
            other.DisconnectSocket();

        connectedSocket = other;
        other.connectedSocket = this;

        if (connectionIndicator != null)
            connectionIndicator.SetActive(true);
        if (other.connectionIndicator != null)
            other.connectionIndicator.SetActive(true);

        if (showDebug)
            Debug.Log($"[Socket] {name} ↔ {other.name}");
    }

    public void DisconnectSocket()
    {
        if (connectedSocket != null)
        {
            BuildingSocket other = connectedSocket;
            connectedSocket = null;
            other.connectedSocket = null;

            if (other.connectionIndicator != null)
                other.connectionIndicator.SetActive(false);
        }

        if (connectionIndicator != null)
            connectionIndicator.SetActive(false);
    }

    public void DisconnectAll()
    {
        DisconnectSocket();
    }

    public Vector3 GetOutward()
    {
        Vector3 world = transform.forward;
        world.y = 0f;
        return world.sqrMagnitude > 0.0001f ? world.normalized : Vector3.forward;
    }

    void OnDestroy()
    {
        DisconnectAll();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = socketType == SocketType.Input
            ? new Color(1f, 0.5f, 0.12f, 0.9f)
            : new Color(0.2f, 0.9f, 0.35f, 0.9f);
        Gizmos.DrawSphere(transform.position, 0.08f);
        Gizmos.DrawLine(transform.position, transform.position + GetOutward() * 0.55f);
    }
}
