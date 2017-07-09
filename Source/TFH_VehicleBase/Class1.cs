﻿using System;
using System.Collections.Generic;

namespace Verse
{
    public class HediffComp_GizmoGiver : HediffComp, IGizmoOwner
    {
        public VerbTracker verbTracker;

        public HediffCompProperties_VerbGiver Props
        {
            get
            {
                return (HediffCompProperties_VerbGiver)this.props;
            }
        }

        public VerbTracker VerbTracker
        {
            get
            {
                return this.verbTracker;
            }
        }

        public List<VerbProperties> VerbProperties
        {
            get
            {
                return this.Props.verbs;
            }
        }

        public HediffComp_GizmoGiver()
        {
            this.verbTracker = new VerbTracker(this);
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Deep.Look<VerbTracker>(ref this.verbTracker, "verbTracker", new object[]
                                                                                   {
                                                                                       this
                                                                                   });
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            this.verbTracker.VerbsTick();
        }
    }
}
