﻿using CombatExtended;
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
        internal static List<Verb_LaunchProjectileSmart> allVerbs = new List<Verb_LaunchProjectileSmart>();
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

        public Verb_LaunchProjectileSmart() : base()
        {
            allVerbs.Add(this);
        }

        private bool allowNeutral = false;
        private TargetLockManager lockManager;
        private LocalTargetInfo lockOn;
        private Vector3 mainDirNormalized;
        private int burstShotCountSaved = 0;
        private ThingDef usedProjectileDef = null;
        private float usedHalfLockAngle = 35f;
        private List<ProjectileCE> projectilesInFlight = new List<ProjectileCE>();
        private float HalfLockAngle
        {
            get
            {
                return EquipmentSource?.def?.GetModExtension<ModExt_SmartPistolSettings>()?.halfLockAngle ?? 35f;
            }
        }
        protected bool AllowNeutral => allowNeutral;

        protected /*override*/ void BeforeProjectileLaunch(ProjectileCE projectile)
        {
            if (lockManager.IsTargetSatisfied(lockOn))
            {
                lockOn = lockManager.GetNextTarget();
            }
            projectile.intendedTarget = lockOn;
            if (projectile is Projectile_SmartBullet smartBullet)
            {
                projectilesInFlight.Add(smartBullet);
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
            InitializeLocks();
            projectilesInFlight.Clear();
            base.WarmupComplete();
        }
        public override void OrderForceTarget(LocalTargetInfo target)
        {
            allowNeutral = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            base.OrderForceTarget(target);
        }
        private void InitializeLocks()
        {
            if (currentTarget.HasThing)
            {
                this.DestroyAllLockMotes();
                if (currentTarget.HasThing)
                {
                    Vector3 drawPos = this.CasterPawn.DrawPos;
                    Pawn pawn = this.currentTarget.Thing as Pawn;
                    Vector3 a = (pawn != null) ? pawn.TrueCenter() : this.currentTarget.Thing.DrawPos;
                    mainDirNormalized = (a - drawPos).normalized;
                    burstShotCountSaved = Mathf.Min(this.verbProps.burstShotCount, CompAmmo.CurMagCount);
                    usedProjectileDef = Projectile;
                    usedHalfLockAngle = HalfLockAngle;
                    this.lockManager = new TargetLockManager(this.CasterPawn, this, usedProjectileDef, AllowNeutral, this.usedHalfLockAngle, burstShotCountSaved, mainDirNormalized);
                    this.lockManager.Initialize(this.currentTarget);
                    lockOn = currentTarget;
                    Pawn pawn2 = this.currentTarget.Pawn;
                    //this.initialMainWasDowned = (pawn2 != null && pawn2.Downed);
                    foreach (LocalTargetInfo targ in this.lockManager.LockedTargets)
                    {
                        this.SpawnLockMote(targ);
                    }
                }
            }
        }
        public override float GetTargetHeight(LocalTargetInfo target, Thing cover, bool roofed, Vector3 targetLoc)
        {
            float targetHeight = 0f;

            var coverRange = new CollisionVertical(cover).HeightRange;   //Get " " cover, assume it is the edifice

            // Projectiles with flyOverhead target the surface in front of the target


            var victimVert = new CollisionVertical(lockOn.Thing);
            var targetRange = victimVert.HeightRange;   //Get lower and upper heights of the target
            targetHeight = victimVert.HeightRange.LerpThroughRange(0.98f);
            return targetHeight;
        }
        public override int ShotsPerBurst
        {
            get
            {
                if (lockManager != null)
                {
                    return Mathf.Min(lockManager.ShotsNeeded.Sum(x => x.Value), CompAmmo.CurMagCount);
                }
                Log.Error($"lock manager was null");
                return base.ShotsPerBurst;
            }
        }
        public override void Reset()
        {
            base.Reset();
            DestroyAllLockMotes();
            lockManager = null;
            allowNeutral = false;
            burstShotCountSaved = 0;
            usedProjectileDef = null;
        }
        public void OnProjectileDestroyed(ProjectileCE projectile)
        {
            if (projectilesInFlight.Contains(projectile))
            {
                projectilesInFlight.Remove(projectile);
                if (targetMotes.ContainsKey(projectile.intendedTarget) && !projectilesInFlight.Any(x => x.intendedTarget == projectile.intendedTarget))
                {
                    if (!targetMotes[projectile.intendedTarget].Destroyed)
                    {
                        targetMotes[projectile.intendedTarget].Destroy();
                    }
                    targetMotes.Remove(projectile.intendedTarget);
                }
            }
        }

        private List<Vector3> bezierLabelWorldPositions = new List<Vector3>();
        private bool IsInvalid(LocalTargetInfo targ)
        {
            if (!targ.HasThing || !targ.Thing.Spawned)
            {
                return true;
            }
            else
            {

                if (targ.Pawn != null)
                {
                    if (targ.Pawn.Dead)
                    {
                        return true;
                    }
                    if (targ.Pawn.Downed)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public override void DrawHighlight(LocalTargetInfo mainTarget)
        {
            base.DrawHighlight(mainTarget);
            Pawn pawn = mainTarget.Thing as Pawn;
            if (pawn != null)
            {
                if (!IsInvalid(mainTarget))
                {
                    Pawn casterPawn = this.CasterPawn;
                    Vector3 drawPos = casterPawn.DrawPos;
                    Faction faction = casterPawn.Faction;
                    Vector3 a = pawn.TrueCenter();
                    Vector3 normalized = (a - drawPos).normalized;
                    Vector3 vector = pawn.TrueCenter();
                    float y = AltitudeLayer.MapDataOverlay.AltitudeFor();
                    Material targetHighlightMat = SmartPistolMaterials.GetTargetHighlightMat(pawn, faction);
                    Matrix4x4 matrix = Matrix4x4.TRS(new Vector3(vector.x, y, vector.z), Quaternion.identity, new Vector3(1.2f, 1f, 1.2f));
                    Graphics.DrawMesh(MeshPool.plane10, matrix, targetHighlightMat, 0);
                    float num = Vector3.Distance(drawPos, pawn.DrawPos);
                    Vector3 control = drawPos + normalized * (num * 0.6f);
                    BezierUtil.DrawQuadraticCurve(drawPos, control, pawn.DrawPos, SmartPistolMaterials.SmartPistolBlueLineMat, 0.1f, 0.05f, 20);
                    float t = 0.6f;
                    Vector3 point = BezierUtil.GetPoint(drawPos, control, pawn.DrawPos, t);
                    this.bezierLabelWorldPositions.Add(point + Vector3.right);
                    IEnumerable<LocalTargetInfo> enumerable = this.GetSubTargets(casterPawn, mainTarget, Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl), this.HalfLockAngle).Take(11);
                    foreach (LocalTargetInfo targ in enumerable)
                    {
                        if (!IsInvalid(targ))
                        {
                            Pawn pawn2 = targ.Thing as Pawn;
                            if (pawn2 != null)
                            {
                                Vector3 vector2 = pawn2.TrueCenter();
                                float y2 = AltitudeLayer.MapDataOverlay.AltitudeFor();
                                Material targetHighlightMat2 = SmartPistolMaterials.GetTargetHighlightMat(pawn2, faction);
                                Matrix4x4 matrix2 = Matrix4x4.TRS(new Vector3(vector2.x, y2, vector2.z), Quaternion.identity, new Vector3(1.2f, 1f, 1.2f));
                                Graphics.DrawMesh(MeshPool.plane10, matrix2, targetHighlightMat2, 0);
                                float num2 = Vector3.Distance(drawPos, pawn2.DrawPos);
                                Vector3 control2 = drawPos + normalized * (num2 * 0.6f);
                                BezierUtil.DrawQuadraticCurve(drawPos, control2, pawn2.DrawPos, SmartPistolMaterials.SmartPistolBlueLineMat, 0.1f, 0.05f, 20);
                                float t2 = 0.6f;
                                Vector3 point2 = BezierUtil.GetPoint(drawPos, control2, pawn2.DrawPos, t2);
                                this.bezierLabelWorldPositions.Add(point2 + Vector3.right);
                            }
                        }
                    }
                }
            }
        }
        private static readonly List<Pawn> tmpPawns = new List<Pawn>();
        public override void OnGUI(LocalTargetInfo target)
        {
            base.OnGUI(target);
            Pawn pawn = target.Thing as Pawn;
            if (pawn != null)
            {
                if (!this.IsInvalid(target))
                {
                    List<LocalTargetInfo> list = this.GetSubTargets(this.CasterPawn, target, Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl), this.HalfLockAngle).Take(11).ToList<LocalTargetInfo>();
                    Vector3 drawPos = this.CasterPawn.DrawPos;
                    Vector3 normalized = (pawn.DrawPos - drawPos).normalized;
                    SmartPistolLabelDrawer.DrawTargetInfoLabel(pawn, 1, normalized, this.CasterPawn, this.lockManager, this.Projectile);
                    float projSpeed = Projectile?.projectile?.speed ?? 0f;

                    Camera component = Find.CameraDriver.GetComponent<Camera>();

                    if (component != null)
                    {
                        float t = 0.85f;
                        float offsetCells = 0f;
                        tmpPawns.Clear();
                        tmpPawns.Add(pawn);
                        foreach (LocalTargetInfo targ in list)
                        {
                            if (!this.IsInvalid(targ) && targ.Pawn != null)
                            {
                                Verb_LaunchProjectileSmart.tmpPawns.Add(targ.Pawn);
                            }
                        }
                        List<ValueTuple<Vector3, float, float>> labelData = SmartPistolLabelDrawer.SmartPistolLabelUtil.GetLabelData(drawPos, Verb_LaunchProjectileSmart.tmpPawns, normalized, t, offsetCells);
                        foreach (ValueTuple<Vector3, float, float> valueTuple in labelData)
                        {
                            Vector3 item = valueTuple.Item1;
                            float item2 = valueTuple.Item2;
                            float item3 = valueTuple.Item3;
                            SmartPistolLabelDrawer.DrawRangeLabel(item, item2, item3, projSpeed);
                        }
                        var enumerable = list.Select((LocalTargetInfo x, int idx) => new
                        {
                            Pawn = x.Pawn,
                            Index = idx + 2
                        });
                        foreach (var tuple in enumerable)
                        {
                            SmartPistolLabelDrawer.DrawTargetInfoLabel(tuple.Pawn, tuple.Index, normalized, this.CasterPawn, this.lockManager, this.Projectile);
                        }
                    }
                }
            }
        }


        private Dictionary<LocalTargetInfo, Mote> targetMotes = new Dictionary<LocalTargetInfo, Mote>();
        private void SpawnLockMote(LocalTargetInfo targ)
        {
            if (targ.HasThing && targ.Thing.Spawned && !targetMotes.ContainsKey(targ))
            {
                ThingDef named = DefDatabase<ThingDef>.GetNamed("SmartPistol_LockedCurve", true);
                Mote_LockedCurve mote_LockedCurve = (Mote_LockedCurve)ThingMaker.MakeThing(named, null);
                mote_LockedCurve.Attach(targ.Thing, Vector3.zero, false);
                mote_LockedCurve.Scale = 1f;
                mote_LockedCurve.Caster = this.CasterPawn;
                mote_LockedCurve.MainDir = this.mainDirNormalized;
                mote_LockedCurve.LineMat = SmartPistolMaterials.SmartPistolRedLineMat;
                mote_LockedCurve.ForceSpawnTick(Find.TickManager.TicksGame);
                GenSpawn.Spawn(mote_LockedCurve, targ.Cell, this.Caster.Map, WipeMode.Vanish);
                this.targetMotes[targ] = mote_LockedCurve;
            }
        }
        private void DestroyAllLockMotes()
        {
            foreach (KeyValuePair<LocalTargetInfo, Mote> keyValuePair in this.targetMotes)
            {
                if (!keyValuePair.Value.Destroyed)
                {
                    keyValuePair.Value.Destroy(DestroyMode.Vanish);
                }
            }
            this.targetMotes.Clear();
        }
        #region SaveLoad
        List<LocalTargetInfo> lockedTargets;
        List<LocalTargetInfo> shotsNeededList;
        List<LocalTargetInfo> shotsFiredList;
        int lockedIndex = 0;
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref allowNeutral, nameof(allowNeutral));
            Scribe_TargetInfo.Look(ref lockOn, nameof(lockOn));
            Scribe_Values.Look(ref mainDirNormalized, nameof(mainDirNormalized));
            Scribe_Values.Look(ref burstShotCountSaved, nameof(burstShotCountSaved));
            Scribe_Collections.Look(ref projectilesInFlight, nameof(projectilesInFlight), LookMode.Reference);
            Scribe_Defs.Look(ref usedProjectileDef, nameof(usedProjectileDef)); //Can't use Projectile property in next TargetLockManager initialization and not sure how to pass it, so save the projectileDef and pass it to TargetLockManager
            Scribe_Values.Look(ref usedHalfLockAngle, nameof(usedHalfLockAngle));

            if (Scribe.mode == LoadSaveMode.Saving && lockManager != null)
            {
                shotsNeededList = Convert(lockManager.ShotsNeeded);
                shotsFiredList = Convert(lockManager.ShotsFired);
                Scribe_Collections.Look(ref shotsNeededList, nameof(shotsNeededList), LookMode.LocalTargetInfo);
                Scribe_Collections.Look(ref shotsFiredList, nameof(shotsFiredList), LookMode.LocalTargetInfo);
                Scribe_Collections.Look(ref lockManager.LockedTargets, nameof(lockManager.LockedTargets), LookMode.LocalTargetInfo);
                Scribe_Values.Look(ref lockManager.lockedIndex, nameof(lockManager.lockedIndex));
            }
            if (Scribe.mode != LoadSaveMode.Saving)
            {
                Scribe_Collections.Look(ref shotsNeededList, nameof(shotsNeededList), LookMode.LocalTargetInfo);
                Scribe_Collections.Look(ref shotsFiredList, nameof(shotsFiredList), LookMode.LocalTargetInfo);
                Scribe_Collections.Look(ref lockedTargets, nameof(lockManager.LockedTargets), LookMode.LocalTargetInfo);
                Scribe_Values.Look(ref lockedIndex, nameof(lockManager.lockedIndex));
            }


        }
        public void PostLoadInitLockManager()
        {
            if (lockManager == null && lockedTargets != null)
            {
                lockManager = new TargetLockManager(CasterPawn, this, usedProjectileDef, allowNeutral, usedHalfLockAngle, burstShotCountSaved, mainDirNormalized);
            }
            if (lockedTargets != null)
            {
                lockManager.LockedTargets = lockedTargets;
                lockManager.ShotsNeeded = Convert(shotsNeededList);
                lockManager.ShotsFired = Convert(shotsFiredList, shotsNeededList);
                lockManager.lockedIndex = lockedIndex;

                foreach (var target in lockManager.LockedTargets)
                {
                    if (!lockManager.IsTargetSatisfied(target))
                    {
                        SpawnLockMote(target);
                    }
                }

                lockedTargets = null;
                shotsNeededList = null;
                shotsFiredList = null;
            }
        }
        List<LocalTargetInfo> Convert(Dictionary<LocalTargetInfo, int> dict)
        {
            var result = new List<LocalTargetInfo>();
            foreach (var pair in dict)
            {
                for (int i = 0; i < pair.Value; i++)
                {
                    result.Add(pair.Key);
                }
            }
            return result;
        }
        Dictionary<LocalTargetInfo, int> Convert(List<LocalTargetInfo> list, IEnumerable<LocalTargetInfo> all = null)
        {
            var result = new Dictionary<LocalTargetInfo, int>();
            foreach (var item in list)
            {
                if (result.ContainsKey(item))
                {
                    result[item]++;
                }
                else
                {
                    result[item] = 1;
                }
            }
            if (all != null)
            {
                foreach (var item in all)
                {
                    if (!result.ContainsKey(item))
                    {
                        result[item] = 0;
                    }
                }
            }
            return result;
        }
        #endregion
    }


}
