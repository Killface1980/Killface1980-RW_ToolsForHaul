﻿using RimWorld;
using Verse;
using Verse.AI;

namespace ToolsForHaul
{
    public class JobGiver_ExitMapBest : JobGiver_ExitMap
    {
        protected override bool TryFindGoodExitDest(Pawn pawn, bool canDig, out IntVec3 dest)
        {

            foreach (Vehicle_Cart vehicle_Cart in ToolsForHaulUtility.Cart())
            {
                if (vehicle_Cart.mountableComp.IsMounted && !vehicle_Cart.mountableComp.Driver.RaceProps.Animal && vehicle_Cart.mountableComp.Driver.ThingID == pawn.ThingID)
                {
                    vehicle_Cart.despawnAtEdge = true;
                }
            }


            return RCellFinder.TryFindBestExitSpot(pawn, out dest);
        }

    }
}