﻿namespace ToolsForHaul.IncidentWorker
{
    using System.Collections.Generic;
    using System.Linq;

    using RimWorld;

    using Verse;

    public class IncidentWorker_RaidEnemy : IncidentWorker_Raid_TFH
    {
        protected override bool FactionCanBeGroupSource(Faction f, Map map, bool desperate = false)
        {
            return base.FactionCanBeGroupSource(f, map, desperate) && f.HostileTo(Faction.OfPlayer) && (desperate || (float)GenDate.DaysPassed >= f.def.earliestRaidDays);
        }

        public override bool TryExecute(IncidentParms parms)
        {
            if (!base.TryExecute(parms))
            {
                return false;
            }
            Find.TickManager.slower.SignalForceNormalSpeedShort();
            Find.StoryWatcher.statsRecord.numRaidsEnemy++;
            return true;
        }

        protected override bool TryResolveRaidFaction(IncidentParms parms)
        {
            Map map = (Map)parms.target;
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
                  where this.FactionCanBeGroupSource(f, map, false) && maxPoints >= f.def.MinPointsToGenerateNormalPawnGroup()
                  select f).TryRandomElementByWeight((Faction f) => f.def.raidCommonality, out parms.faction))
            {
                if (!(from f in Find.FactionManager.AllFactions
                      where this.FactionCanBeGroupSource(f, map, true) && maxPoints >= f.def.MinPointsToGenerateNormalPawnGroup()
                      select f).TryRandomElementByWeight((Faction f) => f.def.raidCommonality, out parms.faction))
                {
                    return false;
                }
            }
            return true;
        }

        protected override void ResolveRaidPoints(IncidentParms parms)
        {
            if (parms.points <= 0f)
            {
                Log.Error("RaidEnemy is resolving raid points. They should always be set before initiating the incident.");
                parms.points = (float)Rand.Range(50, 300);
            }
        }

        protected override void ResolveRaidStrategy(IncidentParms parms)
        {
            if (parms.raidStrategy != null)
            {
                return;
            }
            Map map = (Map)parms.target;
            parms.raidStrategy = (from d in DefDatabase<RaidStrategyDef>.AllDefs
                                  where d.Worker.CanUseWith(parms)
                                  select d).RandomElementByWeight((RaidStrategyDef d) => d.Worker.SelectionChance(map));
        }

        protected override string GetLetterLabel(IncidentParms parms)
        {
            return parms.raidStrategy.letterLabelEnemy;
        }

        protected override string GetLetterText(IncidentParms parms, List<Pawn> pawns)
        {
            string text = null;
            switch (parms.raidArrivalMode)
            {
                case PawnsArriveMode.EdgeWalkIn:
                    text = "EnemyRaidWalkIn".Translate(new object[]
                                                           {
                                                               parms.faction.def.pawnsPlural,
                                                               parms.faction.Name
                                                           });
                    break;
                case PawnsArriveMode.EdgeDrop:
                    text = "EnemyRaidEdgeDrop".Translate(new object[]
                                                             {
                                                                 parms.faction.def.pawnsPlural,
                                                                 parms.faction.Name
                                                             });
                    break;
                case PawnsArriveMode.CenterDrop:
                    text = "EnemyRaidCenterDrop".Translate(new object[]
                                                               {
                                                                   parms.faction.def.pawnsPlural,
                                                                   parms.faction.Name
                                                               });
                    break;
            }
            text += "\n\n";
            text += parms.raidStrategy.arrivalTextEnemy;
            Pawn pawn = pawns.Find((Pawn x) => x.Faction.leader == x);
            if (pawn != null)
            {
                text += "\n\n";
                text += "EnemyRaidLeaderPresent".Translate(new object[]
                                                               {
                                                                   pawn.Faction.def.pawnsPlural,
                                                                   pawn.LabelShort
                                                               });
            }
            return text;
        }

        protected override LetterDef GetLetterDef()
        {
            return LetterDefOf.BadUrgent;
        }

        protected override string GetRelatedPawnsInfoLetterText(IncidentParms parms)
        {
            return "LetterRelatedPawnsRaidEnemy".Translate(new object[]
                                                               {
                                                                   parms.faction.def.pawnsPlural
                                                               });
        }
    }
}
