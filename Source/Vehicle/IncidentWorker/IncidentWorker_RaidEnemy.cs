﻿using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using ToolsForHaul.Components;
using ToolsForHaul.JobDefs;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;

namespace ToolsForHaul.IncidentWorker
{
    using ToolsForHaul.Components.Vehicle;
    using ToolsForHaul.Components.Vehicles;

    public class IncidentWorker_RaidEnemy : IncidentWorker_Raid
    {

        protected override bool CanFireNowSub()
        {
            return base.CanFireNowSub() && GenTemperature.OutdoorTemp < 55.0 && GenTemperature.OutdoorTemp > -55.0;
        }

        protected override bool FactionCanBeGroupSource(Faction f, bool desperate = false)
        {
            return base.FactionCanBeGroupSource(f, desperate) && f.HostileTo(Faction.OfPlayer) && (desperate || GenDate.DaysPassed >= f.def.earliestRaidDays);
        }

        protected override string GetLetterLabel(IncidentParms parms)
        {
            return parms.raidStrategy.letterLabelEnemy;
        }

        public override bool TryExecute(IncidentParms parms)
        {
            {
                // if (!base.TryExecute(parms))
                this.ResolveRaidPoints(parms);
                if (!this.TryResolveRaidFaction(parms))
                {
                    return false;
                }

                this.ResolveRaidStrategy(parms);
                this.ResolveRaidArriveMode(parms);
                this.ResolveRaidSpawnCenter(parms);
                PawnGroupMakerUtility.AdjustPointsForGroupArrivalParams(parms);
                List<Pawn> list = PawnGroupMakerUtility.GenerateArrivingPawns(parms).ToList();
                if (list.Count == 0)
                {
                    Log.Error("Got no pawns spawning raid from parms " + parms);
                    return false;
                }

                TargetInfo letterLookTarget = TargetInfo.Invalid;
                if (parms.raidArrivalMode == PawnsArriveMode.CenterDrop || parms.raidArrivalMode == PawnsArriveMode.EdgeDrop)
                {
                    DropPodUtility.DropThingsNear(parms.spawnCenter, list.Cast<Thing>(), parms.raidPodOpenDelay, true, true);
                    letterLookTarget = parms.spawnCenter;
                }
                else
                {
                    foreach (Pawn current in list)
                    {
                        float value = Rand.Value;
                        IntVec3 intVec = CellFinder.RandomClosewalkCellNear(parms.spawnCenter, 8);
                        GenSpawn.Spawn(current, intVec);

                        letterLookTarget = current;

                        // Vehicles for raiders
                        // lowered probability for shield users as they are overpowered
                        bool isShieldUser = false;

                        if (parms.faction.def.techLevel >= TechLevel.Industrial && current.RaceProps.fleshType != FleshType.Mechanoid && current.RaceProps.ToolUser)
                        {
                            List<Apparel> wornApparel = current.apparel.WornApparel;
                            for (int i = 0; i < wornApparel.Count; i++)
                            {
                                if (wornApparel[i] is PersonalShield)
                                {
                                    isShieldUser = true;
                                    break;
                                }
                            }

                            if (value >= 0.66f && !isShieldUser || isShieldUser && value > 0.9f)
                            {
                                CellFinder.RandomClosewalkCellNear(current.Position, 5);
                                Thing thing = ThingMaker.MakeThing(ThingDef.Named("VehicleATV"));

                                if (value >= 0.9f && !isShieldUser)
                                {
                                    thing = ThingMaker.MakeThing(ThingDef.Named("VehicleCombatATV"));
                                }

                                GenSpawn.Spawn(thing, current.Position);

                                Job job = new Job(HaulJobDefOf.Mount);
                                Find.Reservations.ReleaseAllForTarget(thing);
                                job.targetA = thing;
                                current.jobs.StartJob(job, JobCondition.InterruptForced, null, true);

                                int num2 = Mathf.FloorToInt(Rand.Value * 0.2f * thing.MaxHitPoints);
                                thing.TakeDamage(new DamageInfo(DamageDefOf.Deterioration, num2, null, null));

                                SoundInfo info = SoundInfo.InWorld(thing);
                                thing.TryGetComp<CompMountable>().SustainerAmbient = thing.TryGetComp<CompVehicle>().compProps.soundAmbient.TrySpawnSustainer(info);
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

                Find.LetterStack.ReceiveLetter(this.GetLetterLabel(parms), this.GetLetterText(parms, list), this.GetLetterType(), letterLookTarget, stringBuilder.ToString());
                if (this.GetLetterType() == LetterType.BadUrgent)
                {
                    TaleRecorder.RecordTale(TaleDefOf.RaidArrived);
                }

                PawnRelationUtility.Notify_PawnsSeenByPlayer(list, this.GetRelatedPawnsInfoLetterText(parms), true);
                Lord lord = LordMaker.MakeNewLord(parms.faction, parms.raidStrategy.Worker.MakeLordJob(ref parms), list);
                AvoidGridMaker.RegenerateAvoidGridsFor(parms.faction);
                LessonAutoActivator.TeachOpportunity(ConceptDefOf.EquippingWeapons, OpportunityType.Critical);
                if (!PlayerKnowledgeDatabase.IsComplete(ConceptDefOf.PersonalShields))
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        Pawn pawn = list[i];
                        if (pawn.apparel.WornApparel.Any(ap => ap is PersonalShield))
                        {
                            LessonAutoActivator.TeachOpportunity(ConceptDefOf.PersonalShields, OpportunityType.Critical);
                            break;
                        }
                    }
                }

                if (DebugViewSettings.drawStealDebug && parms.faction.HostileTo(Faction.OfPlayer))
                {
                    Log.Message(string.Concat("Market value threshold to start stealing: ", StealAIUtility.StartStealingMarketValueThreshold(lord), " (colony wealth = ", Find.StoryWatcher.watcherWealth.WealthTotal, ")"));
                }
            }

            Find.TickManager.slower.SignalForceNormalSpeedShort();
            Find.StoryWatcher.statsRecord.numRaidsEnemy++;

            return true;
        }

        // RimWorld.IncidentWorker_RaidEnemy
        protected override bool TryResolveRaidFaction(IncidentParms parms)
        {
            if (parms.faction != null)
            {
                return true;
            }

            float maxPoints = parms.points;
            if (maxPoints <= 0f)
            {
                maxPoints = 999999f;
            }

            if (!(from f in Find.FactionManager.AllFactions
                  where this.FactionCanBeGroupSource(f) && maxPoints >= f.def.MinPointsToGenerateNormalPawnGroup()
                  select f).TryRandomElementByWeight(f => f.def.raidCommonality, out parms.faction))
            {
                if (!(from f in Find.FactionManager.AllFactions
                      where this.FactionCanBeGroupSource(f, true) && maxPoints >= f.def.MinPointsToGenerateNormalPawnGroup()
                      select f).TryRandomElementByWeight(f => f.def.raidCommonality, out parms.faction))
                {
                    Log.Error("IncidentWorker_RaidEnemy could not fire even though we thought we could: no faction could generate with " + maxPoints + " points.");
                    return false;
                }
            }

            return true;
        }


        protected override void ResolveRaidStrategy(IncidentParms parms)
        {
            if (parms.raidStrategy != null)
            {
                return;
            }

            parms.raidStrategy = (from d in DefDatabase<RaidStrategyDef>.AllDefs
                                  where d.Worker.CanUseWith(parms)
                                  select d).RandomElementByWeight(d => d.Worker.SelectionChance);
        }

        // RimWorld.IncidentWorker_RaidEnemy
        protected override string GetLetterText(IncidentParms parms, List<Pawn> pawns)
        {
            string text = null;
            switch (parms.raidArrivalMode)
            {
                case PawnsArriveMode.EdgeWalkIn:
                    text = "EnemyRaidWalkIn".Translate(parms.faction.def.pawnsPlural, parms.faction.Name);
                    break;
                case PawnsArriveMode.EdgeDrop:
                    text = "EnemyRaidEdgeDrop".Translate(parms.faction.def.pawnsPlural, parms.faction.Name);
                    break;
                case PawnsArriveMode.CenterDrop:
                    text = "EnemyRaidCenterDrop".Translate(parms.faction.def.pawnsPlural, parms.faction.Name);
                    break;
            }

            text += "\n\n";
            text += parms.raidStrategy.arrivalTextEnemy;
            Pawn pawn = pawns.Find(x => x.Faction.leader == x);
            if (pawn != null)
            {
                text += "\n\n";
                text += "EnemyRaidLeaderPresent".Translate(pawn.Faction.def.pawnsPlural, pawn.LabelShort);
            }

            return text;
        }

        protected override LetterType GetLetterType()
        {
            return LetterType.BadUrgent;
        }

        // RimWorld.IncidentWorker_RaidEnemy
        protected override string GetRelatedPawnsInfoLetterText(IncidentParms parms)
        {
            return "LetterRelatedPawnsRaidEnemy".Translate(parms.faction.def.pawnsPlural);
        }


    }
}
