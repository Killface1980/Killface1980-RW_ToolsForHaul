﻿using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ToolsForHaul
{
    public class WorkGiver_EquipTools : WorkGiver_Scanner
    {
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            List<Thing> potentialWorkThingsGlobal = new List<Thing>();

            foreach (Thing thing in Find.ListerThings.AllThings)
            {
                float statfloat = 0;
                if (!thing.def.IsMeleeWeapon || !pawn.CanReserveAndReach(thing, PathEndMode.ClosestTouch, Danger.Deadly))
                    continue;
                foreach (KeyValuePair<StatDef, float> stat in pawn.GetWeightedWorkStats())
                {
                    statfloat += RightTools.GetMaxStat(thing as ThingWithComps, stat.Key);
                    if (statfloat > 0)
                    {
                        potentialWorkThingsGlobal.Add(thing);
                    }
                }

            }


            return potentialWorkThingsGlobal;
        }


        public override bool ShouldSkip(Pawn pawn)
        {
            Apparel_Toolbelt toolbelt = ToolsForHaulUtility.TryGetToolbelt(pawn);
            //Should skip pawn that don't have a toolbelt.
            if (toolbelt == null)
                return true;

            // Skip it toolbelt full
        
            if (toolbelt.MaxItem <= toolbelt.slotsComp.slots.Count)
            {
                return true;
            }

            return false;
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t)
        {
            Apparel_Toolbelt toolbelt = ToolsForHaulUtility.TryGetToolbelt(pawn);

            if (!toolbelt.slotsComp.slots.Contains(t.def) || pawn.equipment.Primary.def.Equals(t.def))
                return true;
            return false;

        }

        public override Job JobOnThing(Pawn pawn, Thing t)
        {
            Apparel_Toolbelt toolbelt = ToolsForHaulUtility.TryGetToolbelt(pawn);

            if (toolbelt != null)
            {
                Job jobNew = new Job(DefDatabase<JobDef>.GetNamed("PutInToolbeltSlot"));
                jobNew.maxNumToCarry = 1;
                jobNew.targetB = toolbelt;
                jobNew.targetQueueA = new List<TargetInfo>();
                jobNew.targetQueueA.Add(t);
                pawn.Reserve(t);

                return jobNew;

            }

            JobFailReason.Is("NoToolbelt".Translate());
            return null;
        }
    }
}
