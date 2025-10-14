using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace FusionVRPlus.Misc
{
    public class FusionVRPlusCoroutineManager : MonoBehaviour
    {
        public static FusionVRPlusCoroutineManager instance;

        private void Awake() =>
            instance = this;

        public static Coroutine RunCoroutine(IEnumerator enumerator) =>
            instance.StartCoroutine(enumerator);

        public static void EndCoroutine(Coroutine enumerator) =>
            instance.StopCoroutine(enumerator);
    }
}
