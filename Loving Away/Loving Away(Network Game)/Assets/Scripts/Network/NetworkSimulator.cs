using System;
using System.Threading;
using UnityEngine;

/// <summary>
/// Lab 8-9: Network condition simulator for debugging and testing
/// Simulates packet loss, latency, and jitter to test reliability and interpolation systems
/// Thread-safe: can be used from worker threads (SendInput, BroadcastState)
/// </summary>
public class NetworkSimulator
{
    // Simulation parameters (public for GUI controls)
    public bool enabled = false;
    public float packetLossPercent = 0f;    // 0-100% packet loss
    public int artificialLatencyMs = 0;      // Additional delay in milliseconds
    public int jitterVarianceMs = 0;         // ±variance in milliseconds

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
    /// Simulates latency and jitter by sleeping the current thread
    /// Call this BEFORE sending to add artificial delay
    /// Safe to call from worker threads (uses Thread.Sleep, not Unity APIs)
    ///
    /// Total delay = artificialLatencyMs + random jitter (-jitterVarianceMs to +jitterVarianceMs)
    /// </summary>
    public void SimulateLatency()
    {
        if (!enabled) return;

        int totalDelay = artificialLatencyMs;

        // Add jitter (random variance)
        if (jitterVarianceMs > 0)
        {
            int jitter = random.Next(-jitterVarianceMs, jitterVarianceMs + 1);
            totalDelay += jitter;
        }

        // Ensure non-negative delay
        if (totalDelay > 0)
        {
            Thread.Sleep(totalDelay);
        }
    }

    /// <summary>
    /// Combined simulation: check packet loss + apply latency
    /// Returns true if packet should be sent (after applying latency)
    /// Returns false if packet was dropped
    ///
    /// Example usage in send method:
    ///   if (networkSimulator.SimulateAndCheckSend())
    ///   {
    ///       socket.SendTo(data, endpoint);
    ///   }
    /// </summary>
    public bool SimulateAndCheckSend()
    {
        if (!enabled) return true;

        // Check packet loss first
        if (!ShouldSendPacket())
        {
            return false; // Packet dropped
        }

        // Apply latency
        SimulateLatency();

        return true; // Packet sent (after delay)
    }
}
