# FusionVRPlus

FusionVRPlus is a lightweight and powerful wrapper for **Fusion 2**, designed to make **VR multiplayer** development fast and straightforward.

## Features

- Oculus Attention API integration  
- Very low latency networking (performance depends on your internet connection)  
- Easy-to-use scripting API  
- Custom cosmetic slots system  
- PlayFab integration (required for Attention API)  
- Developer console with admin commands (requires PlayFab, not included in the first release)  
- Automatic offline cosmetics, colors, and name handling  
- Improved ban handling for better security

## Overview

FusionVRPlus provides a simple and efficient way to build multiplayer VR games using Fusion 2.  
It takes care of common networking tasks, integrates essential APIs, and includes systems for cosmetics, moderation, and more — allowing you to focus on gameplay instead of low-level networking details.

## Usage

# Room Stuff

```cs
//CONNECTING 
// if the room is already created the max players wont change neither will the PrivateRoom bool
FusionVRPlusManager.Manager.ConnectToRoom(string roomName, int MaxPlayers, bool PrivateRoom);
```

```cs
//DISCONNECTING
FusionVRPlusManager.Manager.LeaveRoom();
```

