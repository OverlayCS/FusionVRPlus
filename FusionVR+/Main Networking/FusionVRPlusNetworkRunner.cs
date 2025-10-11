using Fusion;
using Fusion.Sockets;
using FusionVRPlus.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace FusionVRPlus.Networking
{
    public class FusionVRPlusNetworkRunner : SimulationBehaviour, INetworkRunnerCallbacks
    {
        public static FusionVRPlusNetworkRunner Instance;

        public static NetworkRunner Runner;
        public static GameObject tempRunnerObject;

        public void Awake()
        {
            Instance = this;
        }

        public static void SetUpRunner()
        {
            if(Runner != null)
            {
                FusionVRPlusLogger.PrintMessage("Cant Reuse Network Runners", MessageType.Warning);
                return;
            }

            tempRunnerObject = Instantiate(FusionVRPlusManager.Manager.NetworkRunnerPrefab);
            Runner = tempRunnerObject.GetComponent<NetworkRunner>();
            Runner.ProvideInput = true;
            Runner.AddCallbacks(Instance);
        }
        public static List<FusionVRPlusPlayer> GetPlayerList()
        {
            if(Runner == null)
            {
                FusionVRPlusLogger.PrintMessage("Cant get player list as your not in a room", MessageType.Warning);
                return new List<FusionVRPlusPlayer>();
            }

            return Players;
        }
        public static bool IsSharedModeMasterClient()
        {
            return IsInRoom() && Runner.IsSharedModeMasterClient;
        }
        public static bool IsInRoom()
        {
            return Runner != null && Runner.IsRunning && Runner.SessionInfo != null;
        }

        private static HashSet<PlayerRef> _waitingForSpawn = new HashSet<PlayerRef>();

        public static List<FusionVRPlusPlayer> Players = new();
        private static void UpdatePlayerList()
        {
            if (FusionVRPlusNetworkRunner.Runner == null)
            {
                Players.Clear();
                return;
            }

            Players.RemoveAll(p => p == null);

            var foundPlayers = new List<FusionVRPlusPlayer>();

            foreach (var playerRef in FusionVRPlusNetworkRunner.Runner.ActivePlayers)
            {
                if (FusionVRPlusNetworkRunner.Runner.TryGetPlayerObject(playerRef, out var netObj) && netObj != null)
                {
                    var plrScript = netObj.GetComponent<FusionVRPlusPlayer>();
                    if (plrScript != null)
                        foundPlayers.Add(plrScript);
                }
                else
                {
                    if (!_waitingForSpawn.Contains(playerRef))
                    {
                        _waitingForSpawn.Add(playerRef);
                        FusionVRPlusCoroutineManager.RunCoroutine(WaitForPlayerObjectAndAdd(playerRef, 2f));
                    }
                }
            }

            Players.Clear();
            foreach (var p in foundPlayers.Distinct())
                Players.Add(p);
        }

        private static System.Collections.IEnumerator WaitForPlayerObjectAndAdd(PlayerRef playerRef, float timeoutSeconds)
        {
            float elapsed = 0f;

            while (elapsed < timeoutSeconds)
            {
                if (FusionVRPlusNetworkRunner.Runner != null && FusionVRPlusNetworkRunner.Runner.TryGetPlayerObject(playerRef, out var netObj) && netObj != null)
                {
                    var plrScript = netObj.GetComponent<FusionVRPlusPlayer>();
                    if (plrScript != null && !Players.Contains(plrScript))
                    {
                        Players.Add(plrScript);
                    }
                    break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            _waitingForSpawn.Remove(playerRef);
        }

        #region Network Callbacks

        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {

        }

        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {

        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (player == runner.LocalPlayer)
            {
                if (!runner.TryGetPlayerObject(player, out _))
                {
                    Vector3 spawnPos = Vector3.zero;
                    if (GorillaLocomotion.Player.Instance != null)
                        spawnPos = GorillaLocomotion.Player.Instance.transform.position;

                    var plrObject = runner.Spawn(FusionVRPlusManager.Manager.PlayerPrefab, spawnPos, Quaternion.identity, player);
                    runner.SetPlayerObject(player, plrObject);
                }
            }

            UpdatePlayerList();
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (runner.TryGetPlayerObject(player, out NetworkObject obj))
            {
                if (runner.IsSharedModeMasterClient || runner.IsServer)
                {
                    runner.Despawn(obj);
                }
            }

            UpdatePlayerList();
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            Players.Clear();

            bool isNormalOrExpectedShutdown =
                shutdownReason == ShutdownReason.Ok ||
                shutdownReason == ShutdownReason.GameIsFull ||
                shutdownReason == ShutdownReason.MaxCcuReached;

            if (FusionVRPlusManager.Manager.AutoReconnectOnError && !isNormalOrExpectedShutdown)
            {
                FusionVRPlusManager.Manager.ReconnectToLastRoom();
            }
        }

        void HandleDisconnect(NetDisconnectReason reason)
        {
            switch (reason)
            {
                case NetDisconnectReason.SendWindowFull:
                    FusionVRPlusLogger.PrintMessage("Client Might Be Spamming RPC or have ban Wifi.", MessageType.Fatal);
                    break;
                case NetDisconnectReason.Timeout:
                    FusionVRPlusLogger.PrintMessage("Client Connection Timed out.", MessageType.Error);
                    break;
                case NetDisconnectReason.SequenceOutOfBounds:
                    FusionVRPlusLogger.PrintMessage("Client is sending out of sync packets try replaying the game.", MessageType.Error);
                    break;
                case NetDisconnectReason.ByRemote:
                    FusionVRPlusLogger.PrintMessage("Client was disconnected by a remote request.", MessageType.Error);
                    break;
                case NetDisconnectReason.Unknown:
                    FusionVRPlusLogger.PrintMessage("Unknown error has been encountered so client was disconnected", MessageType.Error);
                    break;
            }
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            HandleDisconnect(reason);
        }

        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
        {
            FusionVRPlusLogger.PrintMessage($"Incoming connect request from {request.RemoteAddress}.", MessageType.Info);

            if (runner.ActivePlayers.Count() >= 10)
            {
                FusionVRPlusLogger.PrintMessage("Player attempted to join when room was full", MessageType.Error);
                request.Refuse();
                return;
            }

            FusionVRPlusLogger.PrintMessage($"Player {request.RemoteAddress} was accepted", MessageType.Info);
            request.Accept();
        }

        void HandleConnectionFailed(NetConnectFailedReason reason)
        {
            switch (reason)
            {
                case NetConnectFailedReason.Timeout:
                    FusionVRPlusLogger.PrintMessage("Server is not responding you may have been timedout try rejoining game", MessageType.Error);
                    break;
                case NetConnectFailedReason.ServerFull:
                    FusionVRPlusLogger.PrintMessage("Server is currently full unable to join", MessageType.Error);
                    break;
                case NetConnectFailedReason.ServerRefused:
                    FusionVRPlusLogger.PrintMessage("Server refused your connection attempt try agian later", MessageType.Error);
                    break;
            }
        }

        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
            HandleConnectionFailed(reason);
        }

        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
        {

        }

        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
        {

        }

        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
        {

        }

        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            FusionVRPlusInput playerInput = new FusionVRPlusInput
            {
                HeadPosition = FusionVRPlusManager.Manager.Head.transform.position,
                HeadRotation = FusionVRPlusManager.Manager.Head.transform.rotation,
                LeftHandPosition = FusionVRPlusManager.Manager.LHand.transform.position,
                LeftHandRotation = FusionVRPlusManager.Manager.LHand.transform.rotation,
                RightHandPosition = FusionVRPlusManager.Manager.RHand.transform.position,
                RightHandRotation = FusionVRPlusManager.Manager.RHand.transform.rotation
            };
            input.Set(playerInput);
        }

        public List<SessionInfo> Sessions = new();

        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
        {

        }

        public void OnConnectedToServer(NetworkRunner runner)
        {

        }

        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
            Sessions = sessionList;
            FusionVRPlusLogger.PrintMessage("Session List Updated", MessageType.Info);
        }

        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
        {

        }

        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
        {

        }

        public void OnSceneLoadDone(NetworkRunner runner)
        {

        }

        public void OnSceneLoadStart(NetworkRunner runner)
        {

        }
        #endregion
    }
}
