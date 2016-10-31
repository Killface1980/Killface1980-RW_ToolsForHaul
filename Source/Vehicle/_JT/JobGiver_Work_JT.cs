﻿using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;
using Verse.AI;
using RimWorld;

namespace ToolsForHaul.JTBetterHauling
{
    public class JobGiver_Work_JT : JobGiver_Work
    {
        [Detour(typeof(JobGiver_Work), bindingFlags = (BindingFlags.Instance | BindingFlags.NonPublic))]
        protected override Job TryGiveJob(Pawn pawn)
        {
            //Player forced? or fire?
            if (this.emergency && pawn.mindState.priorityWork.IsPrioritized)
            {
                List<WorkGiverDef> workGiversByPriority = pawn.mindState.priorityWork.WorkType.workGiversByPriority;
                for (int i = 0; i < workGiversByPriority.Count; i++)
                {
                    WorkGiver worker = workGiversByPriority[i].Worker;
                    Job job = this.GiverTryGiveJobTargeted(pawn, worker, pawn.mindState.priorityWork.Cell);
                    if (job != null)
                    {
                        job.playerForced = true;
                        return job;
                    }
                }
                pawn.mindState.priorityWork.Clear();
            }
            List<WorkGiver> list = this.emergency ? pawn.workSettings.WorkGiversInOrderEmergency : pawn.workSettings.WorkGiversInOrderNormal;
            int num = -999;
            TargetInfo targetInfo = TargetInfo.Invalid;
            WorkGiver_Scanner workGiver_Scanner = null;
            for (int j = 0; j < list.Count; j++)
            {
                WorkGiver workGiver = list[j];
                //If pawn cannot do job
                if (workGiver.def.priorityInType != num && targetInfo.IsValid)
                {
                    break;
                }
                if (this.GiverCanGiveJobToPawn(pawn, workGiver))
                {
                    try
                    {
                        //If needs to go to bed? Resting?
                        Job job2 = workGiver.NonScanJob(pawn);
                        if (job2 != null)
                        {
                            return job2;
                        }
                        WorkGiver_Scanner scanner = workGiver as WorkGiver_Scanner;
                        if (scanner != null)
                        {
                            if (workGiver.def.scanThings)
                            {
                                Predicate<Thing> predicate = (Thing t) => !t.IsForbidden(pawn) && scanner.HasJobOnThing(pawn, t);
                                IEnumerable<Thing> enumerable = scanner.PotentialWorkThingsGlobal(pawn);
                                Thing thing;
                                //Always false unless roof or researcher
                                if (scanner.Prioritized)
                                {
                                    IEnumerable<Thing> enumerable2 = enumerable;
                                    if (enumerable2 == null)
                                    {
                                        enumerable2 = Find.ListerThings.ThingsMatching(scanner.PotentialWorkThingRequest);
                                    }
                                    Predicate<Thing> validator = predicate;
                                    thing = GenClosest.ClosestThing_Global_Reachable(pawn.Position, enumerable2, scanner.PathEndMode, TraverseParms.For(pawn, Danger.Deadly, TraverseMode.ByPawn, false), 9999f, validator, (Thing x) => scanner.GetPriority(pawn, x));
                                }
                                else
                                {
                                    Predicate<Thing> validator = predicate;
                                    //Start of my code
                                    if (scanner is WorkGiver_HaulGeneral)
                                    {
                                        thing = GenClosest_JT.ClosestThingReachable_JT(pawn.Position, scanner.PotentialWorkThingRequest, scanner.PathEndMode, TraverseParms.For(pawn, Danger.Deadly, TraverseMode.ByPawn, false), 9999f, validator, enumerable, scanner.LocalRegionsToScanFirst, enumerable != null);
                                    }
                                    else
                                    {
                                        thing = GenClosest.ClosestThingReachable(pawn.Position, scanner.PotentialWorkThingRequest, scanner.PathEndMode, TraverseParms.For(pawn, Danger.Deadly, TraverseMode.ByPawn, false), 9999f, validator, enumerable, scanner.LocalRegionsToScanFirst, enumerable != null);
                                    }
                                    //End of my code
                                }
                                if (thing != null)
                                {
                                    targetInfo = thing;
                                    workGiver_Scanner = scanner;
                                }
                            }
                            if (workGiver.def.scanCells)
                            {
                                IntVec3 position = pawn.Position;
                                float num2 = 99999f;
                                float num3 = -3.40282347E+38f;
                                bool prioritized = scanner.Prioritized;
                                foreach (IntVec3 current in scanner.PotentialWorkCellsGlobal(pawn))
                                {
                                    bool flag = false;
                                    float lengthHorizontalSquared = (current - position).LengthHorizontalSquared;
                                    if (prioritized)
                                    {
                                        if (!current.IsForbidden(pawn) && scanner.HasJobOnCell(pawn, current))
                                        {
                                            float priority = scanner.GetPriority(pawn, current);
                                            if (priority > num3 || (priority == num3 && lengthHorizontalSquared < num2))
                                            {
                                                flag = true;
                                                num3 = priority;
                                            }
                                        }
                                    }
                                    else if (lengthHorizontalSquared < num2 && !current.IsForbidden(pawn) && scanner.HasJobOnCell(pawn, current))
                                    {
                                        flag = true;
                                    }
                                    if (flag)
                                    {
                                        targetInfo = current;
                                        workGiver_Scanner = scanner;
                                        num2 = lengthHorizontalSquared;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(string.Concat(new object[]
                        {
                            pawn,
                            " threw exception in WorkGiver ",
                            workGiver.def.defName,
                            ": ",
                            ex.ToString()
                        }));
                    }
                    finally
                    {
                    }
                    if (targetInfo.IsValid)
                    {
                        pawn.mindState.lastGivenWorkType = workGiver.def.workType;
                        Job job3;
                        if (targetInfo.HasThing)
                        {
                            job3 = workGiver_Scanner.JobOnThing(pawn, targetInfo.Thing);
                        }
                        else
                        {
                            job3 = workGiver_Scanner.JobOnCell(pawn, targetInfo.Cell);
                        }
                        if (job3 != null)
                        {
                            return job3;
                        }
                        Log.ErrorOnce(string.Concat(new object[]
                        {
                            workGiver_Scanner,
                            " provided target ",
                            targetInfo,
                            " but yielded no actual job for pawn ",
                            pawn,
                            ". The CanGiveJob and JobOnX methods may not be synchronized."
                        }), 6112651);
                    }
                    num = workGiver.def.priorityInType;
                }
            }
            return null;
        }

        private bool GiverCanGiveJobToPawn(Pawn pawn, WorkGiver giver)
        {
            return (giver.def.canBeDoneByNonColonists || pawn.IsColonist) && giver.MissingRequiredCapacity(pawn) == null && !giver.ShouldSkip(pawn);
        }

        private Job GiverTryGiveJobTargeted(Pawn pawn, WorkGiver giver, IntVec3 cell)
        {
            if (!this.GiverCanGiveJobToPawn(pawn, giver))
            {
                return null;
            }
            try
            {
                Job job = giver.NonScanJob(pawn);
                if (job != null)
                {
                    Job result = job;
                    return result;
                }
                WorkGiver_Scanner scanner = giver as WorkGiver_Scanner;
                if (scanner != null)
                {
                    if (giver.def.scanThings)
                    {
                        Predicate<Thing> predicate = (Thing t) => !t.IsForbidden(pawn) && scanner.HasJobOnThing(pawn, t);
                        List<Thing> thingList = cell.GetThingList();
                        for (int i = 0; i < thingList.Count; i++)
                        {
                            Thing thing = thingList[i];
                            if (scanner.PotentialWorkThingRequest.Accepts(thing) && predicate(thing))
                            {
                                pawn.mindState.lastGivenWorkType = giver.def.workType;
                                Job result = scanner.JobOnThing(pawn, thing);
                                return result;
                            }
                        }
                    }
                    if (giver.def.scanCells && !cell.IsForbidden(pawn) && scanner.HasJobOnCell(pawn, cell))
                    {
                        pawn.mindState.lastGivenWorkType = giver.def.workType;
                        Job result = scanner.JobOnCell(pawn, cell);
                        return result;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat(new object[]
                {
                    pawn,
                    " threw exception in GiverTryGiveJobTargeted on WorkGiver ",
                    giver.def.defName,
                    ": ",
                    ex.ToString()
                }));
            }
            return null;
        }

    }
}