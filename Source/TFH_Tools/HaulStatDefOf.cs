﻿namespace TFH_Tools
{
    using RimWorld;

    using Verse;

    [DefOf]
    public static class HaulStatDefOf
    {
        public static readonly StatDef VehicleSpeed = DefDatabase<StatDef>.GetNamed("VehicleSpeed");
        public static readonly StatDef VehicleMaxItem = DefDatabase<StatDef>.GetNamed("VehicleMaxItem");
        public static readonly StatDef InventoryMaxItem = DefDatabase<StatDef>.GetNamed("InventoryMaxItem");        
    }
}
