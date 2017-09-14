﻿namespace TFH_Incidents
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    using RimWorld;

    using TFH_VehicleBase.DefOfs_TFH;

    using TFH_Vehicles;
    using TFH_Vehicles.DefOfs_TFH;

    using Verse;
    using Verse.AI;
    using Verse.AI.Group;

    public abstract class IncidentWorker_Raid_TFH : IncidentWorker_PawnsArrive
    {
        protected abstract bool TryResolveRaidFaction(IncidentParms parms);

        protected abstract void ResolveRaidStrategy(IncidentParms parms);

        protected abstract string GetLetterLabel(IncidentParms parms);

        protected abstract string GetLetterText(IncidentParms parms, List<Pawn> pawns);

        protected abstract LetterDef GetLetterDef();

        protected abstract string GetRelatedPawnsInfoLetterText(IncidentParms parms);

        protected abstract void ResolveRaidPoints(IncidentParms parms);

        protected virtual void ResolveRaidArriveMode(IncidentParms parms)
        {
            if (parms.raidArrivalMode != PawnsArriveMode.Undecided)
            {
                return;
            }

            if (parms.faction.def.techLevel < TechLevel.Spacer || parms.points < 240f)
            {
                parms.raidArrivalMode = PawnsArriveMode.EdgeWalkIn;
            }
            else
            {
                parms.raidArrivalMode = parms.raidStrategy.arriveModes.RandomElementByWeight(delegate (PawnsArriveMode am)
                {
                    switch (am)
                    {
                        case PawnsArriveMode.EdgeWalkIn:
                            return 70f;
                        case PawnsArriveMode.EdgeDrop:
                            return 20f;
                        case PawnsArriveMode.CenterDrop:
                            return 10f;
                        default:
                            throw new NotImplementedException();
                    }
                });
            }
        }

        protected virtual bool ResolveRaidSpawnCenter(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            if (parms.spawnCenter.IsValid)
            {
                return true;
            }

            if (parms.raidArrivalMode == PawnsArriveMode.CenterDrop || parms.raidArrivalMode == PawnsArriveMode.EdgeDrop)
            {
                if (parms.raidArrivalMode == PawnsArriveMode.CenterDrop)
                {
                    parms.raidPodOpenDelay = 520;
                    parms.spawnRotation = Rot4.Random;
                    if (Rand.Value < 0.4f && map.listerBuildings.ColonistsHaveBuildingWithPowerOn(ThingDefOf.OrbitalTradeBeacon))
                    {
                        parms.spawnCenter = DropCellFinder.TradeDropSpot(map);
                    }
                    else if (!DropCellFinder.TryFindRaidDropCenterClose(out parms.spawnCenter, map))
                    {
                        parms.raidArrivalMode = PawnsArriveMode.EdgeDrop;
                    }
                }

                if (parms.raidArrivalMode == PawnsArriveMode.EdgeDrop)
                {
                    parms.raidPodOpenDelay = 140;
                    parms.spawnCenter = DropCellFinder.FindRaidDropCenterDistant(map);
                    parms.spawnRotation = Rot4.Random;
                }
            }
            else
            {
                if (!RCellFinder.TryFindRandomPawnEntryCell(out parms.spawnCenter, map, CellFinder.EdgeRoadChance_Hostile))
                {
                    return false;
                }

                parms.spawnRotation = Rot4.FromAngleFlat((map.Center - parms.spawnCenter).AngleFlat);
            }

            return true;
        }

        public override bool TryExecute(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            this.ResolveRaidPoints(parms);
            if (!this.TryResolveRaidFaction(parms))
            {
                return false;
            }

            this.ResolveRaidStrategy(parms);
            this.ResolveRaidArriveMode(parms);
            if (!this.ResolveRaidSpawnCenter(parms))
            {
                return false;
            }

            IncidentParmsUtility.AdjustPointsForGroupArrivalParams(parms);
            PawnGroupMakerParms defaultPawnGroupMakerParms = IncidentParmsUtility.GetDefaultPawnGroupMakerParms(parms);
            List<Pawn> list = PawnGroupMakerUtility.GeneratePawns(PawnGroupKindDefOf.Normal, defaultPawnGroupMakerParms).ToList();
            if (list.Count == 0)
            {
                Log.Error("Got no pawns spawning raid from parms " + parms);
                return false;
            }

            TargetInfo target = TargetInfo.Invalid;
            if (parms.raidArrivalMode == PawnsArriveMode.CenterDrop || parms.raidArrivalMode == PawnsArriveMode.EdgeDrop)
            {
                DropPodUtility.DropThingsNear(parms.spawnCenter, map, list.Cast<Thing>(), parms.raidPodOpenDelay, false, true);
                target = new TargetInfo(parms.spawnCenter, map);
            }
            else
            {
                foreach (Pawn current in list)
                {
                    IntVec3 loc = CellFinder.RandomClosewalkCellNear(parms.spawnCenter, map, 8);
                    GenSpawn.Spawn(current, loc, map, parms.spawnRotation);
                    target = current;
                    {

                        // Vehicles for raiders
                        // lowered probability for shield users as they are overpowered
                        float value = Rand.Value;
                        bool isShieldUser = false;

                        if (parms.faction.def.techLevel >= TechLevel.Industrial && current.RaceProps.FleshType != FleshTypeDefOf.Mechanoid && current.RaceProps.ToolUser)
                        {
                            List<Apparel> wornApparel = current.apparel.WornApparel;
                            for (int i = 0; i < wornApparel.Count; i++)
                            {
                                if (wornApparel[i].def == ThingDefOf.Apparel_ShieldBelt)
                                {
                                    isShieldUser = true;
                                    break;
                                }
                            }

                            if (current.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
                            {
                                if (value >= 0.66f && !isShieldUser || isShieldUser && value > 0.9f)
                                {
                                    CellFinder.RandomClosewalkCellNear(current.Position, current.Map, 5);
                                    Pawn cart = PawnGenerator.GeneratePawn(VehicleKindDefOf.TFH_ATV, parms.faction);

                                    if (value >= 0.9f && !isShieldUser)
                                    {
                                        cart = PawnGenerator.GeneratePawn(
                                            VehicleKindDefOf.TFH_CombatATV,
                                            parms.faction);
                                    }

                                    GenSpawn.Spawn(cart, current.Position, map, Rot4.Random, false);

                                    current.Map.reservationManager.ReleaseAllForTarget(cart);
                                    Job job = new Job(VehicleJobDefOf.Mount) { targetA = cart };
                                    current.Reserve(cart);
                                    current.jobs.jobQueue.EnqueueFirst(job);
                                }
                            }
                        }
                    }
                }
            }

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("Points = " + parms.points.ToString("F0"));
            foreach (Pawn current2 in list)
            {
                string str = (current2.equipment == null || current2.equipment.Primary == null) ? "unarmed" : current2.equipment.Primary.LabelCap;
                stringBuilder.AppendLine(current2.KindLabel + " - " + str);
            }

            string letterLabel = this.GetLetterLabel(parms);
            string letterText = this.GetLetterText(parms, list);
            PawnRelationUtility.Notify_PawnsSeenByPlayer(list, ref letterLabel, ref letterText, this.GetRelatedPawnsInfoLetterText(parms), true);
            Find.LetterStack.ReceiveLetter(letterLabel, letterText, this.GetLetterDef(), target, stringBuilder.ToString());
            if (this.GetLetterDef() == LetterDefOf.BadUrgent)
            {
                TaleRecorder.RecordTale(TaleDefOf.RaidArrived);
            }

            Lord lord = LordMaker.MakeNewLord(parms.faction, parms.raidStrategy.Worker.MakeLordJob(parms, map), map, list);
            AvoidGridMaker.RegenerateAvoidGridsFor(parms.faction, map);
            LessonAutoActivator.TeachOpportunity(ConceptDefOf.EquippingWeapons, OpportunityType.Critical);
            if (!PlayerKnowledgeDatabase.IsComplete(ConceptDefOf.ShieldBelts))
            {
                for (int i = 0; i < list.Count; i++)
                {
                    Pawn pawn = list[i];
                    if (pawn.apparel.WornApparel.Any(ap => ap is ShieldBelt))
                    {
                        LessonAutoActivator.TeachOpportunity(ConceptDefOf.ShieldBelts, OpportunityType.Critical);
                        break;
                    }
                }
            }

            if (DebugViewSettings.drawStealDebug && parms.faction.HostileTo(Faction.OfPlayer))
            {
                Log.Message(
                    string.Concat(
                        "Market value threshold to start stealing: ",
                        StealAIUtility.StartStealingMarketValueThreshold(lord),
                        " (colony wealth = ",
                        map.wealthWatcher.WealthTotal,
                        ")"));
            }

            return true;
        }
    }
}
