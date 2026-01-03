using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lab 9: Circular buffer for storing timestamped server state snapshots
/// Enables interpolation of remote player positions at 60 FPS rendering
/// Maintains a sliding window of the last 3 snapshots (150ms @ 20Hz server tick)
/// </summary>
public class SnapshotBuffer
{
    /// <summary>
    /// Snapshot with server timestamp for interpolation calculations
    /// </summary>
    private struct TimestampedSnapshot
    {
        public float timestamp;                                 // Server time when snapshot was created
        public Dictionary<uint, PlayerSnapshot> players;        // playerId → snapshot
    }

    private Queue<TimestampedSnapshot> buffer = new Queue<TimestampedSnapshot>();
    private const int CAPACITY = 3; // 150ms @ 20Hz (3 snapshots for interpolation)

    /// <summary>
    /// Adds a new server state snapshot to the buffer with timestamp
    /// Automatically prunes oldest snapshots if buffer exceeds capacity
    /// </summary>
    /// <param name="serverTime">Server timestamp from ServerStateUpdateMessage</param>
    /// <param name="snapshots">Array of player snapshots from the update</param>
    public void AddSnapshot(float serverTime, PlayerSnapshot[] snapshots)
    {
        // Convert array to dictionary for efficient lookup by playerId
        var snapshotDict = new Dictionary<uint, PlayerSnapshot>();
        foreach (var snapshot in snapshots)
        {
            snapshotDict[snapshot.playerId] = snapshot;
        }

        buffer.Enqueue(new TimestampedSnapshot
        {
            timestamp = serverTime,
            players = snapshotDict
        });

        // Maintain capacity limit
        while (buffer.Count > CAPACITY)
        {
            buffer.Dequeue();
        }
    }

    /// <summary>
    /// Gets an interpolated snapshot for a specific player at a given render time
    /// Uses linear interpolation between two bracketing snapshots
    ///
    /// Interpolation formula:
    ///   t = (renderTime - olderTime) / (newerTime - olderTime)
    ///   position = lerp(olderPos, newerPos, t)
    ///
    /// Typically renderTime = serverTime - 100ms to ensure smooth playback
    /// </summary>
    /// <param name="playerId">ID of player to interpolate</param>
    /// <param name="renderTime">Target time to render (usually serverTime - interpolationDelay)</param>
    /// <returns>Interpolated player snapshot, or latest/default if insufficient data</returns>
    public PlayerSnapshot GetInterpolatedSnapshot(uint playerId, float renderTime)
    {
        if (buffer.Count < 2)
        {
            // Not enough snapshots for interpolation - return latest if available
            if (buffer.Count == 1)
            {
                foreach (var snapshot in buffer)
                {
                    if (snapshot.players.ContainsKey(playerId))
                    {
                        return snapshot.players[playerId];
                    }
                }
            }

            // No data available - return default snapshot
            return new PlayerSnapshot { playerId = playerId };
        }

        // Find two snapshots to interpolate between
        // older: snapshot with timestamp <= renderTime
        // newer: snapshot with timestamp > renderTime
        TimestampedSnapshot? older = null;
        TimestampedSnapshot? newer = null;

        foreach (var snapshot in buffer)
        {
            if (snapshot.timestamp <= renderTime)
            {
                // This is a potential "older" snapshot (take the latest one <= renderTime)
                older = snapshot;
            }
            else if (!newer.HasValue || snapshot.timestamp < newer.Value.timestamp)
            {
                // This is a potential "newer" snapshot (take the earliest one > renderTime)
                newer = snapshot;
            }
        }

        // If we have both bracketing snapshots and player exists in both
        if (older.HasValue && newer.HasValue &&
            older.Value.players.ContainsKey(playerId) &&
            newer.Value.players.ContainsKey(playerId))
        {
            // Calculate interpolation factor (0.0 = older, 1.0 = newer)
            float timeDelta = newer.Value.timestamp - older.Value.timestamp;
            float t = 0f;
            if (timeDelta > 0.001f) // Avoid division by zero
            {
                t = (renderTime - older.Value.timestamp) / timeDelta;
                t = Mathf.Clamp01(t); // Ensure t is in [0, 1] range
            }

            PlayerSnapshot oldSnap = older.Value.players[playerId];
            PlayerSnapshot newSnap = newer.Value.players[playerId];

            // Interpolate continuous values (position, velocity)
            // Use newer snapshot for discrete state (isAlive, health)
            return new PlayerSnapshot
            {
                playerId = playerId,
                position = Vector3.Lerp(oldSnap.position, newSnap.position, t),
                velocity = Vector3.Lerp(oldSnap.velocity, newSnap.velocity, t),
                isAlive = newSnap.isAlive,   // Use newer state for binary flags
                health = newSnap.health       // Use newer state for health
            };
        }

        // Fallback: return newer snapshot if available
        if (newer.HasValue && newer.Value.players.ContainsKey(playerId))
        {
            return newer.Value.players[playerId];
        }

        // Fallback: return older snapshot if available
        if (older.HasValue && older.Value.players.ContainsKey(playerId))
        {
            return older.Value.players[playerId];
        }

        // No data for this player - return default
        return new PlayerSnapshot { playerId = playerId };
    }
}
