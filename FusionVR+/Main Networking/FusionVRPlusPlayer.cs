using Fusion;
using FusionVRPlus.Misc;
using FusionVRPlus.PlayFabNetworking;
using FusionVRPlus.Saving;
using PlayFab;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace FusionVRPlus.Networking
{
    [System.Serializable]
    public class CullItem
    {
        public GameObject ParentObject;
        public bool IncludeChildrenObjects;
    }

    [System.Serializable]
    public class CosmeticSlot
    {
        public string SlotName;
        public List<Cosmetic> Cosmetics;
    }

    [System.Serializable]
    public class Cosmetic
    {
        public string Name;
        [Header("MUST BE DIFFERENT FOR EACH COSMETIC \n EVEN OTHER ONES IN DIFFERNT SLOTS")]
        public int ID;
        public GameObject Object;
    }

    public class FusionVRPlusPlayer : NetworkBehaviour
    {
        public bool InOfflineMode = false;

        public bool IsLocalPlayer => Object.InputAuthority == FusionVRPlusManager.Runner.LocalPlayer;

        [Networked, SerializeField] private string NetUsername { get; set; } // Used only in the player script
        [Networked] private string userID { get; set; } // Used for PlayFab
        public string UserID => userID;

        public string username; // Used for other players & saving (Fusion limitation workaround)
        [Networked, SerializeField] private Color NetPlayerColor { get; set; }
        public Color playerColor;

        [Header("UI References")]
        [SerializeField] private TextMeshPro UsernameText;
        [SerializeField] private TextMeshPro PlayerIDText; // Only shows first 6 characters
        [SerializeField] private TextMeshPro PlayerColorText;

        [Header("Renderers")]
        [SerializeField] private Renderer[] PlayerRenderers;

        [Header("Cosmetics")]
        [Networked, Capacity(12)] private NetworkLinkedList<int> equippedCosmetics => default; // Networked cosmetics
        [SerializeField] private List<Cosmetic> EquippedCosmetics = new(); // Local reference
        [SerializeField] private List<CosmeticSlot> CosmeticSlots = new(); // Local cosmetic slots

        [Header("Player Body References")]
        [SerializeField] private Transform Head;
        [SerializeField] private Transform LHand;
        [SerializeField] private Transform RHand;

        [Header("Networked Transforms")]
        [Networked] private Vector3 NetHeadPos { get; set; }
        [Networked] private Quaternion NetHeadRot { get; set; }
        [Networked] private Vector3 NetLeftPos { get; set; }
        [Networked] private Quaternion NetLeftRot { get; set; }
        [Networked] private Vector3 NetRightPos { get; set; }
        [Networked] private Quaternion NetRightRot { get; set; }

        [Header("Smoothing (Higher the number the less smoothing)")]
        [SerializeField] private float smoothFactor = 5f;
        private Vector3 smoothedHeadPos;
        private Quaternion smoothedHeadRot;
        private Vector3 smoothedLeftPos;
        private Quaternion smoothedLeftRot;
        private Vector3 smoothedRightPos;
        private Quaternion smoothedRightRot;

        [Header("Required For Debug Mode")]
        [SerializeField] private TextMeshPro HeadPosText;
        [SerializeField] private TextMeshPro RHandPosText;
        [SerializeField] private TextMeshPro LHandPosText;
        public bool DebugMode;

        public ChangeDetector changeDetector;


        public override void Spawned()
        {
            changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState, false);

            if(IsLocalPlayer)
            {
                var savedName = FusionVRPlusSavingManager.Instance.LoadNetworkedUsername();
                var savedColor = FusionVRPlusSavingManager.Instance.LoadNetworkedColor();
                var savedCosmetics = FusionVRPlusSavingManager.Instance.LoadNetworkedCosmetics();

                NetUsername = savedName;
                NetPlayerColor = savedColor;

                foreach(var i in savedCosmetics)
                {
                    equippedCosmetics.Add(i);
                    EquippedCosmetics.Add(GetCosmeticFromID(i));
                }

                UpdatePlayerName();
                UpdatePlayerColor();
                UpdateCosmeticVisuals();

                //getting ids first 6 characters                              ⬇ i hated this
                string FirstSix = FusionVRPlusPlayFabManager.Instance.UserID[..Math.Min(6, FusionVRPlusPlayFabManager.Instance.UserID.Length)];
                PlayerIDText.text = $"ID: {FirstSix}";

                userID = FirstSix;

                FusionVRPlusManager.LocalPlayer = this;
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            FusionVRPlusSavingManager.Instance.SaveUsername(username);
            FusionVRPlusSavingManager.Instance.SaveColor(playerColor);
            FusionVRPlusSavingManager.Instance.SaveCosmetics(SerializeEquippedCosmetics());
        }

        public string SerializeEquippedCosmetics()
        {
            List<int> tempList = new List<int>();

            foreach(var id in equippedCosmetics)
            {
                tempList.Add(id);
            }

            return string.Join(",", tempList);
        }

        public void DeserializeEquippedCosmetics(string data)
        {
            //TODO: scan for owned cosmetics and make sure they own them

            equippedCosmetics.Clear();

            if (string.IsNullOrEmpty(data))
            {
                return;
            }

            string[] parts = data.Split(',');
            foreach(string part in parts)
            {
                if (int.TryParse(part, out int id))
                {
                    equippedCosmetics.Add(id);
                    EquippedCosmetics.Add(GetCosmeticFromID(id));
                }
            }
        }

        void SaveCosmetics()
        {
            var data = new Dictionary<string, string>()
            {
                {"EquippedCosmetics", SerializeEquippedCosmetics()}
            };

            PlayFabClientAPI.UpdateUserData(new PlayFab.ClientModels.UpdateUserDataRequest { Data = data}, success =>
            {
                FusionVRPlusLogger.PrintMessage("Saved Cosmetics Successfully", Misc.MessageType.Info);
            }, error =>
            {
                FusionVRPlusLogger.PrintMessage("failed to save cosmetics", Misc.MessageType.Error);
            });
        }

        public override void Render()
        {
            if(InOfflineMode) return;

            if (IsLocalPlayer)
            {
                LocalUpdate();
            }
            else
            {
                NetworkUpdate();
            }

            foreach(var change in changeDetector.DetectChanges(this))
            {
                switch (change)
                {
                    case nameof(NetUsername):
                        UpdatePlayerName();
                        break;
                    case nameof(NetPlayerColor):
                        UpdatePlayerColor();
                        break;
                }
            }

            UpdateCosmeticVisuals();
        }

        public override void FixedUpdateNetwork()
        {
            if(InOfflineMode)return;

            if (!IsLocalPlayer) return;

            if (GetInput(out FusionVRPlusInput input))
            {
                NetHeadPos = input.HeadPosition;
                NetHeadRot = input.HeadRotation;
                NetLeftPos = input.LeftHandPosition;
                NetLeftRot = input.LeftHandRotation;
                NetRightPos = input.RightHandPosition;
                NetRightRot = input.RightHandRotation;
            }
        }

        public void Update()
        {
            if(InOfflineMode)return;

            //doing debug mode stuff idk
            PlayerColorText.gameObject.SetActive(DebugMode);
            PlayerIDText.gameObject.SetActive(DebugMode);
            HeadPosText.gameObject.SetActive(DebugMode);
            RHandPosText.gameObject.SetActive(DebugMode);
            LHandPosText.gameObject.SetActive(DebugMode);

            RHandPosText.text = "Right Hand: " + NetRightPos.ToString();
            LHandPosText.text = "left Hand: " + NetLeftPos.ToString();
            HeadPosText.text =  "Head: " + NetHeadPos.ToString();
        }

        private void UpdateCosmeticVisuals()
        {
            var equippedSet = new HashSet<int>(equippedCosmetics);

            foreach (var slot in CosmeticSlots)
            {
                foreach (var cos in slot.Cosmetics)
                {
                    bool shouldBeActive = equippedSet.Contains(cos.ID);
                    if (cos.Object.activeSelf != shouldBeActive)
                    {
                        cos.Object.SetActive(shouldBeActive);
                    }
                }
            }
        }


        void UpdatePlayerColor()
        {
            playerColor = NetPlayerColor;
            playerColor.a = 255;

            float r = Mathf.Clamp(playerColor.r * 10f, 1f, 10f);
            float g = Mathf.Clamp(playerColor.g * 10f, 1f, 10f);
            float b = Mathf.Clamp(playerColor.b * 10f, 1f, 10f);

            PlayerColorText.text = $"({r:F1}, {g:F1}, {b:F1})";
            PlayerColorText.color = playerColor;

            foreach(var ren in PlayerRenderers)
            {
                ren.material.color = playerColor;
            }
        }

        void UpdatePlayerName()
        {
            username = NetUsername;
            UsernameText.text = NetUsername;
        }

        public void SetPlayerColor(Color newColor)
        {
            if (!IsLocalPlayer)
            {
                FusionVRPlusLogger.PrintMessage("You Cant Set Other Players Color Due To Security", FusionVRPlus.Misc.MessageType.Fatal);
                return;
            }

            NetPlayerColor = newColor;
        }

        public void SetPlayerName(string newName)
        {
            if(!IsLocalPlayer)
            {
                FusionVRPlusLogger.PrintMessage("You Cant Set Other Players Name Due To Security", FusionVRPlus.Misc.MessageType.Fatal);
                return;
            }

            NetUsername = newName;
        }

        public void ToggleCosmetic(string cosName, string cosSlot)
        {
            if (!IsLocalPlayer)
            {
                FusionVRPlusLogger.PrintMessage("You Cant Set Other Players Cosmetics Due To Security", FusionVRPlus.Misc.MessageType.Fatal);
                return;
            }

            string targetName = cosName.ToLower();
            string targetSlot = cosSlot.ToLower();
            bool offline = targetName == "offline";

            foreach (CosmeticSlot slot in CosmeticSlots)
            {
                if (slot.SlotName.ToLower() != targetSlot && !offline)
                    continue;

                for (int i = 0; i < slot.Cosmetics.Count; i++)
                {
                    var cosmetic = slot.Cosmetics[i];

                    if (cosmetic.Name.ToLower() == targetName)
                    {
                        int cosmeticID = cosmetic.ID;

                        if (equippedCosmetics.Contains(cosmeticID))
                        {
                            equippedCosmetics.Remove(cosmeticID);
                            EquippedCosmetics.Remove(cosmetic);
                            Debug.Log($"Unequipped cosmetic ID: {cosmeticID}");
                            return;
                        }

                        for (int j = EquippedCosmetics.Count - 1; j >= 0; j--)
                        {
                            Cosmetic equipped = EquippedCosmetics[j];
                            if (IsCosmeticInSlot(equipped, slot))
                            {
                                EquippedCosmetics.RemoveAt(j);
                                RemoveFromNetworkedList(equippedCosmetics, equipped.ID);
                                Debug.Log($"Removed previous cosmetic in slot: {slot.SlotName}");
                            }
                        }

                        equippedCosmetics.Add(cosmeticID);
                        EquippedCosmetics.Add(cosmetic);
                        Debug.Log($"Equipped cosmetic ID: {cosmeticID}");
                        return;
                    }
                }
            }
        }

        public Cosmetic GetCosmeticFromID(int id)
        {
            foreach (CosmeticSlot slot in CosmeticSlots)
            {
                foreach (Cosmetic cosmetic in slot.Cosmetics)
                {
                    if (cosmetic.ID == id)
                        return cosmetic;
                }
            }

            return null;
        }

        private bool IsCosmeticInSlot(Cosmetic cosmetic, CosmeticSlot slot)
        {
            foreach (Cosmetic c in slot.Cosmetics)
            {
                if (c.ID == cosmetic.ID)
                    return true;
            }
            return false;
        }

        private void RemoveFromNetworkedList(NetworkLinkedList<int> list, int value)
        {
            foreach (var item in list)
            {
                if (item == value)
                {
                    list.Remove(item);
                    return;
                }
            }
        }

        void LocalUpdate()
        {
            Head.transform.SetPositionAndRotation(FusionVRPlusManager.Manager.Head.transform.position, FusionVRPlusManager.Manager.Head.transform.rotation);
            RHand.transform.SetPositionAndRotation(FusionVRPlusManager.Manager.RHand.transform.position, FusionVRPlusManager.Manager.RHand.transform.rotation);
            LHand.transform.SetPositionAndRotation(FusionVRPlusManager.Manager.LHand.transform.position, FusionVRPlusManager.Manager.LHand.transform.rotation);
        }
        
        void NetworkUpdate()
        {
            smoothedHeadPos = Vector3.Lerp(smoothedHeadPos, NetHeadPos, Time.deltaTime * smoothFactor);
            smoothedHeadRot = Quaternion.Slerp(smoothedHeadRot, NetHeadRot, Time.deltaTime * smoothFactor);

            smoothedLeftPos = Vector3.Lerp(smoothedLeftPos, NetLeftPos, Time.deltaTime * smoothFactor);
            smoothedLeftRot = Quaternion.Slerp(smoothedLeftRot, NetLeftRot, Time.deltaTime * smoothFactor);

            smoothedRightPos = Vector3.Lerp(smoothedRightPos, NetRightPos, Time.deltaTime * smoothFactor);
            smoothedRightRot = Quaternion.Slerp(smoothedRightRot, NetRightRot, Time.deltaTime * smoothFactor);

            Head.SetPositionAndRotation(smoothedHeadPos, smoothedHeadRot);
            LHand.SetPositionAndRotation(smoothedLeftPos, smoothedLeftRot);
            RHand.SetPositionAndRotation(smoothedRightPos, smoothedRightRot);

        }

    }
}
