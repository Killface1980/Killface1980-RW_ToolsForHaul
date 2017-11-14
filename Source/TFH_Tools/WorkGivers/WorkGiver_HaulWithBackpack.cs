﻿//#define DEBUG

namespace TFH_Tools.WorkGivers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using RimWorld;

    using TFH_Tools;

    using TFH_VehicleBase;

    using Verse;
    using Verse.AI;

    public class WorkGiver_HaulWithBackpack : WorkGiver_Haul
    {
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            List<Thing> list = new List<Thing>();
            Apparel_Backpack backpack = pawn.TryGetBackpack();
            if (backpack == null)
            {
                return list;
            }
            for (int index = 0; index < pawn.Map.listerHaulables.ThingsPotentiallyNeedingHauling().Count; index++)
            {
                Thing thing = pawn.Map.listerHaulables.ThingsPotentiallyNeedingHauling()[index];
                if (thing.def.thingCategories.Exists(
                    category => backpack.slotsComp.Properties.allowedThingCategoryDefs.Exists(
                                    subCategory => subCategory.ThisAndChildCategoryDefs.Contains(category))
                                && !backpack.slotsComp.Properties.forbiddenSubThingCategoryDefs.Exists(
                                    subCategory => subCategory.ThisAndChildCategoryDefs.Contains(category))))
                {
                    list.Add(thing);
                }

                // return ToolsForHaulUtility.Cart();
            }
            return list;
        }

        public override bool ShouldSkip(Pawn pawn)
        {

            Apparel_Backpack backpack = pawn.TryGetBackpack();

            // Should skip pawn that don't have backpack.
            if (backpack == null || backpack.slotsComp.innerContainer.Count > 0)
            {
                return true;
            }

            // if (backpack.MaxItem - pawn.inventory.innerContainer.Count == 0)
            // {
            //     return true;
            // }

            Trace.DebugWriteHaulingPawn(pawn);
            return pawn.Map.listerHaulables.ThingsPotentiallyNeedingHauling().Count == 0;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (t is Corpse)
            {
                return null;
            }

            if (!HaulAIUtility.PawnCanAutomaticallyHaul(pawn, t, forced))
            {
                return null;
            }

            Apparel_Backpack backpack = pawn.TryGetBackpack();
            if (backpack != null)
            {
                if (!t.def.thingCategories.Exists(
                        category => backpack.slotsComp.Properties.allowedThingCategoryDefs.Exists(
                                        subCategory => subCategory.ThisAndChildCategoryDefs.Contains(category))
                                    && !backpack.slotsComp.Properties.forbiddenSubThingCategoryDefs.Exists(
                                        subCategory => subCategory.ThisAndChildCategoryDefs.Contains(category))))
                {
                    JobFailReason.Is("Backpack can't hold that thing");
                    return null;
                }
                else
                {
                    // ReSharper disable once AssignNullToNotNullAttribute
                    return ToolsForHaulUtility.HaulWithTools(pawn, t);
                }
            }

            JobFailReason.Is("NoBackpack".Translate());
            return null;
        }
    }
}