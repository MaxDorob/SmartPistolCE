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
        private IEnumerable<Verb_LaunchProjectileSmart> Verbs
        {
            get
            {
                if (launcher is Pawn pawn && pawn.equipment != null)
                {
                    foreach (var verb in pawn.equipment.AllEquipmentVerbs.OfType<Verb_LaunchProjectileSmart>())
                    {
                        yield return verb;
                    }
                }
                if (launcher is IVerbOwner verbOwner && verbOwner.VerbTracker != null)
                {
                    foreach (var verb in verbOwner.VerbTracker.AllVerbs.OfType<Verb_LaunchProjectileSmart>())
                    {
                        yield return verb;
                    }
                }
            }
        }
        public float TargetHeight
        {
            get
            {
                if (intendedTargetThing != null && intendedTargetThing.Spawned)
                {
                    return new CollisionVertical(intendedTargetThing).HeightRange.LerpThroughRange(0.98f);
                }
                return 0f;
            }
        }
        protected override bool CanCollideWith(Thing thing, out float dist)
        {
            if (thing == intendedTargetThing && (thing.DrawPos - ExactPosition).MagnitudeHorizontalSquared() < 1.2f)
            {
                dist = 0f;
                return true;
            }
            return base.CanCollideWith(thing, out dist);
        }
        public override void Impact(Thing hitThing)
        {
            foreach (var verb in Verbs)
            {
                verb.OnImpact(this);
            }
            base.Impact(hitThing);
        }
    }
}
