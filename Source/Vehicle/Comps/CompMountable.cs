﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;
using Verse;
using Verse.AI;
using RimWorld;

namespace ToolsForHaul
{
    public class CompMountable : ThingComp
    {
        private const string txtCommandDismountLabel = "CommandDismountLabel";
        private const string txtCommandDismountDesc = "CommandDismountDesc";
        private const string txtCommandMountLabel = "CommandMountLabel";
        private const string txtCommandMountDesc = "CommandMountDesc";
        private const string txtMountOn = "MountOn";
        private const string txtDismount = "Dismount";

        protected Pawn driver = null;
        private Building_Door lastPassedDoor = null;
        private int tickLastDoorCheck = Find.TickManager.TicksGame;
        private const int TickCooldownDoorCheck = 96;

        public void MountOn(Pawn pawn) 
        { 
            if (driver != null)
                return; 
            driver = pawn;
        }
        public bool IsMounted { get { return (driver != null) ? true : false; } }
        public Pawn Driver { get { return driver; } }
        public void Dismount() 
        { 
            //if (Find.Reservations.IsReserved(parent, driver.Faction))
            Find.Reservations.ReleaseAllForTarget(parent); 
            driver = null; 
        }
        public void DismountAt(IntVec3 dismountPos) 
        {
            Dismount();
            //if (driver.Position.IsAdjacentTo8WayOrInside(dismountPos, driver.Rotation, new IntVec2(1,1)))
            if (dismountPos != IntVec3.Invalid)
                parent.Position = dismountPos;
            else
                Log.Warning("Tried dismount at " + dismountPos);
        }
        public Vector3 InteractionOffset { get { return parent.def.interactionCellOffset.ToVector3().RotatedBy(driver.Rotation.AsAngle); } }

        public Vector3 Position { 
            get {
                Vector3 position = driver.DrawPos - InteractionOffset;
                //No driver
                if (driver == null)
                    return parent.DrawPos;
                //Out of bound or Preventing cart from stucking door
                else if (!GenGrid.InBounds(position))
                    return driver.DrawPos;
                else
                    return (driver.DrawPos - InteractionOffset);
            } }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.LookReference<Pawn>(ref driver, "driver");
            Scribe_References.LookReference<Building_Door>(ref lastPassedDoor, "lastPassedDoor");
        }

        public override void CompTick()
        {
            base.CompTick();
            if (IsMounted)
            {
                if (driver.Dead || driver.Downed || driver.health.InPainShock || driver.InMentalState  //Abnormal
                    || (!driver.RaceProps.Animal && driver.Faction != Faction.OfPlayer)
                    || ForbidUtility.IsForbidden(parent, Faction.OfPlayer))
                    Dismount();
                else
                {
                    if (Find.TickManager.TicksGame - tickLastDoorCheck >= TickCooldownDoorCheck
                    && (driver.Position.GetEdifice() is Building_Door || parent.Position.GetEdifice() is Building_Door))
                    {
                        lastPassedDoor = ((driver.Position.GetEdifice() is Building_Door) ?
                            driver.Position.GetEdifice() : parent.Position.GetEdifice()) as Building_Door;
                        lastPassedDoor.StartManualOpenBy(driver);
                        tickLastDoorCheck = Find.TickManager.TicksGame;
                    }
                    else if (Find.TickManager.TicksGame - tickLastDoorCheck >= TickCooldownDoorCheck && lastPassedDoor != null)
                    {
                        lastPassedDoor.StartManualCloseBy(driver);
                        lastPassedDoor = null;
                    }
                    parent.Position = IntVec3Utility.ToIntVec3(Position);
                    parent.Rotation = driver.Rotation;
                }
            }
        }

        public override IEnumerable<Command> CompGetGizmosExtra()
        {
            foreach (Command compCom in base.CompGetGizmosExtra())
                yield return compCom;

            Command_Action com = new Command_Action();

            if (IsMounted)
            {
                com.defaultLabel = txtCommandDismountLabel.Translate();
                com.defaultDesc = txtCommandDismountDesc.Translate();
                com.icon = ContentFinder<Texture2D>.Get("UI/Commands/IconUnmount");
                com.activateSound = SoundDef.Named("Click");
                com.action = () => { Dismount(); };

                yield return com;
            }
            else
            {
                Designator_Mount designator = new Designator_Mount();

                designator.vehicle = parent;
                designator.defaultLabel = txtCommandMountLabel.Translate();
                designator.defaultDesc = txtCommandMountDesc.Translate();
                designator.icon = ContentFinder<Texture2D>.Get("UI/Commands/IconMount");
                designator.activateSound = SoundDef.Named("Click");

                yield return designator;
            }
        }

        public IEnumerable<FloatMenuOption> CompGetFloatMenuOptionsForExtra(Pawn myPawn)
        {
            // order to drive
            Action action_Order;
            string verb;
            if (!IsMounted)
            {
                action_Order = () =>
                {
                    Find.Reservations.ReleaseAllForTarget(parent);
                    Find.Reservations.Reserve(myPawn, parent);
                    Job jobNew = new Job(DefDatabase<JobDef>.GetNamed("Mount"), parent);
                    myPawn.drafter.TakeOrderedJob(jobNew);
                };
                verb = txtMountOn;
                yield return new FloatMenuOption(verb.Translate(parent.LabelShort), action_Order);
            }
            else if (IsMounted && myPawn == driver)
            {
                action_Order = () =>
                {
                    Dismount();
                };
                verb = txtDismount;
                yield return new FloatMenuOption(verb.Translate(parent.LabelShort), action_Order);
            }
        }

    }
}