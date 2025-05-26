using CombatExtended;
using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace SmartPistol
{
    public class Projectile_SmartBullet : BulletCE
    {
        [HarmonyLib.HarmonyPatch(typeof(BulletCE), nameof(BulletCE.Impact))]
        internal static class BulletCEBeforeTakeDamage
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var list = instructions.ToList();
                bool patched = false;
                var targetMethod = AccessTools.Method(typeof(Thing), nameof(Thing.TakeDamage));
                for (int index = 0; index < list.Count; index++)
                {
                    if (!patched && list[index].opcode == OpCodes.Ldarg_1 && list[index + 2].Calls(targetMethod))
                    {
                        yield return new CodeInstruction(OpCodes.Ldarg_0);
                        yield return new CodeInstruction(OpCodes.Ldarg_1);
                        yield return new CodeInstruction(OpCodes.Ldloca_S, list[index + 1].operand);
                        yield return CodeInstruction.Call(typeof(BulletCEBeforeTakeDamage), nameof(BulletCEBeforeTakeDamage.BeforeTakeDamage));
                        patched = true;
                    }
                    yield return list[index];
                }
                if (!patched)
                {
                    Log.Error("BulletCE patch failed!");
                }
            }
            public static void BeforeTakeDamage(BulletCE projectile, Thing hitThing, ref DamageInfo dinfo)
            {
                if (projectile is Projectile_SmartBullet smartBullet)
                {
                    smartBullet.BeforeTakeDamage(hitThing, ref dinfo);
                }
            }
        }
        public override Quaternion ExactRotation => Quaternion.LookRotation((ExactPosition - LastPos).Yto0());
        protected virtual void BeforeTakeDamage(Thing hitThing, ref DamageInfo dinfo)
        {
            if (hitThing is Pawn victim)
            {
                var brain = victim.health.hediffSet.GetBrain();
                if (brain == null)
                {
                    return;
                }


                BodyPartRecord part;
                for (part = brain; part != null; part = part.parent)
                {
                    if (part.depth == BodyPartDepth.Outside)
                    {
                        break;
                    }
                }
                Dictionary<BodyPartRecord, float> chances = new Dictionary<BodyPartRecord, float>
                {
                    { part, 0.93f }// Skill issue
                };
                foreach (var subPart in part.parts.Where(p => p.depth == BodyPartDepth.Outside))
                {
                    chances.Add(subPart, 0.01f);//Small chance to hit something else
                }
                dinfo.SetHitPart(chances.RandomElementByWeight(x => x.Value).Key);


            }
        }
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
                if (launcher is Pawn pawn1 && pawn1.inventory != null)
                {
                    foreach (var item in pawn1.inventory.innerContainer)
                    {
                        var compEquippable = item.TryGetComp<CompEquippable>();
                        if (compEquippable != null)
                        {
                            foreach (var verb in compEquippable.AllVerbs.OfType<Verb_LaunchProjectileSmart>())
                            {
                                yield return verb;
                            }
                        }
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
        public override void Destroy(DestroyMode destroyMode)
        {
            foreach (var verb in Verbs)
            {
                verb.OnProjectileDestroyed(this);
            }
            base.Destroy(destroyMode);
        }

        public override void Tick()
        {
            base.Tick();
            if (this.FlightTicks >= 4)
            {
                Map map = base.Map;
                if (map != null)
                {
                    FleckMaker.ConnectingLine(ExactPosition, LastPos, Defs.SmartPistol_trail, map, 0.055f);
                }
            }
        }
    }
}
