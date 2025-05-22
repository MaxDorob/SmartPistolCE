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
                targetPos = targetPos.WithY(smartBullet.TargetHeight);
            }
            var maxSpeedGainOnDistance = 8f;
            var distanceToTarget = (projectile.ExactPosition - targetPos).magnitude;
            var speedGain = projectile.Props.speedGain * Mathf.Pow(Mathf.Min(maxSpeedGainOnDistance / distanceToTarget, 1f), 2.5f);
            var delta = targetPos - projectile.ExactPosition;
            var angle = Vector3.Angle(projectile.velocity, delta);
            if (angle > 80f)
            {
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
