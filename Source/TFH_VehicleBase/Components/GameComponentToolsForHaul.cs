﻿namespace TFH_VehicleBase.Components
{
    using System.Collections.Generic;

    using RimWorld;

    using Verse;

    public class GameComponentToolsForHaul : GameComponent
    {

        public GameComponentToolsForHaul()
        {
        }

        public GameComponentToolsForHaul(Game game)
        {
        }

        public struct Entry
        {
            public Pawn pawn;

            public Thing tool;

            public StatDef stat;

            public float workStat;

            public Entry(Pawn pawn, Thing tool, StatDef stat, float workStat)
            {
                this.pawn = pawn;
                this.tool = tool;
                this.stat = stat;
                this.workStat = workStat;
            }
        }

        private static List<Entry> _cachedToolEntries = new List<Entry>();

        public static Dictionary<Pawn, ThingWithComps> PreviousPawnWeapon= new Dictionary<Pawn, ThingWithComps>();
          
        public static List<Thing> AutoInventory = new List<Thing>();

        public static List<Entry> CachedToolEntries
        {
            get
            {
                return _cachedToolEntries;
            }

            set
            {
                _cachedToolEntries = value;
            }
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            foreach (Map map in Find.Maps)
            {
                List<Thing> mountedVehicles = map.MountedVehicles();
                if (mountedVehicles != null)
                {
                    foreach (Thing vehicle in mountedVehicles)
                    {
                        ((Vehicle_Cart)vehicle).StartSustainerVehicleIfInactive();
                    }
                }
            }
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref PreviousPawnWeapon, "previousPawnWeapons", LookMode.Reference, LookMode.Reference);
            Scribe_Collections.Look(ref AutoInventory, "AutoInventory", LookMode.Reference);
            Scribe_Collections.Look(ref _cachedToolEntries, "_cachedToolEntries", LookMode.Reference);
        }


    }
}
