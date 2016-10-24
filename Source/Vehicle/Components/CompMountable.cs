﻿using System;
using System.Collections.Generic;
using RimWorld;
using ToolsForHaul.Designators;
using ToolsForHaul.JobDefs;
using ToolsForHaul.StatDefs;
using ToolsForHaul.Utilities;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace ToolsForHaul.Components
{
    public class CompMountable : ThingComp
    {
        private const string txtCommandDismountLabel = "CommandDismountLabel";
        private const string txtCommandDismountDesc = "CommandDismountDesc";
        private const string txtCommandMountLabel = "CommandMountLabel";
        private const string txtCommandMountDesc = "CommandMountDesc";
        private const string txtMountOn = "MountOn";
        private const string txtDismount = "Dismount";

        private Building_Door lastPassedDoor;
        private int tickLastDoorCheck = Find.TickManager.TicksGame;
        private const int TickCooldownDoorCheck = 96;
        private int tickCheck = Find.TickManager.TicksGame;
        private int tickCooldown = 60;

        public Sustainer sustainerAmbient;
        public CompDriver driverComp;

        public int DefaultMaxItem { get { return (int)parent.GetStatValue(HaulStatDefOf.VehicleMaxItem); } }
        public int MaxItemPerBodySize { get { return (int)parent.GetStatValue(HaulStatDefOf.VehicleMaxItem); } }

        private bool IsTank()
        {
            Vehicle_Turret vehicleCart = parent as Vehicle_Turret;
            if (vehicleCart != null)
                return true;
            return false;
        }

        public void MountOn(Pawn pawn)
        {
            if (Driver != null)
                return;

            if (ToolsForHaulUtility.IsDriver(pawn))
                return;

            Driver = pawn;

            MapComponent_ToolsForHaul.currentVehicle.Add(pawn, parent);

            Vehicle_Cart vehicleCart = parent as Vehicle_Cart;
            if (vehicleCart != null)
            {
                // Set faction of vehicle to whoever mounts it
                if (vehicleCart.Faction != Driver.Faction && vehicleCart.ClaimableBy(Driver.Faction))
                {
                    parent.SetFaction(Driver.Faction);
                }

                if (!vehicleCart.vehiclesComp.AnimalsCanDrive())
                {
                    Driver.RaceProps.makesFootprints = false;
                }
                if (vehicleCart.IsCurrentlyMotorized())
                {
                    SoundInfo info = SoundInfo.InWorld(parent);
                    sustainerAmbient = vehicleCart.vehiclesComp.compProps.soundAmbient.TrySpawnSustainer(info);
                }

                if (pawn.RaceProps.Humanlike)
                {
                    driverComp = new CompDriver { vehicleCart = parent as Vehicle_Cart };
                    Driver.AllComps?.Add(driverComp);
                    driverComp.parent = Driver;
                }
                return;
            }

            Vehicle_Turret vehicleTurret = parent as Vehicle_Turret;
            if (vehicleTurret != null)
            {
                // Set faction of vehicle to whoever mounts it
                if (vehicleTurret.Faction != Driver.Faction && vehicleTurret.ClaimableBy(Driver.Faction))
                {
                    parent.SetFaction(Driver.Faction);
                }

                if (!vehicleTurret.vehiclesComp.AnimalsCanDrive())
                {
                    Driver.RaceProps.makesFootprints = false;
                }
                if (vehicleTurret.IsCurrentlyMotorized())
                {
                    SoundInfo info = SoundInfo.InWorld(parent);
                    sustainerAmbient = vehicleTurret.vehiclesComp.compProps.soundAmbient.TrySpawnSustainer(info);
                }

                if (pawn.RaceProps.Humanlike)
                {
                    driverComp = new CompDriver { vehicleTurret = parent as Vehicle_Turret };
                    Driver.AllComps?.Add(driverComp);
                    driverComp.parent = Driver;
                }
                return;
            }


        }

        public bool IsMounted => Driver != null;

        public Pawn Driver;

        public void Dismount()
        {
            if (Driver.RaceProps.Humanlike)
            {
                Driver.AllComps?.Remove(driverComp);
                driverComp.vehicleCart = null;
                driverComp.vehicleTurret = null;
                driverComp.parent = null;
            }

            MapComponent_ToolsForHaul.currentVehicle.Remove(Driver);

            Driver.RaceProps.makesFootprints = true;

            //if (Find.Reservations.IsReserved(parent, Driver.Faction))
            Find.Reservations.ReleaseAllForTarget(parent);
            if (Driver.Faction != Faction.OfPlayer)
            {
                parent.SetForbidden(true);
                parent.SetFaction(null);
            }

            Driver = null;

            if (sustainerAmbient != null)
            {
                sustainerAmbient.End();
            }


            //  Find.ListerBuildings.Add(parent as Building);
        }

        public void DismountAt(IntVec3 dismountPos)
        {
            //if (Driver.Position.IsAdjacentTo8WayOrInside(dismountPos, Driver.Rotation, new IntVec2(1,1)))
            if (dismountPos != IntVec3.Invalid)
            {
                Dismount();
                parent.Position = dismountPos;
                return;
            }
            Log.Warning("Tried dismount at " + dismountPos);
        }

        public Vector3 InteractionOffset
        {
            get { return parent.def.interactionCellOffset.ToVector3().RotatedBy(Driver.Rotation.AsAngle); }
        }

        public Vector3 Position
        {
            get
            {
                Vector3 position;

                position = Driver.DrawPos - InteractionOffset * 1.3f;

                //No Driver
                if (Driver == null)
                    return parent.DrawPos;
                //Out of bound or Preventing cart from stucking door
                if (!position.InBounds())
                    return Driver.DrawPos;

                return position;
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.LookReference(ref Driver, "Driver");
            Scribe_References.LookReference(ref lastPassedDoor, "lastPassedDoor");
        }

        public override void CompTick()
        {
            base.CompTick();
            if (IsMounted)
            {
                if (!Driver.Spawned)
                {
                    parent.DeSpawn();
                    return;
                }

                if (Driver.Dead || Driver.Downed || Driver.health.InPainShock || Driver.MentalStateDef == MentalStateDefOf.WanderPsychotic || (parent.IsForbidden(Faction.OfPlayer) && Driver.Faction == Faction.OfPlayer))
                {
                    if (!Driver.Position.InBounds())
                    {
                        DismountAt(Driver.Position);
                        return;
                    }

                    DismountAt(Driver.Position - parent.def.interactionCellOffset.RotatedBy(Driver.Rotation));
                    Driver.Position = Driver.Position.RandomAdjacentCell8Way();
                    return;
                }



                if (Find.TickManager.TicksGame - tickCheck >= tickCooldown)
                {
                    if (Driver.Faction == Faction.OfPlayer && Driver.CurJob != null)
                    {
                        if (Driver.CurJob.def.playerInterruptible && (Driver.CurJob.def == JobDefOf.DoBill ||
                                                                      Driver.CurJob.def == JobDefOf.EnterCryptosleepCasket ||
                                                                      Driver.CurJob.def == JobDefOf.LayDown ||
                                                                      Driver.CurJob.def == JobDefOf.Lovin ||
                                                                      Driver.CurJob.def == JobDefOf.MarryAdjacentPawn ||
                                                                      Driver.CurJob.def == JobDefOf.Mate ||
                                                                      Driver.CurJob.def == JobDefOf.PrisonerAttemptRecruit ||
                                                                      Driver.CurJob.def == JobDefOf.Research ||
                                                                      Driver.CurJob.def == JobDefOf.SocialRelax ||
                                                                      Driver.CurJob.def == JobDefOf.SpectateCeremony ||
                                                                      Driver.CurJob.def == JobDefOf.StandAndBeSociallyActive ||
                                                                      Driver.CurJob.def == JobDefOf.TakeToBedToOperate ||
                                                                      Driver.CurJob.def == JobDefOf.TendPatient ||
                                                                      Driver.CurJob.def == JobDefOf.UseCommsConsole ||
                                                                      Driver.CurJob.def == JobDefOf.UseNeurotrainer ||
                                                                      Driver.CurJob.def == JobDefOf.VisitSickPawn ||
                                                                      Driver.CurJob.def == JobDefOf.Shear ||

                                                                      Driver.CurJob.def == JobDefOf.FeedPatient ||
                                                                      Driver.CurJob.def == JobDefOf.PrisonerExecution ||
                                                                      Driver.CurJob.def == JobDefOf.ManTurret ||
                                                                      Driver.CurJob.def == JobDefOf.Train ||
                                                                      Driver.health.NeedsMedicalRest ||
                                                                      Driver.health.PrefersMedicalRest

                            ) && Driver.Position.Roofed())
                        {
                            parent.Position = Position.ToIntVec3();
                            parent.Rotation = Driver.Rotation;
                            if (!Driver.Position.InBounds())
                            {
                                DismountAt(Driver.Position);
                                return;
                            }
                            DismountAt(Driver.Position - InteractionOffset.ToIntVec3());
                            Driver.Position = Driver.Position.RandomAdjacentCell8Way();
                            return;
                        }
                    }
                    tickCheck = Find.TickManager.TicksGame;
                    tickCooldown = Rand.RangeInclusive(60, 180);
                }
                if (Find.TickManager.TicksGame - tickLastDoorCheck >= 96 && (Driver.Position.GetEdifice() is Building_Door || parent.Position.GetEdifice() is Building_Door))
                {
                    lastPassedDoor = (Driver.Position.GetEdifice() is Building_Door ? Driver.Position.GetEdifice() : parent.Position.GetEdifice()) as Building_Door;
                    lastPassedDoor.StartManualOpenBy(Driver);
                    tickLastDoorCheck = Find.TickManager.TicksGame;
                }
                else if (Find.TickManager.TicksGame - tickLastDoorCheck >= 96 && lastPassedDoor != null)
                {
                    lastPassedDoor.StartManualCloseBy(Driver);
                    lastPassedDoor = null;
                }




            }
        }


        private bool targetIsMoving
        {
            get
            {
                return Driver != null && Driver.pather != null && Driver.pather.Moving;
            }
        }

        private bool targetIsNearDestination
        {
            get
            {
                Vehicle_Cart cart = parent as Vehicle_Cart;
                if (Driver.Position.AdjacentTo8WayOrInside(Driver.pather.Destination)) return true;
                return false;
            }
        }

        public override IEnumerable<Command> CompGetGizmosExtra()
        {
            foreach (Command compCom in base.CompGetGizmosExtra())
                yield return compCom;

            if (parent.Faction != Faction.OfPlayer)
                yield break;

            Command_Action com = new Command_Action();

            if (IsMounted)
            {
                com.defaultLabel = txtCommandDismountLabel.Translate();
                com.defaultDesc = txtCommandDismountDesc.Translate();
                com.icon = ContentFinder<Texture2D>.Get("UI/Commands/IconUnmount");
                com.activateSound = SoundDef.Named("Click");
                com.action = Dismount;

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
            if (parent.Faction == Faction.OfPlayer)
            {
                if (!IsMounted && !parent.IsForbidden(Faction.OfPlayer))
                {
                    action_Order = () =>
                    {
                        Find.Reservations.ReleaseAllForTarget(parent);
                        Find.Reservations.Reserve(myPawn, parent);
                        Job jobNew = new Job(HaulJobDefOf.Mount, parent);
                        myPawn.drafter.TakeOrderedJob(jobNew);
                    };
                    verb = txtMountOn;
                    yield return new FloatMenuOption(verb.Translate(parent.LabelShort), action_Order);
                }
                else if (IsMounted && myPawn == Driver)// && !myPawn.health.hediffSet.HasHediff(HediffDef.Named("HediffWheelChair")))
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
}