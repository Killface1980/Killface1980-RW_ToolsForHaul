﻿namespace TFH_VehicleHauling.WorkGivers
{
    using RimWorld;

    using TFH_VehicleHauling.DefOf_TFH;

    using Verse;
    using Verse.AI;

    public class WorkGiver_RescueVehicleDowned : WorkGiver_TakeToBed
    {
        private const float MinDistFromEnemy = 40f;

        public override PathEndMode PathEndMode
        {
            get
            {
                return PathEndMode.OnCell;
            }
        }

        public override ThingRequest PotentialWorkThingRequest
        {
            get
            {
                return ThingRequest.ForGroup(ThingRequestGroup.Pawn);
            }
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Pawn pawn2 = t as Pawn;
            if (pawn2 == null || !pawn2.Downed || pawn2.Faction != pawn.Faction || pawn2.InBed() || !pawn.CanReserve(pawn2, 1, -1, null, forced) || GenAI.EnemyIsNear(pawn2, 40f))
            {
                return false;
            }
            Thing thing = base.FindBed(pawn, pawn2);
            return thing != null && pawn2.CanReserve(thing, 1, -1, null, false);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Pawn pawn2 = t as Pawn;
            Thing t2 = base.FindBed(pawn, pawn2);
            return new Job(HaulJobDefOf.HaulWithCart, pawn2, t2)
                       {
                           count = 1
                       };
        }
    }
}
