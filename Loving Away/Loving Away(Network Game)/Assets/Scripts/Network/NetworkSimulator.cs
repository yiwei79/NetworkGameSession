using System;
using System.Threading;
using UnityEngine;

/// <summary>
/// Lab 8-9: Network condition simulator for debugging and testing
/// Simulates packet loss to test ACK/retransmission reliability
/// Thread-safe: can be used from worker threads (SendInput, BroadcastState)
///
/// NOTE: Latency/jitter simulation removed - Thread.Sleep() blocks threads and causes game lag.
/// For latency testing, use actual network conditions (LAN, WiFi, etc.)
/// </summary>
public class NetworkSimulator
{
    // Simulation parameters (public for GUI controls)
    public bool enabled = false;
    public float packetLossPercent = 0f;    // 0-100% packet loss

    private System.Random random = new System.Random();

    /// <summary>
    /// Simulates packet loss before sending a packet
    /// Returns true if packet should be sent, false if dropped
    /// Call this BEFORE sending to simulate loss
    /// </summary>
    /// <returns>True = send packet, False = drop packet</returns>
    public bool ShouldSendPacket()
    {
        if (!enabled) return true;

        // Simulate packet loss
        if (packetLossPercent > 0)
        {
            float roll = (float)random.NextDouble() * 100f;
            if (roll < packetLossPercent)
            {
                UnityEngine.Debug.Log($"[NetSim] Packet DROPPED ({packetLossPercent:F0}% loss)");
                return false; // Drop packet
            }
        }

        return true; // Send packet
    }

    /// <summary>
    /// Simulates network conditions before sending a packet
    /// Returns true if packet should be sent, false if dropped
    ///
    /// Example usage in send method:
    ///   if (networkSimulator.SimulateAndCheckSend())
    ///   {
    ///       socket.SendTo(data, endpoint);
    ///   }
    /// </summary>
    public bool SimulateAndCheckSend()
    {
        // Only simulate packet loss (latency simulation removed to avoid thread blocking)
        return ShouldSendPacket();
    }
}
