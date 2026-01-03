using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lab 8: Circular buffer for storing sent client inputs with timestamps
/// Used for retransmission when inputs are not ACKed by the server
/// Maintains a sliding window of the last 30 inputs (1 second @ 30Hz client send rate)
/// </summary>
public class InputHistoryBuffer
{
    /// <summary>
    /// Stored input with send timestamp for retransmission timeout calculation
    /// </summary>
    private struct StoredInput
    {
        public ClientInputMessage input;
        public float sendTime;
    }

    private Queue<StoredInput> buffer = new Queue<StoredInput>();
    private const int MAX_CAPACITY = 30; // 1 second @ 30Hz send rate

    /// <summary>
    /// Adds a sent input to the history buffer
    /// Automatically prunes oldest entries if buffer exceeds capacity
    /// </summary>
    /// <param name="input">The input message that was sent</param>
    /// <param name="sendTime">Unity Time.time when the input was sent</param>
    public void AddInput(ClientInputMessage input, float sendTime)
    {
        buffer.Enqueue(new StoredInput { input = input, sendTime = sendTime });

        // Prune oldest entries if buffer too large
        while (buffer.Count > MAX_CAPACITY)
        {
            buffer.Dequeue();
        }
    }

    /// <summary>
    /// Gets all inputs that have not been ACKed yet
    /// Used for diagnostics and debugging
    /// </summary>
    /// <param name="lastAckedSequence">Last sequence number ACKed by server</param>
    /// <returns>List of unACKed inputs</returns>
    public List<ClientInputMessage> GetUnackedInputs(uint lastAckedSequence)
    {
        List<ClientInputMessage> unacked = new List<ClientInputMessage>();

        foreach (var stored in buffer)
        {
            if (stored.input.sequenceNumber > lastAckedSequence)
            {
                unacked.Add(stored.input);
            }
        }

        return unacked;
    }

    /// <summary>
    /// Removes all inputs with sequence <= lastAckedSequence from the buffer
    /// Called when new ACKs are received to free up memory
    /// </summary>
    /// <param name="lastAckedSequence">Last sequence number confirmed by server</param>
    public void PruneAckedInputs(uint lastAckedSequence)
    {
        // Remove all inputs with sequence <= lastAckedSequence
        while (buffer.Count > 0 && buffer.Peek().input.sequenceNumber <= lastAckedSequence)
        {
            buffer.Dequeue();
        }
    }

    /// <summary>
    /// Gets inputs that need to be retransmitted due to timeout
    /// An input is considered timed out if:
    ///   - It has not been ACKed (sequence > lastAckedSequence)
    ///   - AND it was sent more than 'timeout' seconds ago
    /// </summary>
    /// <param name="lastAckedSequence">Last sequence number ACKed by server</param>
    /// <param name="currentTime">Current Unity Time.time</param>
    /// <param name="timeout">Retransmission timeout in seconds (e.g., 0.15 for 150ms)</param>
    /// <returns>List of (input, original send time) tuples for retransmission</returns>
    public List<(ClientInputMessage, float)> GetInputsForRetransmit(uint lastAckedSequence, float currentTime, float timeout)
    {
        List<(ClientInputMessage, float)> toRetransmit = new List<(ClientInputMessage, float)>();

        foreach (var stored in buffer)
        {
            // Input needs retransmit if:
            // 1. Not ACKed yet (sequence > lastAckedSequence)
            // 2. AND sent more than 'timeout' seconds ago
            if (stored.input.sequenceNumber > lastAckedSequence &&
                currentTime - stored.sendTime > timeout)
            {
                toRetransmit.Add((stored.input, stored.sendTime));
            }
        }

        return toRetransmit;
    }
}
