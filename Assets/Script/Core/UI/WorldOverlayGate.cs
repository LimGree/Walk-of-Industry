/// <summary>
/// Открыто окно поверх мира, которое не MachineUI (ремонт, станции дронов).
/// Пока true — стройка, выделение, ходьба и хотбар не реагируют на клики и клавиши.
/// </summary>
public static class WorldOverlayGate
{
    public static bool IsOpen =>
        (RepairUI.Instance != null && RepairUI.Instance.IsOpen)
        || (DroneStationUI.Instance != null && DroneStationUI.Instance.IsOpen);
}
