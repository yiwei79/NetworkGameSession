# Network Scripts

This folder contains all networking-related code for Deliverable 3.

## Files

- **NetworkProtocol.cs** - Message type definitions and data structures
- **Serializer.cs** - Binary serialization utilities (BinaryWriter/BinaryReader)
- **GameNetworkManager.cs** - Main networking component (UDP client/server)

## Usage

1. Add `GameNetworkManager` component to a GameObject in your scene
2. Configure server/client settings in the Inspector
3. Reference from `SimplePlayerController` to send/receive messages

## Network Flow

```
SimplePlayerController → GameNetworkManager → UDP Socket → Network
                ↑                                                 ↓
                └─────────────── OnStateUpdate ←─────────────────┘
```

