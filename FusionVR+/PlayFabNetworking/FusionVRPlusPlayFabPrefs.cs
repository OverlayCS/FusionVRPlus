using Fusion;
using FusionVRPlus.Misc;
using PlayFab;
using PlayFab.ClientModels;
using System.Collections.Generic;
using UnityEngine;

namespace FusionVRPlus.PlayFabNetworking
{
    public static class FusionVRPlusPlayFabPrefs
    {
        private static Dictionary<string, string> saveData = new Dictionary<string, string>();

        public static bool CheckOverlap(string key, string value)
        {
            if (saveData.ContainsKey(key))
                return saveData[key] == value;
            else
                return false;
        }

        public static void Get()
        {
            if (!PlayFabSettings.staticPlayer.IsClientLoggedIn())
            {
                FusionVRPlusLogger.PrintMessage("Cant Get PlayerData As your not logged into playfab", MessageType.Error);
            }

            FusionVRPlusLogger.PrintMessage("Getting saved data", MessageType.Info);

            PlayFabClientAPI.GetUserData(new GetUserDataRequest()
            {
                Keys = null,
                PlayFabId = FusionVRPlusPlayFabManager.Instance.UserID,
            }, msg =>
            {
                foreach (KeyValuePair<string, UserDataRecord> kv in msg.Data)
                {
                    string str = kv.Value.Value;

                    FusionVRPlusLogger.PrintMessage($"{kv.Key} : {str}", MessageType.Info);

                    saveData[kv.Key] = str;
                }
            }, error =>
            {
                FusionVRPlusLogger.PrintMessage("Failed to get user data", MessageType.Error);
            });
        }

        public static void DeleteAll()
        {
            FusionVRPlusLogger.PrintMessage("You can't delete all user data for obvious reasons", MessageType.Error);
        }

        public static void DeleteKey(string key)
        {
            Debug.Log("Deleting key");

            PlayFabClientAPI.UpdateUserData(new UpdateUserDataRequest()
            {
                KeysToRemove = new List<string>() { key },
                Permission = UserDataPermission.Public
            }, msg => { FusionVRPlusLogger.PrintMessage("Deleted key", MessageType.Info); }, error => { FusionVRPlusLogger.PrintMessage("Failed to upload player data", MessageType.Error); });
        }

        public static float GetFloat(string key, float defaultValue)
        {
            if (saveData.ContainsKey(key))
            {
                if (float.TryParse(saveData[key], out float result))
                    return result;
            }

            return defaultValue;
        }

        public static float GetFloat(string key)
        {
            if (saveData.ContainsKey(key))
            {
                if (float.TryParse(saveData[key], out float result))
                    return result;
            }

            return 0;
        }

        public static int GetInt(string key, int defaultValue)
        {
            if (saveData.ContainsKey(key))
            {
                if (int.TryParse(saveData[key], out int result))
                    return result;
            }

            return defaultValue;
        }

        public static int GetInt(string key)
        {
            if (saveData.ContainsKey(key))
            {
                if (int.TryParse(saveData[key], out int result))
                    return result;
            }

            return 0;
        }

        public static string GetString(string key, string defaultValue)
        {
            if (saveData.ContainsKey(key))
                return saveData[key];

            return defaultValue;
        }

        public static string GetString(string key)
        {
            if (saveData.ContainsKey(key))
                return saveData[key];

            return "";
        }

        public static bool HasKey(string key)
        {
            return saveData.ContainsKey(key);
        }

        public static void Save()
        {
            FusionVRPlusLogger.PrintMessage("Saving data", MessageType.Info);

            PlayFabClientAPI.UpdateUserData(new UpdateUserDataRequest()
            {
                Data = saveData,
                Permission = UserDataPermission.Public
            }, msg => { FusionVRPlusLogger.PrintMessage("Saved data", MessageType.Info); }, error => { FusionVRPlusLogger.PrintMessage("Failed to upload data", MessageType.Error); });
        }

        public static void SetFloat(string key, float value)
        {
            if (!CheckOverlap(key, value.ToString()))
            {
                saveData[key] = value.ToString();

                FusionVRPlusLogger.PrintMessage("Setting float", MessageType.Info);

                PlayFabClientAPI.UpdateUserData(new UpdateUserDataRequest()
                {
                    Data = new Dictionary<string, string>()
                    {
                        { key, value.ToString() }
                    },
                    Permission = UserDataPermission.Public
                }, msg => { FusionVRPlusLogger.PrintMessage("Saved float", MessageType.Info); }, error => { FusionVRPlusLogger.PrintMessage("Failed to upload data", MessageType.Error); });
            }
            else
            {
                Debug.Log("No reason to upload as data is the same");
            }
        }

        public static void SetInt(string key, int value)
        {
            if (!CheckOverlap(key, value.ToString()))
            {
                saveData[key] = value.ToString();

                Debug.Log("Setting int");

                PlayFabClientAPI.UpdateUserData(new UpdateUserDataRequest()
                {
                    Data = new Dictionary<string, string>()
                    {
                        { key, value.ToString() }
                    },
                    Permission = UserDataPermission.Public
                }, msg => { FusionVRPlusLogger.PrintMessage("Saved int", MessageType.Info); }, error => { FusionVRPlusLogger.PrintMessage("Failed to upload data", MessageType.Info); });
            }
            else
            {
                Debug.Log("No reason to upload as data is the same");
            }
        }

        public static void SetString(string key, string value)
        {
            if (!CheckOverlap(key, value))
            {
                saveData[key] = value;

                FusionVRPlusLogger.PrintMessage("Setting string", MessageType.Info);

                PlayFabClientAPI.UpdateUserData(new UpdateUserDataRequest()
                {
                    Data = new Dictionary<string, string>()
                    {
                        { key, value }
                    },
                    Permission = UserDataPermission.Public
                }, msg => { FusionVRPlusLogger.PrintMessage("Saved string", MessageType.Info); }, error => { FusionVRPlusLogger.PrintMessage("Failed to upload data", MessageType.Error); });
            }
            else
            {
                FusionVRPlusLogger.PrintMessage("No reason to upload as data is the same", MessageType.Info);
            }
        }

        private static void Save(string key)
        {
            if (HasKey(key))
            {
                FusionVRPlusLogger.PrintMessage("Saving key", MessageType.Info);

                PlayFabClientAPI.UpdateUserData(new UpdateUserDataRequest()
                {
                    Data = new Dictionary<string, string>()
                    {
                        { key, saveData[key] }
                    },
                    Permission = UserDataPermission.Public
                }, msg => { FusionVRPlusLogger.PrintMessage("Saved key", MessageType.Info); }, error => { FusionVRPlusLogger.PrintMessage("Failed to upload data", MessageType.Error); });
            }
        }
    }
}
