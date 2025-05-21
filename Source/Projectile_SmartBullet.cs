using CombatExtended;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SmartPistol
{
    public class Projectile_SmartBullet : BulletCE
    {
        public float targetHeight = 0f;
        protected override bool CanCollideWith(Thing thing, out float dist)
        {
            if (thing == intendedTargetThing && (thing.DrawPos - ExactPosition).MagnitudeHorizontalSquared() < 1.2f)
            {
                dist = 0f;
                return true;
            }
            return base.CanCollideWith(thing, out dist);
        }
    }
}
