﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using RimWorld;

using UnityEngine;

using Verse;
using Verse.AI;
using Verse.Sound;

namespace Vehicle
{
    class Designator_Board : Designator
    {
        private const string txtCannotBoard = "CannotBoard";

        public Thing vehicle;
        public IntVec3 mountPos;

        public Designator_Board()
            : base()
        {
            useMouseIcon = true;
            this.soundSucceeded = SoundDefOf.Click;
        }

        public override int DraggableDimensions { get { return 1; } }

        public override AcceptanceReport CanDesignateCell(IntVec3 loc)
        {
            List<Thing> thingList = loc.GetThingList();

            foreach (Thing thing in thingList)
            {
                Pawn pawn = thing as Pawn;
                if (pawn != null && (pawn.Faction == Faction.OfPlayer || (pawn.RaceProps.Animal && pawn.drafter != null)))
                    return true;
            }

            return new AcceptanceReport(txtCannotBoard.Translate());
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            List<Thing> thingList = c.GetThingList();
            foreach (Thing thing in thingList)
            {
                Pawn pawn = thing as Pawn;
                if (pawn != null && (pawn.Faction == Faction.OfPlayer || (pawn.RaceProps.Animal && pawn.drafter != null)))
                {
                    Pawn driver = pawn;
                    Job job = new Job(DefDatabase<JobDef>.GetNamed("Board"));
                    Find.Reservations.ReleaseAllForTarget(vehicle);
                    job.targetA = vehicle;
                    job.targetB = mountPos;
                    driver.jobs.StartJob(job, JobCondition.InterruptForced);
                    break;
                }
            }

            Find.DesignatorManager.Deselect();
        }
    }
}