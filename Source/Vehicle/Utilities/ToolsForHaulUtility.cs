﻿//#define DEBUG

using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using ToolsForHaul.Components;
using ToolsForHaul.JobDefs;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ToolsForHaul.Utilities
{
    public static class ToolsForHaulUtility
    {
        private const double ValidDistance = 30;
        public static readonly string TooLittleHaulable;
        public static readonly string NoEmptyPlaceForCart;
        public static readonly string NoEmptyPlaceLowerTrans;
        public static readonly string NoAvailableCart;
        public static readonly string BurningLowerTrans;

        public static List<Thing> Cart = new List<Thing>();

        public static List<Thing> CartTurret = new List<Thing>();

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
            if (!pawn.RaceProps.Humanlike)
                return null;

            foreach (Apparel apparel in pawn.apparel.WornApparel)
                if (apparel is Apparel_Backpack)
                    return apparel as Apparel_Backpack;
            return null;
        }

        public static Apparel_Toolbelt TryGetToolbelt(Pawn pawn)
        {
            if (!pawn.RaceProps.Humanlike)
                return null;
        
            foreach (Apparel apparel in pawn.apparel.WornApparel)
                if (apparel is Apparel_Toolbelt)
                    return apparel as Apparel_Toolbelt;
            return null;
        }

        /// <summary>
        ///     Calculates the actual current movement speed of a pawn
        /// </summary>
        /// <param name="pawn">Pawn to calculate speed of</param>
        /// <returns>Move speed in cells per second</returns>
        public static float GetMoveSpeed(Pawn pawn)
        {
            float movePerTick = 60 / pawn.GetStatValue(StatDefOf.MoveSpeed, false); //Movement per tick
            movePerTick += PathGrid.CalculatedCostAt(pawn.Position, false, pawn.Position);
            Building edifice = pawn.Position.GetEdifice();
            if (edifice != null)
            {
                movePerTick += edifice.PathWalkCostFor(pawn);
            }

            //Case switch to handle walking, jogging, etc.
            if (pawn.CurJob != null)
            {
                switch (pawn.CurJob.locomotionUrgency)
                {
                    case LocomotionUrgency.Amble:
                        movePerTick *= 3;
                        if (movePerTick < 60)
                        {
                            movePerTick = 60;
                        }
                        break;
                    case LocomotionUrgency.Walk:
                        movePerTick *= 2;
                        if (movePerTick < 50)
                        {
                            movePerTick = 50;
                        }
                        break;
                    case LocomotionUrgency.Jog:
                        break;
                    case LocomotionUrgency.Sprint:
                        movePerTick = Mathf.RoundToInt(movePerTick * 0.75f);
                        break;
                }
            }
            return 60 / movePerTick;
        }

        public static Thing TryGetBackpackLastItem(Pawn pawn)
        {
            Apparel_Backpack backpack = TryGetBackpack(pawn);
            if (backpack == null)
                return null;
            Thing lastItem = null;
            int lastItemInd = -1;
            Thing foodInInventory = FoodUtility.BestFoodInInventory(pawn);
            if (backpack.slotsComp.slots.Count > 0)
            {
                if (backpack.numOfSavedItems > 0)
                {
                    lastItemInd = (backpack.numOfSavedItems >= backpack.MaxItem
                        ? backpack.slotsComp.slots.Count
                        : backpack.numOfSavedItems) - 1;
                    lastItem = backpack.slotsComp.slots[lastItemInd];
                }
                if (foodInInventory != null && backpack.numOfSavedItems < backpack.slotsComp.slots.Count
                    && backpack.slotsComp.slots[lastItemInd + 1] == foodInInventory)
                    lastItem = foodInInventory;
            }
            return lastItem;
        }

        public static bool AvailableVehicle(Pawn pawn, ThingWithComps cart)
        {
            if (cart.Faction != Faction.OfPlayer) return false;
            if (cart.IsForbidden(pawn.Faction)) return false;
            if (cart.Position.IsForbidden(pawn)) return false;
            if (cart.IsBurning()) return false;
            if (!pawn.CanReserveAndReach(cart, PathEndMode.ClosestTouch, Danger.Some)) return false;

            if (!cart.TryGetComp<CompMountable>().IsMounted) return true;
            if (cart.TryGetComp<CompMountable>().Driver == pawn) return true;
            if (cart.TryGetComp<CompMountable>().IsMounted && cart.TryGetComp<CompMountable>().Driver.RaceProps.Animal) return true;
            return false;
        }

        public static bool AvailableAnimalCart(ThingWithComps cart)
        {
            Pawn Driver = cart.GetComp<CompMountable>().IsMounted ? cart.GetComp<CompMountable>().Driver : null;
            if (Driver == null)
                return false;

            return Driver.RaceProps.Animal && Driver.CasualInterruptibleNow()
                   && Driver.needs.food.CurCategory < HungerCategory.Starving
                   && Driver.needs.rest.CurCategory < RestCategory.VeryTired
                   && !Driver.health.ShouldBeTendedNow;
        }


        public static Job HaulWithTools(Pawn pawn, Vehicle_Cart cart = null, Thing haulThing = null)
        {
            Trace.stopWatchStart();

            //Job Setting
            bool UseBackpack = false;
            JobDef jobDef = null;
            TargetInfo targetC;
            int maxItem;
            int thresholdItem;
            int reservedMaxItem;
            IEnumerable<Thing> remainingItems;
            bool ShouldDrop;
            Thing lastItem = TryGetBackpackLastItem(pawn);
            Apparel_Backpack backpack = TryGetBackpack(pawn);
                if (cart == null)
                {
                    jobDef = HaulJobDefOf.HaulWithBackpack;
                    targetC = backpack;
                    maxItem = backpack.MaxItem;
                    // thresholdItem = (int)Math.Ceiling(maxItem * 0.5);
                    thresholdItem = 2;
                    reservedMaxItem = backpack.slotsComp.slots.Count;
                    remainingItems = backpack.slotsComp.slots;
                    ShouldDrop = false;
                    UseBackpack = true;
                    if (lastItem != null)
                        for (int i = 0; i < backpack.slotsComp.slots.Count; i++)
                            if (backpack.slotsComp.slots[i] == lastItem && reservedMaxItem - (i + 1) <= 0)
                            {
                                ShouldDrop = false;
                                break;
                            }
                }
                else
                {
                    if (cart.mountableComp.IsMounted)
                {
                    jobDef = cart.mountableComp.Driver.RaceProps.Animal
                        ? HaulJobDefOf.HaulWithAnimalCart
                        : HaulJobDefOf.HaulWithCart;
                }
                else
                    jobDef = HaulJobDefOf.HaulWithCart;

                targetC = cart;

                maxItem = cart.MaxItem;
                thresholdItem = (int)Math.Ceiling(maxItem * 0.25);
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

            if (UseBackpack)
                job.numToBringList = new List<int>();

            Trace.AppendLine(pawn.LabelCap + " In HaulWithTools: " + jobDef.defName + "\n"
                             + "MaxItem: " + maxItem + " reservedMaxItem: " + reservedMaxItem);

            //Drop remaining item
            if (ShouldDrop)
            {
                Trace.AppendLine("Start Drop remaining item");
                bool startDrop = false;
                for (int i = 0; i < remainingItems.Count(); i++)
                {
                  if (UseBackpack && startDrop == false)
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
                if (!UseBackpack && job.def == HaulJobDefOf.HaulWithCart && !cart.IsInValidStorage())
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
            if (haulThing != null) searchPos = haulThing.Position;
            else if (!UseBackpack) searchPos = cart.Position;
            else searchPos = pawn.Position;
            foreach (SlotGroup slotGroup in Find.SlotGroupManager.AllGroupsListInPriorityOrder)
            {
                Trace.AppendLine("Start searching slotGroup");
                if (slotGroup.CellsList.Count - slotGroup.HeldThings.Count() < maxItem)
                    continue;

                //Counting valid items
                Trace.AppendLine("Start Counting valid items");
                int thingsCount =
                    ListerHaulables.ThingsPotentiallyNeedingHauling()
                        .Count(item => slotGroup.Settings.AllowedToAccept(item));

                //Finding valid items
                Trace.AppendLine("Start Finding valid items");
                if (thingsCount > thresholdItem)
                {
                    Thing thing;
                    if (haulThing == null)
                    {
                        //ClosestThing_Global_Reachable Configuration
                        Predicate<Thing> predicate = item
                            => !job.targetQueueA.Contains(item) && !item.IsBurning()
                               && !item.IsInAnyStorage()
                               && (UseBackpack
                                   ? item.def.thingCategories.Exists(
                                       category =>
                                           backpack.slotsComp.Properties.allowedThingCategoryDefs.Exists(
                                               subCategory =>
                                                   subCategory.ThisAndChildCategoryDefs.Contains(category))
                                           &&
                                           !backpack.slotsComp.Properties.forbiddenSubThingCategoryDefs.Exists(
                                               subCategory =>
                                                   subCategory.ThisAndChildCategoryDefs.Contains(category)))
                                   : cart.allowances.Allows(item))
                               && !item.IsForbidden(pawn.Faction)
                               && slotGroup.Settings.AllowedToAccept(item)
                               && pawn.CanReserveAndReach(item, PathEndMode.Touch, pawn.NormalMaxDanger());
                        //&& !(item is UnfinishedThing && ((UnfinishedThing)item).BoundBill != null)
                        //&& (item.def.IsNutritionSource && !SocialProperness.IsSociallyProper(item, pawn, false, false));
                        thing = GenClosest.ClosestThing_Global_Reachable(searchPos,
                            ListerHaulables.ThingsPotentiallyNeedingHauling(),
                            PathEndMode.ClosestTouch,
                            TraverseParms.For(pawn, pawn.NormalMaxDanger()),
                            9999,
                            predicate);
                        if (thing == null)
                            continue;
                    }
                    else
                        thing = haulThing;

                    //Find StorageCell
                    IntVec3 storageCell = FindStorageCell(pawn, thing, job.targetQueueB);
                    if (storageCell == IntVec3.Invalid) break;

                    //Add Queue & Reserve
                    job.targetQueueA.Add(thing);
                    //for backpacks
                    if (UseBackpack)
                        job.numToBringList.Add(thing.def.stackLimit);

                    job.targetQueueB.Add(storageCell);


                    IntVec3 center = thing.Position;

                    //Enqueue SAME items in valid distance
                    Trace.AppendLine("Start Enqueuing SAME items in valid distance");
                    foreach (Thing item in ListerHaulables.ThingsPotentiallyNeedingHauling().Where(item
                        => !job.targetQueueA.Contains(item) && !item.IsBurning()
                           && !item.IsInAnyStorage()
                           && (UseBackpack
                               ? item.def.thingCategories.Exists(
                                   category =>
                                       backpack.slotsComp.Properties.allowedThingCategoryDefs.Exists(
                                           subCategory =>
                                               subCategory.ThisAndChildCategoryDefs.Contains(category))
                                       &&
                                       !backpack.slotsComp.Properties.forbiddenSubThingCategoryDefs.Exists(
                                           subCategory =>
                                               subCategory.ThisAndChildCategoryDefs.Contains(category)))
                               : cart.allowances.Allows(item))
                           && !item.IsForbidden(pawn.Faction)
                           && slotGroup.Settings.AllowedToAccept(item)
                           && pawn.CanReserveAndReach(item, PathEndMode.Touch, pawn.NormalMaxDanger()) &&
                           center.DistanceToSquared(item.Position) <= ValidDistance))
                    {
                        job.targetQueueA.Add(item);
                        if (UseBackpack)
                            job.numToBringList.Add(item.def.stackLimit);


                        reservedMaxItem++;

                        if (reservedMaxItem + job.targetQueueA.Count >= maxItem)
                            break;
                    }

                    //Enqueue other items in valid distance
                    if (reservedMaxItem + job.targetQueueA.Count < maxItem)
                    {
                        Trace.AppendLine("Start Enqueuing items in valid distance");

                        foreach (Thing item in ListerHaulables.ThingsPotentiallyNeedingHauling().Where(item
                            => !job.targetQueueA.Contains(item) && !item.IsBurning()
                               && !item.IsInAnyStorage()
                               && (UseBackpack
                                   ? item.def.thingCategories.Exists(
                                       category =>
                                           backpack.slotsComp.Properties.allowedThingCategoryDefs.Exists(
                                               subCategory =>
                                                   subCategory.ThisAndChildCategoryDefs.Contains(category))
                                           &&
                                           !backpack.slotsComp.Properties.forbiddenSubThingCategoryDefs.Exists(
                                               subCategory =>
                                                   subCategory.ThisAndChildCategoryDefs.Contains(category)))
                                   : cart.allowances.Allows(item))
                               && !item.IsForbidden(pawn.Faction)
                               && slotGroup.Settings.AllowedToAccept(item)
                               && pawn.CanReserveAndReach(item, PathEndMode.Touch, pawn.NormalMaxDanger())
                               && center.DistanceToSquared(item.Position) <= ValidDistance))
                        {
                            job.targetQueueA.Add(item);
                          if (UseBackpack)
                              job.numToBringList.Add(item.def.stackLimit);
                            reservedMaxItem++;

                            if (reservedMaxItem + job.targetQueueA.Count >= maxItem)
                                break;
                        }
                    }

                    //Also enqueue items in whiche are not in their best storage cell
                    if (reservedMaxItem + job.targetQueueA.Count < maxItem)
                    {
                        Trace.AppendLine("Start Enqueuing items in valid distance & not in best storage cell");
                        foreach (Thing item in ListerHaulables.ThingsPotentiallyNeedingHauling().Where(item
                            => !job.targetQueueA.Contains(item) && !item.IsBurning()
                               && !item.IsInValidBestStorage()
                               && (UseBackpack
                                   ? item.def.thingCategories.Exists(
                                       category =>
                                           backpack.slotsComp.Properties.allowedThingCategoryDefs.Exists(
                                               subCategory =>
                                                   subCategory.ThisAndChildCategoryDefs.Contains(category))
                                           &&
                                           !backpack.slotsComp.Properties.forbiddenSubThingCategoryDefs.Exists(
                                               subCategory =>
                                                   subCategory.ThisAndChildCategoryDefs.Contains(category)))
                                   : cart.allowances.Allows(item))
                               && !item.IsForbidden(pawn.Faction)
                               && slotGroup.Settings.AllowedToAccept(item)
                               && pawn.CanReserveAndReach(item, PathEndMode.Touch, pawn.NormalMaxDanger())
                               && center.DistanceToSquared(item.Position) <= ValidDistance))
                        {
                            job.targetQueueA.Add(item);
                          if (UseBackpack)
                              job.numToBringList.Add(item.def.stackLimit);
                            reservedMaxItem++;

                            if (reservedMaxItem + job.targetQueueA.Count >= maxItem)
                                break;
                        }
                    }

                    //Find storage cell
                    Trace.AppendLine("Start Finding storage cell");
                    if (reservedMaxItem + job.targetQueueA.Count > thresholdItem)
                    {
                        foreach (
                            IntVec3 cell in
                                slotGroup.CellsList.Where(
                                    cell =>
                                        pawn.CanReserveAndReach(cell, PathEndMode.ClosestTouch, Danger.Some) &&
                                        cell.Standable() && cell.GetStorable() == null))
                        {
                            StoragePriority currentPriority = HaulAIUtility.StoragePriorityAtFor(thing.Position, thing);
                            IntVec3 storeCell = cell;
                            if (
                                !StoreUtility.TryFindBestBetterStoreCellFor(thing, pawn, currentPriority, pawn.Faction,
                                    out storeCell))
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
            if (cart != null && job.def == HaulJobDefOf.HaulWithCart && !cart.IsInValidStorage())
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

        public static Job DismountInBase(Pawn pawn, Thing cart)
        {
            Job job = new Job(HaulJobDefOf.DismountInBase);
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
                        if (!targetQueue.Contains(adjCell) && adjCell.IsValidStorageFor(haulable) &&
                            pawn.CanReserveAndReach(adjCell, PathEndMode.ClosestTouch, Danger.Some))
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

        public static Vehicle_Cart FindWheelChair(Pawn patient, Pawn pawn)
        {
            foreach (Vehicle_Cart vehicle in Cart)
            {
                if (vehicle.vehicleComp.IsMedical() && vehicle.mountableComp.Driver == null &&
                    pawn.CanReserveAndReach(vehicle.InteractionCell, PathEndMode.ClosestTouch, Danger.Some) &&
                    vehicle.Faction == pawn.Faction)
                {
                    Debug.Log("Wheel chair found");
                    return vehicle;
                }
            }
            Debug.Log("No wheel chair found");
            return null;
        }

        public static bool IsDriver(Pawn pawn)
        {
            foreach (Vehicle_Cart vehicle in Cart)
                if (vehicle.mountableComp.Driver == pawn)
                    return true;
            foreach (Vehicle_Turret vehicle in CartTurret)
                if (vehicle.mountableComp.Driver == pawn)
                    return true;
            return false;
        }

        public static Vehicle_Cart GetCartByDriver(Pawn pawn)
        {
            foreach (Vehicle_Cart vehicle in Cart)
                if (vehicle.mountableComp.Driver == pawn)
                {
                    return vehicle;
                }
            return null;
        }

        public static Vehicle_Turret GetTurretByDriver(Pawn pawn)
        {
            foreach (Vehicle_Turret vehicleTurret in CartTurret)
                if (vehicleTurret.mountableComp.Driver == pawn)
                {
                    return vehicleTurret;
                }
            return null;
        }

        public static bool IsDriverOfThisVehicle(Pawn pawn, Thing vehicleReq)
        {
            foreach (Vehicle_Cart vehicle in Cart)
                if (vehicle.mountableComp.Driver == pawn && vehicle == vehicleReq)
                    return true;
            foreach (Vehicle_Turret vehicleTurret in CartTurret)
                if (vehicleTurret.mountableComp.Driver == pawn && vehicleTurret == vehicleReq)
                    return true;
            return false;
        }
    }
}