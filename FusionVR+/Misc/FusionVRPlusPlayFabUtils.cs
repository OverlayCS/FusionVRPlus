using PlayFab;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FusionVRPlus.Misc
{

    //WIP still working on
    public static class FusionVRPlusPlayFabUtils
    {
        public static bool IsLoggedIntoPlayFab()
        {
            return PlayFabSettings.staticPlayer.IsClientLoggedIn();
        }
    }
}
