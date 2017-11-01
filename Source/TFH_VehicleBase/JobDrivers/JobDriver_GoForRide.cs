﻿namespace TFH_VehicleBase.JobDrivers
{
    using System.Collections.Generic;
    using System.Diagnostics;

    using RimWorld;

    using TFH_VehicleBase.Components;

    using Verse;
    using Verse.AI;

    public class JobDriver_GoForRide : JobDriver
    {
        private const TargetIndex DrivePathInd = TargetIndex.A;
        private const TargetIndex MountableInd = TargetIndex.B;

        private const int tickCheckInterval = 64;

        public override bool TryMakePreToilReservations()
        {
            return this.pawn.Reserve(this.TargetThingB, this.job);
        }

        [DebuggerHidden]
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.EndOnDespawnedOrNull(MountableInd);
            if (this.TargetThingB.IsForbidden(this.pawn.Faction))
            {
                this.FailOnForbidden(MountableInd);
            }

            yield return Toils_Reserve.Reserve(MountableInd, this.job.def.joyMaxParticipants);

            Toil toil = Toils_Goto.GotoCell(DrivePathInd, PathEndMode.OnCell);
            toil.tickAction = delegate
            {
                if (Find.TickManager.TicksGame > this.startTick + this.job.def.joyDuration)
                {
                    this.EndJobWith(JobCondition.Succeeded);
                    return;
                }

                JoyUtility.JoyTickCheckEnd(this.pawn);
            };
            ThingWithComps cart = this.job.GetTarget(MountableInd).Thing as ThingWithComps;

            // JumpIf already mounted
            yield return Toils_Jump.JumpIf(toil, () =>
            {
                if (cart.TryGetComp<CompMountable>().Rider == this.pawn)
                {
                    return true;
                }

                return false;
            });

            // Mount on Target
            yield return Toils_Goto.GotoThing(MountableInd, PathEndMode.ClosestTouch)
                                        .FailOnDestroyedOrNull(MountableInd);
            yield return Toils_Cart.MountOn(MountableInd);



            yield return toil;
            yield return new Toil
            {
                initAction = delegate
                {
                    if (this.job.targetQueueA.Count > 0)
                    {
                        LocalTargetInfo targetA = this.job.targetQueueA[0];
                        this.job.targetQueueA.RemoveAt(0);
                        this.job.targetA = targetA;
                        this.JumpToToil(toil);
                    }
                }
            };
        }

    }
}
