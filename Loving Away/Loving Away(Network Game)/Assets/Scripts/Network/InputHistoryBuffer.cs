using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lab 8: Circular buffer for storing sent client inputs with timestamps
/// Used for retransmission when inputs are not ACKed by the server
/// Maintains a sliding window of the last 30 inputs (1 second @ 30Hz client send rate)
/// FIX: Uses Dictionary to allow updating send times after retransmission (prevents spam)
/// </summary>
public class InputHistoryBuffer
{
    /// <summary>
    /// Stored input with send timestamp for retransmission timeout calculation
    /// </summary>
    private class StoredInput
    {
        public ClientInputMessage input;
        public float sendTime;
        public float lastRetransmitTime; // Track when we last retransmitted this input
    }

    private Dictionary<uint, StoredInput> buffer = new Dictionary<uint, StoredInput>();
    private const int MAX_CAPACITY = 30; // 1 second @ 30Hz send rate

    /// <summary>
    /// Adds a sent input to the history buffer
    /// Automatically prunes oldest entries if buffer exceeds capacity
    /// </summary>
    /// <param name="input">The input message that was sent</param>
    /// <param name="sendTime">Unity Time.time when the input was sent</param>
    public void AddInput(ClientInputMessage input, float sendTime)
    {
        buffer[input.sequenceNumber] = new StoredInput
        {
            input = input,
            sendTime = sendTime,
            lastRetransmitTime = 0f // Never retransmitted yet
        };

        // Prune oldest entries if buffer too large
        if (buffer.Count > MAX_CAPACITY)
        {
            // Find and remove the oldest sequence number
            uint oldestSeq = uint.MaxValue;
            foreach (var seq in buffer.Keys)
            {
                if (seq < oldestSeq) oldestSeq = seq;
            }
            buffer.Remove(oldestSeq);
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

        foreach (var kvp in buffer)
        {
            if (kvp.Value.input.sequenceNumber > lastAckedSequence)
            {
                unacked.Add(kvp.Value.input);
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
        List<uint> toRemove = new List<uint>();
        foreach (var kvp in buffer)
        {
            if (kvp.Key <= lastAckedSequence)
            {
                toRemove.Add(kvp.Key);
            }
        }

        foreach (var seq in toRemove)
        {
            buffer.Remove(seq);
        }
    }

    /// <summary>
    /// Gets inputs that need to be retransmitted due to timeout
    /// An input is considered timed out if:
    ///   - It has not been ACKed (sequence > lastAckedSequence)
    ///   - AND it was sent more than 'timeout' seconds ago (or last retransmit was timeout ago)
    ///   - FIX: Checks lastRetransmitTime to prevent retransmit spam
    /// </summary>
    /// <param name="lastAckedSequence">Last sequence number ACKed by server</param>
    /// <param name="currentTime">Current Unity Time.time</param>
    /// <param name="timeout">Retransmission timeout in seconds (e.g., 0.05 for 50ms)</param>
    /// <returns>List of (input, original send time) tuples for retransmission</returns>
    public List<(ClientInputMessage, float)> GetInputsForRetransmit(uint lastAckedSequence, float currentTime, float timeout)
    {
        List<(ClientInputMessage, float)> toRetransmit = new List<(ClientInputMessage, float)>();

        foreach (var kvp in buffer)
        {
            StoredInput stored = kvp.Value;

            // Input needs retransmit if:
            // 1. Not ACKed yet (sequence > lastAckedSequence)
            // 2. AND either:
            //    a) Never retransmitted (lastRetransmitTime == 0) and sent > timeout ago
            //    b) Was retransmitted, but last retransmit was > timeout ago
            bool notAcked = stored.input.sequenceNumber > lastAckedSequence;
            bool neverRetransmitted = stored.lastRetransmitTime == 0f;
            float timeSinceOriginalSend = currentTime - stored.sendTime;
            float timeSinceLastRetransmit = currentTime - stored.lastRetransmitTime;

            bool needsRetransmit = false;
            if (notAcked)
            {
                if (neverRetransmitted && timeSinceOriginalSend > timeout)
                {
                    needsRetransmit = true; // First retransmit
                }
                else if (!neverRetransmitted && timeSinceLastRetransmit > timeout)
                {
                    needsRetransmit = true; // Subsequent retransmit
                }
            }

            if (needsRetransmit)
            {
                toRetransmit.Add((stored.input, stored.sendTime));
            }
        }

        return toRetransmit;
    }

    /// <summary>
    /// Marks an input as retransmitted by updating its lastRetransmitTime
    /// This prevents retransmit spam by tracking when we last resent this input
    /// </summary>
    /// <param name="sequenceNumber">Sequence number of the retransmitted input</param>
    /// <param name="currentTime">Current Unity Time.time</param>
    public void MarkAsRetransmitted(uint sequenceNumber, float currentTime)
    {
        if (buffer.ContainsKey(sequenceNumber))
        {
            buffer[sequenceNumber].lastRetransmitTime = currentTime;
        }
    }
}
