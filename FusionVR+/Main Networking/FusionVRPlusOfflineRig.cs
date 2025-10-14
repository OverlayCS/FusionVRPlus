using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace FusionVRPlus.Networking
{
    public class FusionVRPlusOfflineRig : MonoBehaviour
    {
        [SerializeField] private Transform Head;
        [SerializeField] private Transform LHand;
        [SerializeField] private Transform RHand;

        [SerializeField] public Renderer[] PlayerRenderers;
        [SerializeField] public TextMeshPro PlayernameText;

        void LocalUpdate()
        {
            Head.transform.SetPositionAndRotation(FusionVRPlusManager.Manager.Head.transform.position, FusionVRPlusManager.Manager.Head.transform.rotation);
            RHand.transform.SetPositionAndRotation(FusionVRPlusManager.Manager.RHand.transform.position, FusionVRPlusManager.Manager.RHand.transform.rotation);
            LHand.transform.SetPositionAndRotation(FusionVRPlusManager.Manager.LHand.transform.position, FusionVRPlusManager.Manager.LHand.transform.rotation);
        }

        private void Update()
        {
            if(FusionVRPlusManager.Manager != null)
            {
                LocalUpdate();
            }
        }
    }
}
