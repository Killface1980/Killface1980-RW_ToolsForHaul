﻿// Verse.Pawn_InventoryTracker
using RimWorld;
using System;
using System.Collections.Generic;
using Verse;

public class Pawn_InventoryTracker2 : IThingHolder, IExposable
{
    public Pawn pawn;

    public ThingOwner<Thing> innerContainer;

    private bool unloadEverything;

    private List<Thing> itemsNotForSale = new List<Thing>();

    private static List<ThingCount> tmpDrugsToKeep = new List<ThingCount>();

    private static List<Thing> tmpThingList = new List<Thing>();

    public bool UnloadEverything
    {
        get
        {
            return this.unloadEverything && this.HasAnyUnloadableThing;
        }
        set
        {
            if (value && this.HasAnyUnloadableThing)
            {
                this.unloadEverything = true;
            }
            else
            {
                this.unloadEverything = false;
            }
        }
    }

    private bool HasAnyUnloadableThing
    {
        get
        {
            return this.FirstUnloadableThing != default(ThingStackPart);
        }
    }

    public ThingStackPart FirstUnloadableThing
    {
        get
        {
            if (this.innerContainer.Count == 0)
            {
                return default(ThingStackPart);
            }
            if (this.pawn.drugs != null && this.pawn.drugs.CurrentPolicy != null)
            {
                DrugPolicy currentPolicy = this.pawn.drugs.CurrentPolicy;
                Pawn_InventoryTracker2.tmpDrugsToKeep.Clear();
                for (int i = 0; i < currentPolicy.Count; i++)
                {
                    if (currentPolicy[i].takeToInventory > 0)
                    {
                        Pawn_InventoryTracker2.tmpDrugsToKeep.Add(new ThingCount(currentPolicy[i].drug, currentPolicy[i].takeToInventory));
                    }
                }
                for (int j = 0; j < this.innerContainer.Count; j++)
                {
                    if (!this.innerContainer[j].def.IsDrug)
                    {
                        return new ThingStackPart(this.innerContainer[j], this.innerContainer[j].stackCount);
                    }
                    int num = -1;
                    int num2 = 0;
                    while (num2 < Pawn_InventoryTracker2.tmpDrugsToKeep.Count)
                    {
                        if (this.innerContainer[j].def != Pawn_InventoryTracker2.tmpDrugsToKeep[num2].ThingDef)
                        {
                            num2++;
                            continue;
                        }
                        num = num2;
                        break;
                    }
                    if (num < 0)
                    {
                        return new ThingStackPart(this.innerContainer[j], this.innerContainer[j].stackCount);
                    }
                    if (this.innerContainer[j].stackCount > Pawn_InventoryTracker2.tmpDrugsToKeep[num].Count)
                    {
                        return new ThingStackPart(this.innerContainer[j], this.innerContainer[j].stackCount - Pawn_InventoryTracker2.tmpDrugsToKeep[num].Count);
                    }
                    Pawn_InventoryTracker2.tmpDrugsToKeep[num] = new ThingCount(Pawn_InventoryTracker2.tmpDrugsToKeep[num].ThingDef, Pawn_InventoryTracker2.tmpDrugsToKeep[num].Count - this.innerContainer[j].stackCount);
                }
                return default(ThingStackPart);
            }
            return new ThingStackPart(this.innerContainer[0], this.innerContainer[0].stackCount);
        }
    }

    public IThingHolder ParentHolder
    {
        get
        {
            return this.pawn;
        }
    }

    public Pawn_InventoryTracker2(Pawn pawn)
    {
        this.pawn = pawn;
        this.innerContainer = new ThingOwner<Thing>(this, false, LookMode.Deep);
        Log.Message("TRACKER INITIALIZED");
    }

    public void ExposeData()
    {
        Scribe_Collections.Look<Thing>(ref this.itemsNotForSale, "itemsNotForSale", LookMode.Reference, new object[0]);
        Scribe_Deep.Look<ThingOwner<Thing>>(ref this.innerContainer, "innerContainer", new object[1]
                                                                                           {
                                                                                               this
                                                                                           });
        Scribe_Values.Look<bool>(ref this.unloadEverything, "unloadEverything", false, false);
    }

    public void InventoryTrackerTick()
    {
        this.innerContainer.ThingOwnerTick(true);
        if (this.unloadEverything && !this.HasAnyUnloadableThing)
        {
            this.unloadEverything = false;
        }
    }

    public void InventoryTrackerTickRare()
    {
        this.innerContainer.ThingOwnerTickRare(true);
    }

    public void DropAllNearPawn(IntVec3 pos, bool forbid = false, bool unforbid = false)
    {
        if (this.pawn.MapHeld == null)
        {
            Log.Error("Tried to drop all inventory near pawn but the pawn is unspawned. pawn=" + this.pawn);
        }
        else
        {
            Pawn_InventoryTracker2.tmpThingList.Clear();
            Pawn_InventoryTracker2.tmpThingList.AddRange(this.innerContainer);
            for (int i = 0; i < Pawn_InventoryTracker2.tmpThingList.Count; i++)
            {
                Thing thing = default(Thing);
                this.innerContainer.TryDrop(Pawn_InventoryTracker2.tmpThingList[i], pos, this.pawn.MapHeld, ThingPlaceMode.Near, out thing, (Action<Thing, int>)delegate (Thing t, int unused)
                    {
                        if (forbid)
                        {
                            t.SetForbiddenIfOutsideHomeArea();
                        }
                        if (unforbid)
                        {
                            t.SetForbidden(false, false);
                        }
                        if (t.def.IsPleasureDrug)
                        {
                            LessonAutoActivator.TeachOpportunity(ConceptDefOf.DrugBurning, OpportunityType.Important);
                        }
                    });
            }
        }
    }

    public void DestroyAll(DestroyMode mode = DestroyMode.Vanish)
    {
        this.innerContainer.ClearAndDestroyContents(mode);
    }

    public bool Contains(Thing item)
    {
        return this.innerContainer.Contains(item);
    }

    public bool NotForSale(Thing item)
    {
        return this.itemsNotForSale.Contains(item);
    }

    public void TryAddItemNotForSale(Thing item)
    {
        if (this.innerContainer.TryAdd(item, false))
        {
            this.itemsNotForSale.Add(item);
        }
    }

    public void Notify_ItemRemoved(Thing item)
    {
        this.itemsNotForSale.Remove(item);
        if (this.unloadEverything && !this.HasAnyUnloadableThing)
        {
            this.unloadEverything = false;
        }
    }

    public ThingOwner GetDirectlyHeldThings()
    {
        return this.innerContainer;
    }

    public void GetChildHolders(List<IThingHolder> outChildren)
    {
        ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, this.GetDirectlyHeldThings());
    }
}
