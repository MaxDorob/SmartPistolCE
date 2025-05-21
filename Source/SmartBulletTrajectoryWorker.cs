using CombatExtended;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace SmartPistol
{
    public class SmartBulletTrajectoryWorker : SmartRocketTrajectoryWorker
    {
        protected override void ReactiveAcceleration(ProjectileCE projectile)
        {
            LocalTargetInfo currentTarget = projectile.intendedTarget;
            if (currentTarget.ThingDestroyed || !currentTarget.HasThing)
            {
                base.ReactiveAcceleration(projectile);
                return;
            }
            var targetPos = currentTarget.Thing?.DrawPos ?? currentTarget.Cell.ToVector3Shifted();
            if (projectile is Projectile_SmartBullet smartBullet)
            {
                targetPos = targetPos.WithY(smartBullet.targetHeight);
            }
            var ticksBeforeMaxSpeedGain = 23f;
            var speedGain = projectile.Props.speedGain * Mathf.Min(projectile.FlightTicks / ticksBeforeMaxSpeedGain, 1f);
            var delta = targetPos - projectile.ExactPosition;
            var angle = projectile.velocity.AngleToFlat(delta);
            if (angle > 140f)
            {
                Log.Message($"Angle between {delta} and {projectile.velocity} is {angle}");
                return;
            }

            var newVelocity = projectile.velocity;
            newVelocity += delta.normalized *  speedGain / GenTicks.TicksPerRealSecond;
            var currentSpeed = newVelocity.magnitude * GenTicks.TicksPerRealSecond;
            newVelocity = newVelocity.normalized * Mathf.Min(currentSpeed, projectile.Props.speed) / GenTicks.TicksPerRealSecond;
            projectile.velocity = newVelocity;
        }
    }
}
