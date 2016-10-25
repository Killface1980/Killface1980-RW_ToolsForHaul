﻿using System.Collections.Generic;
using System.Linq;
using RimWorld;
using ToolsForHaul.Components;
using ToolsForHaul.Utilities;
using Verse;
using Verse.AI;

namespace ToolsForHaul.WorkGivers
{
    public class WorkGiver_HaulWithCart : WorkGiver_Scanner
    {


        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            //return ToolsForHaulUtility.Cart();
            return ListerHaulables.ThingsPotentiallyNeedingHauling();
        }

        public override bool ShouldSkip(Pawn pawn)
        {
            Trace.DebugWriteHaulingPawn(pawn);
            if (ToolsForHaulUtility.Cart.Count == 0)
                return true;

            if (pawn.RaceProps.Animal || !pawn.RaceProps.Humanlike || !pawn.RaceProps.hasGenders)
                return true;

            if (ToolsForHaulUtility.GetTurretDriver(pawn) != null)
                return true;

            //int countForbidden = 0;
            //foreach (var actualCart in cart)
            //{
            //    if (actualCart.IsForbidden(pawn.Faction))
            //        countForbidden+=1;
            //}
            //if (cart.Count== countForbidden)
            //{
            //    return true;
            //}
            return false;
        }

        public override Job JobOnThing(Pawn pawn, Thing t)
        {
            Vehicle_Cart cart = null;

            if (!HaulAIUtility.PawnCanAutomaticallyHaulFast(pawn, t))
            {
                return null;
            }

            // Vehicle selection
            bool isTurret = false;

            if (ToolsForHaulUtility.IsDriver(pawn))
            {
                cart = ToolsForHaulUtility.GetCartDriver(pawn);
            }

            if (ToolsForHaulUtility.GetTurretDriver(pawn)!=null)
            {
                JobFailReason.Is("Can't haul with military vehicle");
                return null;
            }

            if (cart == null)
            {
                cart = RightTools.GetRightVehicle(pawn, WorkTypeDefOf.Hauling) as Vehicle_Cart;

                if (cart == null)
                    return null;
            }

            if (cart.IsForbidden(pawn.Faction) || !pawn.CanReserveAndReach(cart, PathEndMode.ClosestTouch, pawn.NormalMaxDanger()))
                return null;
            if (cart.IsBurning())
            {
                JobFailReason.Is(ToolsForHaulUtility.BurningLowerTrans);
                return null;
            }

            if (!cart.allowances.Allows(t))
            {
                JobFailReason.Is("Cart does not allow that thing");
                    return null;
                }
                if (ListerHaulables.ThingsPotentiallyNeedingHauling().Count == 0 && cart.storage.Count == 0)
                {
                    JobFailReason.Is("NoHaulable".Translate());
                    return null;
                }
            
            if (Find.SlotGroupManager.AllGroupsListInPriorityOrder.Count == 0)
            {
                JobFailReason.Is(ToolsForHaulUtility.NoEmptyPlaceLowerTrans);
                return null;
            }
            if (ToolsForHaulUtility.AvailableAnimalCart(cart) || ToolsForHaulUtility.AvailableVehicle(cart, pawn))
                return ToolsForHaulUtility.HaulWithTools(pawn, cart, t);
            JobFailReason.Is(ToolsForHaulUtility.NoAvailableCart);
            return null;
        }

    }

}