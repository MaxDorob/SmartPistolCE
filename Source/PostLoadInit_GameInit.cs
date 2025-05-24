using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SmartPistol
{
    public class PostLoadInit_GameInit : GameComponent
    {
        public PostLoadInit_GameInit(Game game) : base()
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            foreach (var verb in Verb_LaunchProjectileSmart.allVerbs)
            {
                verb.PostLoadInitLockManager();
            }
        }
    }
}
