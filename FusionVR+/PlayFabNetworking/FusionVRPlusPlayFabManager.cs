using FusionVRPlus.Misc;
using PlayFab;
using PlayFab.ClientModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace FusionVRPlus.PlayFabNetworking
{
    public class FusionVRPlusPlayFabManager : MonoBehaviour
    {
        public static FusionVRPlusPlayFabManager Instance;

        [SerializeField] private string PlayFabTitleID;
        [SerializeField] private static string userID;
        [SerializeField] private static string sessionTicket;

        public List<TextMeshPro> BannedReasonText;
        public List<TextMeshPro> BannedLengthText;

        public string UserID { get { return userID; } }
        public string SessionTicket { get { return sessionTicket; } }

        private void Awake()
        {
            if(Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(Instance);
            }

            PlayFabSettings.TitleId = PlayFabTitleID;
        }

        private void Update()
        {
            
        }

        public static bool IsAccountBanned()
        {
            if (!PlayFabSettings.staticPlayer.IsEntityLoggedIn())
                FusionVRPlusLogger.PrintMessage("Unable To Check If Banned Because Your Not Logged Into Playfab", MessageType.Error);

            bool banned = false;

            PlayFabClientAPI.GetUserInventory(new GetUserInventoryRequest(), result =>
            {
                banned = false;
            }, error =>
            {
                if(error.Error == PlayFabErrorCode.AccountBanned)
                {
                    banned = true;
                }
            });

            return banned;
        }

        public void ConnectToPlayFab(string CustomID)
        {
            if (string.IsNullOrWhiteSpace(CustomID))
            {
                FusionVRPlusLogger.PrintMessage("No custom id :(", MessageType.Error);
                return;
            }

            LoginWithCustomIDRequest request = new LoginWithCustomIDRequest()
            {
                CustomId = CustomID,
                CreateAccount = true
            };

            PlayFabClientAPI.LoginWithCustomID(request, success =>
            {
                FusionVRPlusLogger.PrintMessage("Successfully Logged Into PlayFab", MessageType.Info);
                userID = success.PlayFabId;
                FusionVRPlusPlayFabPrefs.Get();
            }, error =>
            {
                FusionVRPlusLogger.PrintMessage("Failed to log into playfab: " + error.ErrorMessage, MessageType.Error);
                UpdateScene(error);
            });
        }

        public void UpdateScene(PlayFabError error)
        {
            foreach (var item in error.ErrorDetails)
            {
                int hoursLeft = 0;

                foreach (var BanTimeText in BannedLengthText)
                {
                    string playFabTime = item.Value[0];
                    if (playFabTime == "Indefinite")
                    {
                        BanTimeText.text = "Permanent Ban";
                        hoursLeft = int.MaxValue;
                    }
                    else if (DateTimeOffset.TryParse(playFabTime, CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto))
                    {
                        TimeSpan timeLeft = dto.UtcDateTime - DateTime.UtcNow;
                        hoursLeft = Mathf.Max(0, (int)Math.Ceiling(timeLeft.TotalHours));
                        BanTimeText.text = $"Hours Left: {hoursLeft}";
                    }
                    else
                    {
                        BanTimeText.text = "Unknown Ban Duration";
                        hoursLeft = 0;
                    }
                }

                foreach (var BanReason in BannedReasonText)
                    BanReason.text = $"Reason: {item.Key}";
            }
        }

        public List<ItemInstance> GetUserInventory()
        {
            List<ItemInstance> items = new List<ItemInstance>();

            GetUserInventoryRequest request = new GetUserInventoryRequest();

            PlayFabClientAPI.GetUserInventory(request, result =>
            {
                items = result.Inventory;
            }, error =>
            {
                FusionVRPlusLogger.PrintMessage("Failed to get user inventory: " + error.ErrorMessage, MessageType.Error);
                items = new List<ItemInstance>();
            });

            return items ?? new List<ItemInstance>();
        }

        public int GetUserVirtualCurrency(string currencyCode)
        {
            int balance = 0;

            GetUserInventoryRequest request = new GetUserInventoryRequest();
            PlayFabClientAPI.GetUserInventory(request, result =>
            {
                if (result.VirtualCurrency.TryGetValue(currencyCode, out int value))
                {
                    balance = value;
                }
                else
                {
                    balance = 0;
                }
            }, error =>
            {
                FusionVRPlusLogger.PrintMessage("Failed to get user inventory: " + error.ErrorMessage, MessageType.Error);
                balance = 0;
            });

            return balance;
        }

        private  Dictionary<string, string> GetTitleData()
        {
            return new Dictionary<string, string>();
        }
    }
}
