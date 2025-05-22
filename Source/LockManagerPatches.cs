using CombatExtended;
using RB_SmartPistol;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace SmartPistol
{
    internal static class LockManagerPatches
    {
        [HarmonyLib.HarmonyPatch(typeof(TargetLockManager), nameof(TargetLockManager.EstimateAverageShotsHead))]
        internal static class EstimateAverageShotsHead_Patch
        {
            public static bool Prefix(Pawn target, ThingDef projectileDef, ref int __result)
            {
                var brain = target.health.hediffSet.GetBrain();
                if (brain == null)
                {
                    __result = 3; //Hit unknown thing 3 times, just to be sure
                    return false;
                }
                var dinfo = new DamageInfo(
                    projectileDef.projectile.damageDef,
                    projectileDef.projectile.GetDamageAmount(1),
                    (projectileDef.projectile as ProjectilePropertiesCE)?.armorPenetrationSharp ?? 1f, //Armor Penetration
                    -1,
                    null,
                    null,
                    projectileDef);
                var damage = dinfo.Amount;
                var penetration = dinfo.ArmorPenetrationInt;
                List<BodyPartRecord> list = new List<BodyPartRecord>();
                BodyPartRecord part = brain;
                for (BodyPartRecord bodyPartRecord = brain; bodyPartRecord != null; bodyPartRecord = bodyPartRecord.parent)
                {
                    list.Add(bodyPartRecord);
                    if (bodyPartRecord.depth == BodyPartDepth.Outside)
                    {
                        part = bodyPartRecord;
                        break;
                    }
                }
                var partArmors = (target?.apparel?.WornApparel?.FindAll(a => a.def.apparel.CoversBodyPart(part)) ?? Enumerable.Empty<Apparel>());
                float maxArmorRating = 0.01f;
                if (partArmors.Any())
                {
                    maxArmorRating = partArmors.Max(x => x.GetStatValue(StatDefOf.ArmorRating_Sharp));
                }
                if (maxArmorRating > penetration)
                {
                    dinfo = ArmorUtilityCE.GetDeflectDamageInfo(dinfo, part, ref damage, ref penetration);
                }
                __result = Mathf.CeilToInt(target.health.hediffSet.GetPartHealth(part) / damage);
                return false;
            }
        }
    }
}
