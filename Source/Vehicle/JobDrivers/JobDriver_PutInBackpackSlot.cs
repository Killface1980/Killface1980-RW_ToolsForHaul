﻿using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace ToolsForHaul
{
    public class JobDriver_PutInBackpackSlot : JobDriver
    {
        //Constants
        public const TargetIndex HaulableInd = TargetIndex.A;
        public const TargetIndex SlotterInd = TargetIndex.B;

        public override string GetReport()
        {
            Thing hauledThing = TargetThingA;

            string repString;
            if (hauledThing != null)
                repString = "ReportPutInInventory".Translate(hauledThing.LabelCap, CurJob.GetTarget(SlotterInd).Thing.LabelCap);
            else
                repString = "ReportPutSomethingInInventory".Translate(CurJob.GetTarget(SlotterInd).Thing.LabelCap);

            return repString;
        }


        protected override IEnumerable<Toil> MakeNewToils()
        {
            Apparel_Backpack backpack = CurJob.GetTarget(SlotterInd).Thing as Apparel_Backpack;

            // no free slots
            this.FailOn(() => backpack.slotsComp.slots.Count >= (backpack as Apparel_Backpack).MaxItem);

            // reserve resources
            yield return Toils_Reserve.ReserveQueue(HaulableInd);

            // extract next target thing from targetQueue
            Toil toilExtractNextTarget = Toils_JobTransforms.ExtractNextTargetFromQueue(HaulableInd);
            yield return toilExtractNextTarget;

            Toil toilGoToThing = Toils_Goto.GotoThing(HaulableInd, PathEndMode.ClosestTouch)
                .FailOnDespawnedOrNull(HaulableInd);
            yield return toilGoToThing;

            Toil pickUpThingIntoSlot = new Toil
            {
                initAction = () =>
                {
                    if (!backpack.slotsComp.slots.TryAdd(CurJob.targetA.Thing))
                        EndJobWith(JobCondition.Incompletable);
                }
            };
            yield return pickUpThingIntoSlot;

            yield return Toils_Jump.JumpIfHaveTargetInQueue(HaulableInd, toilExtractNextTarget);
        }
    }
}