using FusionVRPlus.Networking;
using FusionVRPlus.PlayFabNetworking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace FusionVRPlus.Saving
{
    public class FusionVRPlusSavingManager : MonoBehaviour
    {
        public static FusionVRPlusSavingManager Instance;

        public string UsernameKey = "Username";
        public string ColorKey = "Color";
        public string cosmeticsKey = "EquippedCosmetics";

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(Instance);
            }
        }

        #region Username
        public void SaveUsername(string username)
        {
            FusionVRPlusPlayFabPrefs.SetString(UsernameKey, username);
            PlayerPrefs.SetString(UsernameKey, username);
        }
        public string LoadNetworkedUsername()
        {
            if(FusionVRPlusPlayFabPrefs.HasKey(UsernameKey))
                return FusionVRPlusPlayFabPrefs.GetString(UsernameKey);
            else
                return LoadLocalUsername();
        }

        public string LoadLocalUsername()
        {
            if (PlayerPrefs.HasKey(UsernameKey))
                return PlayerPrefs.GetString(UsernameKey);
            else
                return "Player" + UnityEngine.Random.Range(1000, 9999);
        }
        #endregion

        #region Color
        public void SaveColor(Color color)
        {
            string colorString = JsonUtility.ToJson(color);
            FusionVRPlusPlayFabPrefs.SetString(ColorKey, colorString);
            PlayerPrefs.SetString(ColorKey, colorString);
        }

        public Color LoadNetworkedColor()
        {
            if (FusionVRPlusPlayFabPrefs.HasKey(ColorKey))
            {
                string colorString = FusionVRPlusPlayFabPrefs.GetString(ColorKey);
                return JsonUtility.FromJson<Color>(colorString);
            }
            else
            {
                return LoadLocalColor();
            }
        }

        public Color LoadLocalColor()
        {
            if (PlayerPrefs.HasKey(ColorKey))
            {
                string colorString = PlayerPrefs.GetString(ColorKey);
                return JsonUtility.FromJson<Color>(colorString);
            }
            else
            {
                return new Color(UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f), 1f);
            }
        }
        #endregion

        #region Cosmetics

        public List<int> DeserializeEquippedCosmetics(string data = null)
        {
            List<int> ids = new List<int>();

            if (data == null)
            {
                if (!PlayerPrefs.HasKey("EquippedCosmetics"))
                {
                    return new List<int>();
                }
                data = PlayerPrefs.GetString("EquippedCosmetics");
            }

            string[] parts = data.Split(',');
            foreach (string part in parts)
            {
                if (int.TryParse(part, out int id))
                {
                    ids.Add(id);
                }
            }

            return ids;
        }

        public void SaveCosmetics(string data)
        {
            FusionVRPlusPlayFabPrefs.SetString(cosmeticsKey, data);
            PlayerPrefs.SetString(cosmeticsKey, data);
        }

        public List<int> LoadNetworkedCosmetics()
        {
            if (FusionVRPlusPlayFabPrefs.HasKey(cosmeticsKey))
            {
                string data = FusionVRPlusPlayFabPrefs.GetString(cosmeticsKey);
                return DeserializeEquippedCosmetics(data);
            }
            else
            {
                return LoadLocalCosmetics();
            }
        }

        public List<int> LoadLocalCosmetics()
        {
            if (PlayerPrefs.HasKey(cosmeticsKey))
            {
                string data = PlayerPrefs.GetString(cosmeticsKey);
                return DeserializeEquippedCosmetics(data);
            }
            else
            {
                return new List<int>();
            }
        }

        public List<int> GetAllCosmeticIDs()
        {
            return DeserializeEquippedCosmetics(FusionVRPlusManager.LocalPlayer.SerializeEquippedCosmetics());
        }

        #endregion

    }
}
