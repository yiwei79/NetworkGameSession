# Gameplay Scripts

This folder contains game logic and player control code.

## Files

- **ServerGameState.cs** - Server-authoritative game state (player positions, physics)
- **SimplePlayerController.cs** - Client-side input and rendering
- **ShootVisualFeedback.cs** - Visual effects for shooting action

## Usage

1. Add `SimplePlayerController` component to a GameObject
2. Assign `GameNetworkManager` and player prefab references
3. Set local player ID and colors
4. `ShootVisualFeedback` is automatically added to player GameObjects

## Responsibilities

- **ServerGameState**: Authority on all game state, runs on server
- **SimplePlayerController**: Collects input, renders state, runs on all clients
- **ShootVisualFeedback**: Visual polish, runs on all clients

