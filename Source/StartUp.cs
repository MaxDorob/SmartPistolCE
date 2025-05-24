using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SmartPistol
{
    [StaticConstructorOnStartup]
    public static class StartUp
    {
        static HarmonyLib.Harmony harmony;
        static StartUp()
        {
            harmony = new HarmonyLib.Harmony("SmartPistolCE");
            harmony.PatchAll();
        }
    }
}
