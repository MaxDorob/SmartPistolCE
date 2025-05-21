using CombatExtended;
using HarmonyLib;
using RB_SmartPistol;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace SmartPistol
{
    public class Verb_LaunchProjectileSmart : CombatExtended.Verb_ShootCE
    {
        [HarmonyLib.HarmonyPatch(typeof(Verb_LaunchProjectileCE), nameof(Verb_LaunchProjectileCE.TryCastShot))]
        internal static class InsertBeforeProjectileLaunch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var insList = instructions.ToList();
                var spawnProjectileMethod = AccessTools.Method("CombatExtended.Verb_LaunchProjectileCE:SpawnProjectile");
                bool patched = false;
                var targetIns = instructions.Last(x => x.StoresField(AccessTools.Field(typeof(ProjectileCE), nameof(ProjectileCE.AccuracyFactor))));
                var loadOperand = insList[insList.FirstIndexOf(x => x.Calls(spawnProjectileMethod)) + 1].operand;
                foreach (var instruction in instructions)
                {
                    yield return instruction;
                    if (!patched && instruction == targetIns)
                    {
                        yield return new CodeInstruction(OpCodes.Ldarg_0);
                        yield return new CodeInstruction(OpCodes.Ldloc_S, loadOperand);
                        yield return CodeInstruction.Call(typeof(InsertBeforeProjectileLaunch), nameof(BeforeProjectileLaunch));
                        patched = true;
                        Log.Message("Pathced!");
                    }
                }
                if (!patched)
                {
                    Log.Error("Patching failed");
                }
            }
            internal static void BeforeProjectileLaunch(Verb_LaunchProjectileCE verb, ProjectileCE projectile)
            {
                if (verb is Verb_LaunchProjectileSmart ourVerb)
                {
                    ourVerb.BeforeProjectileLaunch(projectile);
                }
            }
        }
        private TargetLockManager lockManager;
        private LocalTargetInfo lockOn;

        private float HalfLockAngle
        {
            get
            {
                return EquipmentSource?.def?.GetModExtension<ModExt_SmartPistolSettings>()?.halfLockAngle ?? 35f;
            }
        }

        protected /*override*/ void BeforeProjectileLaunch(ProjectileCE projectile)
        {
            if (lockManager.IsTargetSatisfied(lockOn))
            {
                lockOn = lockManager.GetNextTarget();
            }
            Log.Warning($"Locked on target: {lockOn}");
            projectile.intendedTarget = lockOn;
            if (projectile is Projectile_SmartBullet smartBullet)
            {
                var report = ShiftVecReportFor(lockOn, lockOn.Cell);
                smartBullet.targetHeight = GetTargetHeight(report.target, report.cover, report.roofed, lockOn.Cell.ToVector3Shifted());
            }

        }
        public override bool TryCastShot()
        {
            var result = base.TryCastShot();
            if (result)
            {
                lockManager.RegisterShot(lockOn);
            }
            return result;
        }

        public override void WarmupComplete()
        {
            SmartPistolDef.RBSmartPistol_StartCast.PlayOneShot(new TargetInfo(this.CasterPawn.Position, this.CasterPawn.Map, false));
            lockManager = new TargetLockManager(CasterPawn, this, Projectile, false, HalfLockAngle, verbProps.burstShotCount, (Caster.TrueCenter() - currentTarget.Cell.ToVector3Shifted()).normalized);
            lockManager.Initialize(currentTarget);
            lockOn = currentTarget;
            base.WarmupComplete();
        }

        public override float GetTargetHeight(LocalTargetInfo target, Thing cover, bool roofed, Vector3 targetLoc)
        {
            float targetHeight = 0f;

            var coverRange = new CollisionVertical(cover).HeightRange;   //Get " " cover, assume it is the edifice

            // Projectiles with flyOverhead target the surface in front of the target


            var victimVert = new CollisionVertical(lockOn.Thing);
            var targetRange = victimVert.HeightRange;   //Get lower and upper heights of the target
            if (targetRange.min < coverRange.max)   //Some part of the target is hidden behind some cover
            {
                // - It is possible for targetRange.max < coverRange.max, technically, in which case the shooter will never hit until the cover is gone.
                // - This should be checked for in LoS -NIA
                targetRange.min = coverRange.max;

                // Target fully hidden, shift aim upwards if we're doing suppressive fire
                if (targetRange.max <= coverRange.max && (CompFireModes?.CurrentAimMode == AimMode.SuppressFire || VerbPropsCE.ignorePartialLoSBlocker))
                {
                    targetRange.max = coverRange.max * 2;
                }
            }
            else if (lockOn.Thing is Pawn Victim)
            {

                targetRange.min = victimVert.MiddleHeight;
                targetRange.max = victimVert.Max;
                targetHeight = targetRange.RandomInRange;
                if (projectilePropsCE != null)
                {
                    targetHeight += projectilePropsCE.aimHeightOffset;
                }
                if (targetHeight > CollisionVertical.WallCollisionHeight && roofed)
                {
                    targetHeight = CollisionVertical.WallCollisionHeight;
                }
            }
            Log.Message($"{lockOn.Thing} {victimVert.Min} - {victimVert.Max} = {targetHeight}");
            return targetHeight;
        }
        public override int ShotsPerBurst
        {
            get
            {
                if (lockManager != null)
                {
                    return lockManager.ShotsNeeded.Sum(x => x.Value);
                }
                Log.Error($"lock manager was null");
                return base.ShotsPerBurst;
            }
        }
        public override void Reset()
        {
            base.Reset();
            lockManager = null;
        }


    }


}
