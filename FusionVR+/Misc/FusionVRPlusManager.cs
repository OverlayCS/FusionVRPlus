using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using FusionVRPlus.Misc;
using FusionVRPlus.PlayFabNetworking;
using PlayFab;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Unity.VisualScripting;
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

    public class FusionVRPlusManager : MonoBehaviour
    {
        public static FusionVRPlusManager Manager;

        public FusionVRPlusServer MainServer;
        public FusionVRPlusServer[] BackupServer;

        [Header("Player Stuff")]
        public GameObject Head;
        public GameObject LHand;
        public GameObject RHand;

        [Header("Default Stuff")]
        public string DefaultPlayerName;
        public Color DefaultPlayerColor;

        [Header("Options")]
        public bool AutoConnect;
        public bool AutoReconnectOnError;
        public bool BlockNetworkingIfBanned;
        public bool AutoConnectToPlayFab = true;

        [Header("Prefabs")]
        public GameObject PlayerPrefab;

        [Header("Network Stuff")]
        public GameObject NetworkRunnerPrefab;
        public FusionVRPlusConnectionState ConnectionState;

        [Header("Dont Add Into This")]
        public string LastJoinedRoomName;
        public int LastJoinedRoomPlayerCount;
        public bool LastJoinedRoomIsPrivate;

        public static FusionVRPlusPlayer LocalPlayer;

        private void Start()
        {
            FusionVRPlusCoroutineManager.instance.StartCoroutine(DelayedAutoConnect());
        }

        IEnumerator DelayedAutoConnect()
        {
            FusionVRPlusPlayFabManager.Instance.ConnectToPlayFab(SystemInfo.deviceUniqueIdentifier);

            yield return new WaitForSeconds(2f);

            if (AutoConnect)
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
        public string GenerateRoomCode()
        {
            string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            System.Random random = new System.Random();
            return new string(Enumerable.Repeat(chars, 6).Select(s => s[random.Next(s.Length)]).ToArray());
        }

        public async Task ConnectToServer(string RoomName, int MaxPlayers, bool Private)
        {
            ConnectionState = FusionVRPlusConnectionState.Connecting;

            if (FusionVRPlusNetworkRunner.IsInRoom())
            {
                FusionVRPlusLogger.PrintMessage("Already in room unable to join", MessageType.Error);
                return;
            }

            if (!CheckForValidServer())
            {
                FusionVRPlusLogger.PrintMessage(
                    "Unable to find any valid servers. Please check the main and backup server list.",
                    MessageType.Fatal
                );
                return;
            }

            if (BlockNetworkingIfBanned && FusionVRPlusPlayFabManager.IsAccountBanned())
            {
                FusionVRPlusLogger.PrintMessage("Account Is Banned So You Cant Join Rooms", MessageType.Error);
                return;
            }

            FusionVRPlusNetworkRunner.SetUpRunner();

            StartGameResult res = await FusionVRPlusNetworkRunner.Runner.StartGame(new StartGameArgs()
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
                FusionVRPlusLogger.PrintMessage($"Failed To Join Room Error: {res.ShutdownReason}", MessageType.Error);
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
            if(FusionVRPlusNetworkRunner.Runner == null)
            {
                return;
            }

            ConnectionState = FusionVRPlusConnectionState.Disconnecting;
            FusionVRPlusNetworkRunner.Runner.Shutdown();
            Destroy(FusionVRPlusNetworkRunner.tempRunnerObject);
            FusionVRPlusNetworkRunner.Runner = null;
            FusionVRPlusNetworkRunner.tempRunnerObject = null;
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
                MessageType.Error
            );

            foreach(var server in BackupServer)
            {
                if(string.IsNullOrWhiteSpace(server.APPID) || string.IsNullOrWhiteSpace(server.VOICEID))
                {
                    FusionVRPlusLogger.PrintMessage(
                        $"Backup server has missing AppID or VoiceID. Skipping...",
                        MessageType.Warning
                    );
                    continue;
                }

                PhotonAppSettings.Global.AppSettings.AppIdFusion = server.APPID;
                PhotonAppSettings.Global.AppSettings.AppIdVoice = server.VOICEID;


                FusionVRPlusLogger.PrintMessage(
                    $"Switched to backup server with AppID {server.APPID}.",
                    MessageType.Info
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

        /// <summary>
        /// this is used for the session list updating its not for joining rooms
        /// </summary>
        public void ConnectToLobby()
        {
            FusionVRPlusNetworkRunner.SetUpRunner();
            FusionVRPlusNetworkRunner.Runner.JoinSessionLobby(SessionLobby.Shared);
        }
        #endregion

        #region Player Handling

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
                    FusionVRPlusLogger.PrintMessage("Max CCU reached unable to join room", MessageType.Error);
                    HandleMaxCCUReached();
                    break;
                case ShutdownReason.GameIsFull:
                    FusionVRPlusLogger.PrintMessage("room is full unable to join", MessageType.Error);
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
            FusionVRPlusLogger.PrintMessage("Attempting to handle max CCU reached", MessageType.Info);

            var currentServer = new FusionVRPlusServer
            {
                APPID = PhotonAppSettings.Global.AppSettings.AppIdFusion,
                VOICEID = PhotonAppSettings.Global.AppSettings.AppIdVoice
            };

            var NextServer = GetNextServer(currentServer);

            if (NextServer == null)
            {
                FusionVRPlusLogger.PrintMessage("not backup server are available unable to switch server :(", MessageType.Fatal);
            }

            PhotonAppSettings.Global.AppSettings.AppIdFusion = NextServer.APPID;
            PhotonAppSettings.Global.AppSettings.AppIdVoice = NextServer.VOICEID;

            FusionVRPlusLogger.PrintMessage("Found backup server: " + NextServer.APPID, MessageType.Info);

            //removing old Network Runner before reconnecting
            FusionVRPlusNetworkRunner.Runner?.Shutdown();

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
    }

}
