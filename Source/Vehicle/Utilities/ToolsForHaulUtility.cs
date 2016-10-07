﻿//#define DEBUG

using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace ToolsForHaul
{
    public static class ToolsForHaulUtility
    {
        public static string TooLittleHaulable;
        public static string NoEmptyPlaceForCart;
        public static string NoEmptyPlaceLowerTrans;
        public static string NoAvailableCart;
        public static string BurningLowerTrans;
        private static readonly JobDef jobDefHaulWithAnimalCart = DefDatabase<JobDef>.GetNamed("HaulWithAnimalCart");
        private static readonly JobDef jobDefHaulWithCart = DefDatabase<JobDef>.GetNamed("HaulWithCart");
        private static readonly JobDef jobDefDismountInBase = DefDatabase<JobDef>.GetNamed("DismountInBase");
        private const double ValidDistance = 30;

        static ToolsForHaulUtility()
        {
            TooLittleHaulable = "TooLittleHaulable".Translate();
            NoEmptyPlaceForCart = "NoEmptyPlaceForCart".Translate();
            NoEmptyPlaceLowerTrans = "NoEmptyPlaceLower".Translate();
            NoAvailableCart = "NoAvailableCart".Translate();
            BurningLowerTrans = "BurningLower".Translate();
        }
        public static Apparel_Backpack TryGetBackpack(Pawn pawn)
        {
            foreach (Apparel apparel in pawn.apparel.WornApparel)
                if (apparel is Apparel_Backpack)
                    return apparel as Apparel_Backpack;
            return null;
        }

        public static Thing TryGetBackpackLastItem(Pawn pawn)
        {
            Apparel_Backpack backpack = TryGetBackpack(pawn);
            if (backpack == null)
                return null;
            Thing lastItem = null;
            int lastItemInd = -1;
            Thing foodInInventory = FoodUtility.BestFoodInInventory(pawn);
            if (pawn.inventory.container.Count > 0)
            {
                if (backpack.numOfSavedItems > 0)
                {
                    lastItemInd = (backpack.numOfSavedItems >= pawn.inventory.container.Count ? pawn.inventory.container.Count : backpack.numOfSavedItems) - 1;
                    lastItem = pawn.inventory.container[lastItemInd];
                }
                if (foodInInventory != null && backpack.numOfSavedItems < pawn.inventory.container.Count
                    && pawn.inventory.container[lastItemInd + 1] == foodInInventory)
                    lastItem = foodInInventory;
            }
            return lastItem;
        }


        public static List<Thing> Cart()
        {
            return Find.ListerThings.AllThings.FindAll(thing => thing is Vehicle_Cart);
        }

        public static bool AvailableCart(Vehicle_Cart cart, Pawn pawn)
        {
            if (!cart.TryGetComp<CompMountable>().IsMounted) return true;
            if (cart.TryGetComp<CompMountable>().Driver == pawn) return true;
            if (!cart.IsForbidden(pawn.Faction)) return true;
            return false;
        }

        public static bool AvailableAnimalCart(Vehicle_Cart cart)
        {
            Pawn Driver = cart.TryGetComp<CompMountable>().IsMounted ? cart.TryGetComp<CompMountable>().Driver : null;
            if (Driver == null)
                return false;

            return Driver.RaceProps.Animal && Driver.CasualInterruptibleNow()
                && Driver.needs.food.CurCategory < HungerCategory.Starving
                && Driver.needs.rest.CurCategory < RestCategory.VeryTired
                && !Driver.health.ShouldBeTendedNow;
        }

        public static Job HaulWithTools(Pawn pawn, Vehicle_Cart cart = null)
        {
            Trace.stopWatchStart();

            //Job Setting
            JobDef jobDef;
            TargetInfo targetC;
            int maxItem;
            int thresholdItem;
            int reservedMaxItem;
            IEnumerable<Thing> remainingItems;
            bool ShouldDrop;
            Thing lastItem = TryGetBackpackLastItem(pawn);
            if (cart == null)
            {
                Apparel_Backpack backpack = TryGetBackpack(pawn);
                jobDef = DefDatabase<JobDef>.GetNamed("HaulWithBackpack");
                targetC = backpack;
                maxItem = backpack.MaxItem;
                thresholdItem = (int)Math.Ceiling(maxItem * 0.5);
                reservedMaxItem = pawn.inventory.container.Count;
                remainingItems = pawn.inventory.container;
                ShouldDrop = true;
                if (lastItem != null)
                    for (int i = 0; i < pawn.inventory.container.Count; i++)
                        if (pawn.inventory.container[i] == lastItem && (reservedMaxItem - (i + 1)) <= 0)
                        {
                            ShouldDrop = false;
                        break;
                    }
            }
            else
            {
                if (cart.TryGetComp<CompMountable>().IsMounted &&
                    cart.TryGetComp<CompMountable>().Driver.RaceProps.Animal) jobDef = jobDefHaulWithAnimalCart;
                else jobDef = jobDefHaulWithCart;
                targetC = cart;
                maxItem = cart.MaxItem;
                thresholdItem = (int)Math.Ceiling(maxItem * 0.5);
                reservedMaxItem = cart.storage.Count;
                remainingItems = cart.storage;
                ShouldDrop = reservedMaxItem > 0 ? true : false;
            }
            Job job = new Job(jobDef)
            {
                targetQueueA = new List<TargetInfo>(),
                targetQueueB = new List<TargetInfo>(),
                targetC = targetC
            };

            Trace.AppendLine(pawn.LabelCap + " In HaulWithTools: " + jobDef.defName + "\n"
                + "MaxItem: " + maxItem + " reservedMaxItem: " + reservedMaxItem);

            //Drop remaining item
            if (ShouldDrop)
            {
                Trace.AppendLine("Start Drop remaining item");
                bool startDrop = false;
                for (int i = 0; i < remainingItems.Count(); i++)
                {
                    if (cart == null && startDrop == false)
                        if (remainingItems.ElementAt(i) == lastItem)
                            startDrop = true;
                        else
                            continue;
                    IntVec3 storageCell = FindStorageCell(pawn, remainingItems.ElementAt(i), job.targetQueueB);
                    if (storageCell == IntVec3.Invalid) break;
                    job.targetQueueB.Add(storageCell);
                }
                if (!job.targetQueueB.NullOrEmpty())
                {
                    Trace.AppendLine("Dropping Job is issued");
                    Trace.LogMessage();
                    return job;
                }
                if (cart != null && job.def == jobDefHaulWithCart && !cart.IsInValidStorage())
                {
                    Trace.AppendLine("In DismountInBase");
                    return DismountInBase(pawn, cart);
                }
                JobFailReason.Is(NoEmptyPlaceLowerTrans);
                Trace.AppendLine("End Drop remaining item");
                Trace.AppendLine("No Job. Reason: " + JobFailReason.Reason);
                Trace.LogMessage();
                return null;
            }

            //Collect item
            Trace.AppendLine("Start Collect item");
            IntVec3 searchPos;
            if (cart != null) searchPos = cart.Position;
            else searchPos = pawn.Position;
            foreach (SlotGroup slotGroup in Find.SlotGroupManager.AllGroupsListInPriorityOrder)
            {

                Trace.AppendLine("Start searching slotGroup");
                if (slotGroup.CellsList.Count - slotGroup.HeldThings.Count() < maxItem)
                    continue;

                //Counting valid items
                Trace.AppendLine("Start Counting valid items");
                int thingsCount = ListerHaulables.ThingsPotentiallyNeedingHauling().Count(item => slotGroup.Settings.AllowedToAccept(item));

                //Finding valid items
                Trace.AppendLine("Start Finding valid items");
                if (thingsCount > thresholdItem)
                {
                    //ClosestThing_Global_Reachable Configuration
                    Predicate<Thing> predicate = item
                        => !job.targetQueueA.Contains(item) && !item.IsBurning()
                            && ((cart != null && cart.allowances.Allows(item)) || cart == null)
                         && !item.IsForbidden(pawn.Faction)
                            && slotGroup.Settings.AllowedToAccept(item)
                            && pawn.CanReserveAndReach(item, PathEndMode.Touch, pawn.NormalMaxDanger());
                    //&& !(item is UnfinishedThing && ((UnfinishedThing)item).BoundBill != null)
                    //&& (item.def.IsNutritionSource && !SocialProperness.IsSociallyProper(item, pawn, false, false));
                    Thing thing = GenClosest.ClosestThing_Global_Reachable(searchPos,
                                                        ListerHaulables.ThingsPotentiallyNeedingHauling(),
                                                        PathEndMode.ClosestTouch,
                                                        TraverseParms.For(pawn, pawn.NormalMaxDanger()),
                                                        9999,
                                                        predicate);
                    if (thing == null)
                        continue;

                    IntVec3 center = thing.Position;

                    //Enqueue items in valid distance
                    Trace.AppendLine("Start Enqueuing items in valid distance");
                    foreach (Thing item in ListerHaulables.ThingsPotentiallyNeedingHauling().Where(item
                                    => !job.targetQueueA.Contains(item) && !item.IsBurning()
                                        && ((cart != null && cart.allowances.Allows(item)) || cart == null)
                                        && !item.IsForbidden(pawn.Faction)
                                        && slotGroup.Settings.AllowedToAccept(item)
                                        && pawn.CanReserveAndReach(item, PathEndMode.Touch, pawn.NormalMaxDanger())
                                        && center.DistanceToSquared(item.Position) <= ValidDistance))
                    {
                        job.targetQueueA.Add(item);
                        if (reservedMaxItem + job.targetQueueA.Count >= maxItem)
                            break;
                    }

                    //Find storage cell
                    Trace.AppendLine("Start Finding storage cell");
                    if (reservedMaxItem + job.targetQueueA.Count > thresholdItem)
                    {
                        List<IntVec3> availableCells = new List<IntVec3>();
                        foreach (IntVec3 cell in slotGroup.CellsList.Where(cell => pawn.CanReserveAndReach(cell, PathEndMode.ClosestTouch, Danger.Deadly) && cell.Standable() && cell.GetStorable() == null))
                        {
                            StoragePriority currentPriority = HaulAIUtility.StoragePriorityAtFor(thing.Position, thing);
                            IntVec3 storeCell = cell;
                            if (!StoreUtility.TryFindBestBetterStoreCellFor(thing, pawn, currentPriority, pawn.Faction, out storeCell, true))
                            {
                                if (cell.InAllowedArea(pawn))
                                    job.targetQueueB.Add(cell);
                                if (job.targetQueueB.Count >= job.targetQueueA.Count)
                                    break;
                            }
                            else
                            {
                                if (storeCell.InAllowedArea(pawn))
                                    job.targetQueueB.Add(storeCell);
                                if (job.targetQueueB.Count >= job.targetQueueA.Count)
                                    break;
                            }

                        }
                        break;
                    }
                    job.targetQueueA.Clear();
                }
            }
            Trace.AppendLine("Elapsed Time");
            Trace.stopWatchStop();

            //Check job is valid
            if (!job.targetQueueA.NullOrEmpty() && reservedMaxItem + job.targetQueueA.Count > thresholdItem
                && !job.targetQueueB.NullOrEmpty())
            {
                Trace.AppendLine("Hauling Job is issued");
                Trace.LogMessage();
                return job;
            }
            if (cart != null && job.def == jobDefHaulWithCart && !cart.IsInValidStorage())
            {
                Trace.AppendLine("In DismountInBase: ");
                return DismountInBase(pawn, cart);
            }

            if (job.targetQueueA.NullOrEmpty())
                JobFailReason.Is("NoHaulable".Translate());
            else if (reservedMaxItem + job.targetQueueA.Count <= thresholdItem)
                JobFailReason.Is(TooLittleHaulable);
            else if (job.targetQueueB.NullOrEmpty())
                JobFailReason.Is(NoEmptyPlaceLowerTrans);
            Trace.AppendLine("No Job. Reason: " + JobFailReason.Reason);
            Trace.LogMessage();
            return null;
        }

        public static Job DismountInBase(Pawn pawn, Vehicle_Cart cart)
        {
            Job job = new Job(jobDefDismountInBase);
            job.targetA = cart;
            job.targetB = FindStorageCell(pawn, cart);
            if (job.targetB != IntVec3.Invalid)
            {
                Trace.AppendLine("DismountInBase Job is issued");
                Trace.LogMessage();
                return job;
            }
            JobFailReason.Is(NoEmptyPlaceForCart);
            Trace.AppendLine("No Job. Reason: " + JobFailReason.Reason);
            Trace.LogMessage();
            return null;
        }

        public static IntVec3 FindStorageCell(Pawn pawn, Thing haulable, List<TargetInfo> targetQueue = null)
        {
            //Find closest cell in queue.
            if (!targetQueue.NullOrEmpty())
                foreach (TargetInfo target in targetQueue)
                    foreach (IntVec3 adjCell in GenAdjFast.AdjacentCells8Way(target))
                        if (!targetQueue.Contains(adjCell) && adjCell.IsValidStorageFor(haulable) && pawn.CanReserveAndReach(adjCell, PathEndMode.ClosestTouch, Danger.Deadly))
                            return adjCell;
            /*
            StoragePriority currentPriority = HaulAIUtility.StoragePriorityAtFor(closestHaulable.Position, closestHaulable);
            IntVec3 foundCell;
            if (StoreUtility.TryFindBestBetterStoreCellFor(closestHaulable, pawn, currentPriority, pawn.Faction, out foundCell, true))
                return foundCell;
            */
            //Vanilla code is not worked item on container.
            StoragePriority currentPriority = HaulAIUtility.StoragePriorityAtFor(haulable.Position, haulable);
            foreach (SlotGroup slotGroup in Find.SlotGroupManager.AllGroupsListInPriorityOrder)
            {
                if (slotGroup.Settings.Priority < currentPriority)
                    break;
                foreach (IntVec3 cell in slotGroup.CellsList)
                    if (((!targetQueue.NullOrEmpty() && !targetQueue.Contains(cell)) || targetQueue.NullOrEmpty())
                        && cell.GetStorable() == null
                        && slotGroup.Settings.AllowedToAccept(haulable)
                        && pawn.CanReserveAndReach(cell, PathEndMode.ClosestTouch, Danger.Deadly))
                        return cell;
            }

            return IntVec3.Invalid;
        }
    }
}
