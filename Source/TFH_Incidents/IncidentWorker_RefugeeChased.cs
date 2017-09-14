﻿namespace TFH_Incidents
{
    using System.Linq;

    using RimWorld;

    using TFH_VehicleBase;
    using TFH_VehicleBase.DefOfs_TFH;

    using TFH_Vehicles;
    using TFH_Vehicles.DefOfs_TFH;

    using Verse;
    using Verse.AI;

    // Vanilla copy with spawning vehicles
    public class IncidentWorker_RefugeeChased : IncidentWorker
    {
        private const float RaidPointsFactor = 1.35f;

        private const float RelationWithColonistWeight = 20f;

        private static readonly IntRange RaidDelay = new IntRange(1000, 2500);

        public override bool TryExecute(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            IntVec3 spawnSpot;
            if (!CellFinder.TryFindRandomEdgeCellWith(c => map.reachability.CanReachColony(c), map, CellFinder.EdgeRoadChance_Neutral, out spawnSpot))
            {
                return false;
            }

            Faction faction = Find.FactionManager.FirstFactionOfDef(FactionDefOf.Spacer);
            PawnGenerationRequest request = new PawnGenerationRequest(PawnKindDefOf.SpaceRefugee, faction, PawnGenerationContext.NonPlayer, -1, false, false, false, false, true, false, 20f, false, true, true, false, false, null, null, null, null, null);
            Pawn refugee = PawnGenerator.GeneratePawn(request);
            refugee.relations.everSeenByPlayer = true;
            Faction enemyFac;
            if (!(from f in Find.FactionManager.AllFactions
                  where !f.def.hidden && f.HostileTo(Faction.OfPlayer)
                  select f).TryRandomElement(out enemyFac))
            {
                return false;
            }

            string text = "RefugeeChasedInitial".Translate(
                refugee.Name.ToStringFull,
                refugee.story.Title.ToLower(),
                enemyFac.def.pawnsPlural,
                enemyFac.Name,
                refugee.ageTracker.AgeBiologicalYears);
            text = text.AdjustedFor(refugee);
            PawnRelationUtility.TryAppendRelationsWithColonistsInfo(ref text, refugee);
            DiaNode diaNode = new DiaNode(text);
            DiaOption diaOption = new DiaOption("RefugeeChasedInitial_Accept".Translate());
            diaOption.action = delegate
                {
                    GenSpawn.Spawn(refugee, spawnSpot, map);
                    refugee.SetFaction(Faction.OfPlayer);
                    CameraJumper.TryJump(refugee);
                    IncidentParms incidentParms = StorytellerUtility.DefaultParmsNow(Find.Storyteller.def, IncidentCategory.ThreatBig, map);
                    incidentParms.forced = true;
                    incidentParms.faction = enemyFac;
                    incidentParms.raidStrategy = RaidStrategyDefOf.ImmediateAttack;
                    incidentParms.raidArrivalMode = PawnsArriveMode.EdgeWalkIn;
                    incidentParms.spawnCenter = spawnSpot;
                    incidentParms.points *= 1.35f;
                    QueuedIncident qi = new QueuedIncident(new FiringIncident(IncidentDefOf.RaidEnemy, null, incidentParms), Find.TickManager.TicksGame + RaidDelay.RandomInRange);
                    Find.Storyteller.incidentQueue.Add(qi);
                };
            diaOption.resolveTree = true;
            diaNode.options.Add(diaOption);
            string text2 = "RefugeeChasedRejected".Translate(refugee.NameStringShort);
            DiaNode diaNode2 = new DiaNode(text2);
            DiaOption diaOption2 = new DiaOption("OK".Translate());
            diaOption2.resolveTree = true;
            diaNode2.options.Add(diaOption2);
            DiaOption diaOption3 = new DiaOption("RefugeeChasedInitial_Reject".Translate());
            diaOption3.action = delegate
                {
                    Find.WorldPawns.PassToWorld(refugee);
                };
            diaOption3.link = diaNode2;
            diaNode.options.Add(diaOption3);
            string title = "RefugeeChasedTitle".Translate(map.info.parent.Label);

            // Vehicle
            if (refugee.RaceProps.ToolUser)
            {
                float value = Rand.Value;

                if (refugee.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
                {
                    if (value >= 0.2f)
                    {
                        CellFinder.RandomClosewalkCellNear(refugee.Position, refugee.Map, 5);

                        Pawn cart = PawnGenerator.GeneratePawn(VehicleKindDefOf.TFH_SmackWay, parms.faction);

                        if (value >= 0.7f)
                        {
                            cart = PawnGenerator.GeneratePawn(VehicleKindDefOf.TFH_Speeder, parms.faction);
                        }

                        GenSpawn.Spawn(cart, refugee.Position, map, Rot4.Random, false);

                        refugee.Map.reservationManager.ReleaseAllForTarget(cart);
                        Job job = new Job(VehicleJobDefOf.Mount) { targetA = cart };
                        refugee.Reserve(cart);
                        refugee.jobs.jobQueue.EnqueueFirst(job);
                    }
                }
            }

            Find.WindowStack.Add(new Dialog_NodeTree(diaNode, true, true, title));
            return true;
        }
    }
}
