using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using FusionVRPlus.Misc;
using FusionVRPlus.PlayFabNetworking;
using FusionVRPlus.Saving;
using Photon.Pun;
using Photon.Voice.Unity;
using PlayFab;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FusionVRPlus.Networking
{

    [System.Serializable]
    public class FusionVRPlusServer
    {
        public string APPID;
        public string VOICEID;
    }

    public enum FusionVRPlusConnectionState : byte
    {
        Connected,  
        Disconnected,
        Connecting,
        Disconnecting,
        FailedToJoin,
    }

    public struct FusionVRPlusInput : INetworkInput
    {
        public Vector3 HeadPosition;
        public Quaternion HeadRotation;
        public Vector3 LeftHandPosition;
        public Quaternion LeftHandRotation;
        public Vector3 RightHandPosition;
        public Quaternion RightHandRotation;
    }

    public class FusionVRPlusManager : MonoBehaviour, INetworkRunnerCallbacks
    {
        public static FusionVRPlusManager Manager;
        public static NetworkRunner Runner;
        public static GameObject tempRunnerObject;
        public static FusionVRPlusPlayer LocalPlayer;

        [Header("Server Settings")]
        public FusionVRPlusServer MainServer;
        public FusionVRPlusServer[] BackupServer;

        [Header("Player References")]
        public GameObject Head;
        public GameObject LHand;
        public GameObject RHand;
        public GameObject PlayerPrefab;

        [Header("Default Player Settings")]
        public string DefaultPlayerName;
        public Color DefaultPlayerColor;

        [Header("Network Options")]
        public bool AutoConnectToPhoton;
        public bool AutoJoinRoom;
        public bool AutoReconnectOnError;
        public bool BlockNetworkingIfBanned;
        public bool AutoConnectToPlayFab = true;

        [Header("Network Runtime")]
        public GameObject NetworkRunnerPrefab;
        public FusionVRPlusConnectionState ConnectionState = FusionVRPlusConnectionState.Disconnected;

        [Header("Last Joined Room Info (Do Not Edit)")]
        public string LastJoinedRoomName;
        public int LastJoinedRoomPlayerCount;
        public bool LastJoinedRoomIsPrivate;
        public GameObject RigTemp;

        private HashSet<PlayerRef> _waitingForSpawn = new();
        public List<FusionVRPlusPlayer> Players = new();

        private void Start()
        {
            SpawnOfflineRig();

            FusionVRPlusCoroutineManager.instance.StartCoroutine(DelayedAutoConnect());
        }

        public bool IsSharedModeMasterClient()
        {
            return IsInRoom() && Runner.IsSharedModeMasterClient;
        }

        IEnumerator DelayedAutoConnect()
        {
            if (AutoConnectToPlayFab)
            {
                FusionVRPlusPlayFabManager.Instance.ConnectToPlayFab(SystemInfo.deviceUniqueIdentifier);
            }
            
            yield return new WaitForSeconds(2f);

            if (AutoConnectToPhoton)
            {
                SetUpRunner();
            }

            if (AutoJoinRoom)
            {
                string roomCode = GenerateRoomCode();
                _ = ConnectToServer(roomCode, 10, false);
            }

        }

        private void Awake()
        {

            if(Manager == null)
            {
                Manager = this;
                Manager.AddComponent<FusionVRPlusCoroutineManager>(); 
                DontDestroyOnLoad(this);
            }
            else
            {
                Destroy(Manager);
            }
        }
        #region Room Management

        public bool IsInRoom()
        {
            return Runner != null && Runner.IsRunning && Runner.SessionInfo != null;
        }
        public string GenerateRoomCode()
        {
            string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            System.Random random = new System.Random();
            return new string(Enumerable.Repeat(chars, 6).Select(s => s[random.Next(s.Length)]).ToArray());
        }

        public static void SetUpRunner()
        {
            if (Runner != null)
            {
                FusionVRPlusLogger.PrintMessage("Cant Reuse Network Runners", FusionVRPlus.Misc.MessageType.Warning);
                return;
            }

            tempRunnerObject = Instantiate(FusionVRPlusManager.Manager.NetworkRunnerPrefab);
            Runner = tempRunnerObject.GetComponent<NetworkRunner>();
            Runner.ProvideInput = true;
            Runner.AddCallbacks(Manager);
        }

        void SpawnOfflineRig()
        {
            RigTemp = Instantiate(PlayerPrefab);
            FusionVRPlusPlayer plr = RigTemp.GetComponent<FusionVRPlusPlayer>();
            plr.InOfflineMode = true;

            NetworkTransform[] nt = plr.GetComponentsInChildren<NetworkTransform>();

            foreach(var n in nt)
            {
                n.enabled = false;
            }

            Speaker[] sp = plr.GetComponentsInChildren<Speaker>();

            foreach(var s in sp)
            {
                s.enabled = false;
            }

            FusionVRPlusOfflineRig rig = RigTemp.GetComponent<FusionVRPlusOfflineRig>();

            rig.PlayernameText.text = FusionVRPlusSavingManager.Instance.LoadLocalUsername();

            foreach(var r in rig.PlayerRenderers)
            {
                r.material.color = FusionVRPlusSavingManager.Instance.LoadLocalColor();
            }

            foreach(var id in FusionVRPlusSavingManager.Instance.LoadLocalCosmetics())
            {
                Cosmetic cos = plr.GetCosmeticFromID(id);

                cos.Object.SetActive(true);
            }
        }

        void DespawnRig()
        {
            if(RigTemp != null)
            {
                Destroy(RigTemp);
            }
        }

        public async Task ConnectToServer(string RoomName, int MaxPlayers, bool Private)
        {
            ConnectionState = FusionVRPlusConnectionState.Connecting;

            if (IsInRoom())
            {
                FusionVRPlusLogger.PrintMessage("Already in room unable to join", FusionVRPlus.Misc.MessageType.Error);
                return;
            }

            if (!CheckForValidServer())
            {
                FusionVRPlusLogger.PrintMessage(
                    "Unable to find any valid servers. Please check the main and backup server list.",
                    FusionVRPlus.Misc.MessageType.Fatal
                );
                return;
            }

            if (BlockNetworkingIfBanned && FusionVRPlusPlayFabManager.IsAccountBanned())
            {
                FusionVRPlusLogger.PrintMessage("Account Is Banned So You Cant Join Rooms", FusionVRPlus.Misc.MessageType.Error);
                return;
            }
            if(Runner == null)
            {
                SetUpRunner();
            }

            StartGameResult res = await Runner.StartGame(new StartGameArgs()
            {
                GameMode = GameMode.Shared,
                SessionName = RoomName,
                PlayerCount = MaxPlayers,
                IsVisible = !Private,
                IsOpen = true,
                SessionProperties = new Dictionary<string, SessionProperty>
                {
                    {"Version", Application.version }
                }
            });

            if (!res.Ok)
            {
                ConnectionState = FusionVRPlusConnectionState.FailedToJoin;
                FusionVRPlusLogger.PrintMessage($"Failed To Join Room Error: {res.ShutdownReason}", FusionVRPlus.Misc.MessageType.Error);
                HandleShutdownReason(res);
            }
            else
            {
                ConnectionState = FusionVRPlusConnectionState.Connected;
                LastJoinedRoomName = RoomName;
                LastJoinedRoomIsPrivate = Private;
                LastJoinedRoomPlayerCount = MaxPlayers;
            }
        }

        public void LeaveRoom()
        {
            if(Runner == null)
            {
                return;
            }

            ConnectionState = FusionVRPlusConnectionState.Disconnecting;
            Runner.Shutdown();
            Destroy(tempRunnerObject);
            Runner = null;
            tempRunnerObject = null;
            ConnectionState = FusionVRPlusConnectionState.Disconnected;
        }
        public bool CheckForValidServer()
        {
            bool IsMainServerValid =
                !string.IsNullOrWhiteSpace(MainServer.APPID) &&
                !string.IsNullOrWhiteSpace(MainServer.VOICEID);

            if (IsMainServerValid) 
            {
                PhotonAppSettings.Global.AppSettings.AppIdFusion = MainServer.APPID;
                PhotonAppSettings.Global.AppSettings.AppIdVoice = MainServer.VOICEID;
                return true;
            }

            FusionVRPlusLogger.PrintMessage(
                "Main server AppID or VoiceID is missing. Checking backup servers...",
                FusionVRPlus.Misc.MessageType.Error
            );

            foreach(var server in BackupServer)
            {
                if(string.IsNullOrWhiteSpace(server.APPID) || string.IsNullOrWhiteSpace(server.VOICEID))
                {
                    FusionVRPlusLogger.PrintMessage(
                        $"Backup server has missing AppID or VoiceID. Skipping...",
                        FusionVRPlus.Misc.MessageType.Warning
                    );
                    continue;
                }

                PhotonAppSettings.Global.AppSettings.AppIdFusion = server.APPID;
                PhotonAppSettings.Global.AppSettings.AppIdVoice = server.VOICEID;


                FusionVRPlusLogger.PrintMessage(
                    $"Switched to backup server with AppID {server.APPID}.",
                    FusionVRPlus.Misc.MessageType.Info
                );

                return true;
            }

            return false;
        }
#if UNITY_EDITOR
        public void CheckDefaultValues()
        {
            bool b = CheckForRig(this);
            if (b)
            {
                Debug.Log("Attempted to set default values");
            }
        }

        private bool CheckForRig(FusionVRPlusManager manager)
        {
            GameObject[] objects = FindObjectsOfType<GameObject>();

            bool b = false;

            if (manager.Head == null)
            {
                b = true;
                foreach (GameObject obj in objects)
                {
                    if (obj.name.Contains("Camera") || obj.name.Contains("Head"))
                    {
                        manager.Head = obj;
                        break;
                    }
                }
            }

            if (manager.LHand == null)
            {
                b = true;
                foreach (GameObject obj in objects)
                {
                    if (obj.name.Contains("Left") && (obj.name.Contains("Hand") || obj.name.Contains("Controller")))
                    {
                        manager.LHand = obj;
                        break;
                    }
                }
            }

            if (manager.RHand == null)
            {
                b = true;
                foreach (GameObject obj in objects)
                {
                    if (obj.name.Contains("Right") && (obj.name.Contains("Hand") || obj.name.Contains("Controller")))
                    {
                        manager.RHand = obj;
                        break;
                    }
                }
            }

            return b;
        }
#endif

        #endregion

        #region Player Handling

        private void UpdatePlayerList()
        {
            if (Runner == null)
            {
                Players.Clear();
                return;
            }

            Players.RemoveAll(p => p == null);

            var foundPlayers = new List<FusionVRPlusPlayer>();

            foreach (var playerRef in Runner.ActivePlayers)
            {
                if (Runner.TryGetPlayerObject(playerRef, out var netObj) && netObj != null)
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

        private System.Collections.IEnumerator WaitForPlayerObjectAndAdd(PlayerRef playerRef, float timeoutSeconds)
        {
            float elapsed = 0f;

            while (elapsed < timeoutSeconds)
            {
                if (Runner != null && Runner.TryGetPlayerObject(playerRef, out var netObj) && netObj != null)
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


        //TODO: add a rate limiter so people cant spam change name/color/cosmetic
        public static void SetPlayerName(string newName)
        {
            LocalPlayer.SetPlayerName(newName);
        }

        public static void SetPlayerColor(Color newColor)
        {
            LocalPlayer.SetPlayerColor(newColor);
        }
        
        public static void ToggleCosmetic(string cosName, string cosSlot)
        {
            LocalPlayer.ToggleCosmetic(cosName, cosSlot);
        }
        
        public static Color GetRandomColor() => new Color(
            UnityEngine.Random.value,
            UnityEngine.Random.value,
            UnityEngine.Random.value,
            1f
        );
        #endregion

        #region Error Handling
        void HandleShutdownReason(StartGameResult res)
        {
            switch (res.ShutdownReason)
            {
                case ShutdownReason.MaxCcuReached:
                    FusionVRPlusLogger.PrintMessage("Max CCU reached unable to join room", FusionVRPlus.Misc.MessageType.Error);
                    HandleMaxCCUReached();
                    break;
                case ShutdownReason.GameIsFull:
                    FusionVRPlusLogger.PrintMessage("room is full unable to join", FusionVRPlus.Misc.MessageType.Error);
                    break;
            }
        }

        private FusionVRPlusServer GetNextServer(FusionVRPlusServer CurrentServer)
        {
            foreach (var server in BackupServer)
            {
                if (server == CurrentServer) continue;

                if (!string.IsNullOrWhiteSpace(server.APPID) && !string.IsNullOrWhiteSpace(server.VOICEID))
                {
                    return server;
                }
            }

            return null;
        }

        async Task HandleMaxCCUReached()
        {
            FusionVRPlusLogger.PrintMessage("Attempting to handle max CCU reached", FusionVRPlus.Misc.MessageType.Info);

            var currentServer = new FusionVRPlusServer
            {
                APPID = PhotonAppSettings.Global.AppSettings.AppIdFusion,
                VOICEID = PhotonAppSettings.Global.AppSettings.AppIdVoice
            };

            var NextServer = GetNextServer(currentServer);

            if (NextServer == null)
            {
                FusionVRPlusLogger.PrintMessage("not backup server are available unable to switch server :(", FusionVRPlus.Misc.MessageType.Fatal);
            }

            PhotonAppSettings.Global.AppSettings.AppIdFusion = NextServer.APPID;
            PhotonAppSettings.Global.AppSettings.AppIdVoice = NextServer.VOICEID;

            FusionVRPlusLogger.PrintMessage("Found backup server: " + NextServer.APPID, FusionVRPlus.Misc.MessageType.Info);

            //removing old Network Runner before reconnecting
            Runner?.Shutdown();

            await ReconnectToLastRoom();
        }

        public async Task ReconnectToLastRoom()
        {
            if (string.IsNullOrEmpty(LastJoinedRoomName)) return;

            await ConnectToServer(LastJoinedRoomName, LastJoinedRoomPlayerCount, LastJoinedRoomIsPrivate);
        }
        #endregion

        #region Server Backups
        public FusionVRPlusServer GetBackupFromIndex(int index)
        {
            return BackupServer[index];
        }
        #endregion

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

                    var plrObject = runner.Spawn(Manager.PlayerPrefab, spawnPos, Quaternion.identity, player);
                    runner.SetPlayerObject(player, plrObject);

                }

                DespawnRig();
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
            SpawnOfflineRig();

            bool isNormalOrExpectedShutdown =
                shutdownReason == ShutdownReason.Ok ||
                shutdownReason == ShutdownReason.GameIsFull ||
                shutdownReason == ShutdownReason.MaxCcuReached;

            if (Manager.AutoReconnectOnError && !isNormalOrExpectedShutdown)
            {
                _ = Manager.ReconnectToLastRoom();
            }
        }

        void HandleDisconnect(NetDisconnectReason reason)
        {
            switch (reason)
            {
                case NetDisconnectReason.SendWindowFull:
                    FusionVRPlusLogger.PrintMessage("Client Might Be Spamming RPC or have ban Wifi.", FusionVRPlus.Misc.MessageType.Fatal);
                    break;
                case NetDisconnectReason.Timeout:
                    FusionVRPlusLogger.PrintMessage("Client Connection Timed out.", FusionVRPlus.Misc.MessageType.Error);
                    break;
                case NetDisconnectReason.SequenceOutOfBounds:
                    FusionVRPlusLogger.PrintMessage("Client is sending out of sync packets try replaying the game.", FusionVRPlus.Misc.MessageType.Error);
                    break;
                case NetDisconnectReason.ByRemote:
                    FusionVRPlusLogger.PrintMessage("Client was disconnected by a remote request.", FusionVRPlus.Misc.MessageType.Error);
                    break;
                case NetDisconnectReason.Unknown:
                    FusionVRPlusLogger.PrintMessage("Unknown error has been encountered so client was disconnected", FusionVRPlus.Misc.MessageType.Error);
                    break;
            }
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            HandleDisconnect(reason);
        }

        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
        {
            FusionVRPlusLogger.PrintMessage($"Incoming connect request from {request.RemoteAddress}.", FusionVRPlus.Misc.MessageType.Info);

            if (runner.ActivePlayers.Count() >= 10)
            {
                FusionVRPlusLogger.PrintMessage("Player attempted to join when room was full", FusionVRPlus.Misc.MessageType.Error);
                request.Refuse();
                return;
            }

            FusionVRPlusLogger.PrintMessage($"Player {request.RemoteAddress} was accepted", FusionVRPlus.Misc.MessageType.Info);
            request.Accept();
        }

        void HandleConnectionFailed(NetConnectFailedReason reason)
        {
            switch (reason)
            {
                case NetConnectFailedReason.Timeout:
                    FusionVRPlusLogger.PrintMessage("Server is not responding you may have been timedout try rejoining game", FusionVRPlus.Misc.MessageType.Error);
                    break;
                case NetConnectFailedReason.ServerFull:
                    FusionVRPlusLogger.PrintMessage("Server is currently full unable to join", FusionVRPlus.Misc.MessageType.Error);
                    break;
                case NetConnectFailedReason.ServerRefused:
                    FusionVRPlusLogger.PrintMessage("Server refused your connection attempt try agian later", FusionVRPlus.Misc.MessageType.Error);
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
                HeadPosition = Manager.Head.transform.position,
                HeadRotation = Manager.Head.transform.rotation,
                LeftHandPosition = Manager.LHand.transform.position,
                LeftHandRotation = Manager.LHand.transform.rotation,
                RightHandPosition = Manager.RHand.transform.position,
                RightHandRotation = Manager.RHand.transform.rotation
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
            FusionVRPlusLogger.PrintMessage("Session List Updated", FusionVRPlus.Misc.MessageType.Info);
        }

        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
        {
            FusionVRPlusLogger.PrintMessage("Custom Auth Response Recieved", FusionVRPlus.Misc.MessageType.Info);

            foreach(var item in data)
            {
                FusionVRPlusLogger.PrintMessage($"Key: {item.Key} Value: {item.Value}", FusionVRPlus.Misc.MessageType.Info);
            }

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