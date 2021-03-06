﻿// Pawnmorpher_DebugDialogue.cs modified by Iron Wolf for Pawnmorph on 08/02/2019 7:29 PM
// last updated 08/02/2019  7:29 PM

using System.Collections.Generic;
using System.Linq;
using System.Text;
using AlienRace;
using HarmonyLib;
using JetBrains.Annotations;
using Pawnmorph.GraphicSys;
using Pawnmorph.Hediffs;
using Pawnmorph.Hybrids;
using Pawnmorph.Utilities;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

#pragma warning disable 1591
namespace Pawnmorph.DebugUtils
{
    public class Pawnmorpher_DebugDialogue : Dialog_DebugOptionLister
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="Pawnmorpher_DebugDialogue" /> class.
        /// </summary>
        public Pawnmorpher_DebugDialogue()
        {
            forcePause = true;
        }

        protected override void DoListingItems()
        {
            if (KeyBindingDefOf.Dev_ToggleDebugActionsMenu.KeyDownEvent)
            {
                Event.current.Use();
                Close();
            }

            if (Current.ProgramState == ProgramState.Playing)
                if (Find.CurrentMap != null)
                    ListPlayOptions();
        }

        private void AddAspectAtStage(AspectDef def, Pawn p, int i)
        {
            p.GetAspectTracker()?.Add(def, i);
        }



        void TryStartRandomHunt(Pawn pawn)
        {
            if (!pawn.RaceProps.predator) return;
            var prey = FormerHumanUtilities.FindRandomPreyFor(pawn);
            if (prey == null) return;
            var job = new Job(JobDefOf.PredatorHunt, prey)
            {
                killIncappedTarget = true
            };

            pawn.jobs?.StartJob(job, JobCondition.InterruptForced);
        }


        void GetMutationInfo(Pawn pawn)
        {
            var tracker = pawn?.GetMutationTracker();
            if (tracker == null)
            {
                Log.Message($"no tracker on {pawn?.Name?.ToStringFull ?? "NULL"}");
                return;
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"---{pawn.Name}---");
            builder.AppendLine("---Raw Influence---"); 
            foreach (KeyValuePair<AnimalClassBase, float> kvp in tracker)
            {
                builder.AppendLine($"{kvp.Key.Label}:{kvp.Value}"); 
            }

            builder.AppendLine($"---Total={tracker.TotalInfluence} N:{tracker.TotalNormalizedInfluence} NN:{tracker.TotalInfluence/MorphUtilities.GetMaxInfluenceOfRace(pawn.def)}---");


            builder.AppendLine($"---HighestInfluence={tracker.HighestInfluence?.Label ?? "NULL"}---");



            Log.Message(builder.ToString()); 

        }

        void GetAllMutationInfo()
        {
            foreach (Pawn pawn in PawnsFinder.AllCaravansAndTravelingTransportPods_Alive)
            {
                GetMutationInfo(pawn); 
            }
        }


        private void ForceTransformation(Pawn pawn)
        {
            var allHediffs = pawn.health.hediffSet.hediffs;
            if (allHediffs == null) return;

            foreach (Hediff hediff in allHediffs)
            {
                var transformer = hediff.def.GetAllTransformers().FirstOrDefault();
                if (transformer != null)
                {
                    transformer.TransformPawn(pawn, hediff);
                    return; 
                }
            }
        }

        void RecalculateInfluence(Pawn pawn)
        {
            pawn?.GetComp<MutationTracker>()?.RecalculateMutationInfluences();
        }

        void AllRecalculateInfluence()
        {
            foreach (Pawn colonist in PawnsFinder.AllMaps_FreeColonists)
            {
                RecalculateInfluence(colonist); 
            }
        }

        void ResetMutationProgression( Pawn pawn)
        {
            var allHediffs = pawn?.health?.hediffSet?.hediffs;
            if (allHediffs == null) return;

            foreach (Hediff_AddedMutation mutation in allHediffs.OfType<Hediff_AddedMutation>())
            {
                mutation.SeverityAdjust?.Restart();
            }
        }

        List<DebugMenuOption> GetGiveBackstoriesOptions(Pawn pawn)
        {
            List<DebugMenuOption> options = new List<DebugMenuOption>(); 
            foreach (BackstoryDef backstoryDef in DefDatabase<BackstoryDef>.AllDefs)
            {
                var def = backstoryDef; 
                options.Add(new DebugMenuOption(def.defName, DebugMenuOptionMode.Action, () => AddBackstoryToPawn(pawn, def)));
            }

            return options; 
        }

        void AddBackstoryToPawn(Pawn pawn, BackstoryDef def)
        {
            pawn.story.adulthood = def.backstory; 
        }

        private List<DebugMenuOption> GetAddAspectOptions(AspectDef def, Pawn p)
        {
            var outLst = new List<DebugMenuOption>();
            for (var i = 0; i < def.stages.Count; i++)
            {
                AspectStage stage = def.stages[i];
                int i1 = i; //need to make a copy 
                var label = string.IsNullOrEmpty(stage.label) ? def.label : stage.label; 
                outLst.Add(new DebugMenuOption($"{i}) {label}", DebugMenuOptionMode.Action,
                                               () => AddAspectAtStage(def, p, i1)));
            }

            return outLst;
        }

        private List<DebugMenuOption> GetAddAspectOptions(Pawn pawn)
        {
            AspectTracker tracker = pawn.GetAspectTracker();
            var outLst = new List<DebugMenuOption>();
            foreach (AspectDef aspectDef in DefDatabase<AspectDef>.AllDefs.Where(d => !tracker.Contains(d))
            ) //don't allow aspects to be added more then once 
            {
                AspectDef tmpDef = aspectDef;

                outLst.Add(new DebugMenuOption($"{aspectDef.defName}", DebugMenuOptionMode.Action,
                                               () =>
                                                   Find.WindowStack
                                                       .Add(new Dialog_DebugOptionListLister(GetAddAspectOptions(tmpDef,
                                                                                                                 pawn)))));
            }

            return outLst;
        }


        private List<DebugMenuOption> GetRaceChangeOptions()
        {
            //var races = RaceGenerator.ImplicitRaces;
            var lst = new List<DebugMenuOption>();
            foreach (MorphDef morph in DefDatabase<MorphDef>.AllDefs)
            {
                MorphDef local = morph;

                lst.Add(new DebugMenuOption(local.label, DebugMenuOptionMode.Tool, () =>
                {
                    Pawn pawn = Find.CurrentMap.thingGrid.ThingsAt(UI.MouseCell()).OfType<Pawn>().FirstOrDefault();
                    if (pawn != null && pawn.RaceProps.intelligence == Intelligence.Humanlike)
                        RaceShiftUtilities.ChangePawnToMorph(pawn, local);
                }));
            }

            return lst;
        }


        private void GetRandomMutationsOptions()
        {
            var options = new List<DebugMenuOption>
                {new DebugMenuOption("none", DebugMenuOptionMode.Tool, () => GivePawnRandomMutations(null))};


            foreach (MorphDef morphDef in MorphDef.AllDefs)
            {
                var option = new DebugMenuOption(morphDef.label, DebugMenuOptionMode.Tool,
                                                 () => GivePawnRandomMutations(morphDef));
                options.Add(option);
            }

            Find.WindowStack.Add(new Dialog_DebugOptionListLister(options));
        }

        private List<DebugMenuOption> GetRemoveAspectOptions(Pawn p)
        {
            var outLst = new List<DebugMenuOption>();


            AspectTracker aspectTracker = p.GetAspectTracker();
            if (aspectTracker == null) return outLst;
            foreach (Aspect aspect in aspectTracker.Aspects.ToList())
            {
                Aspect tmpAspect = aspect;
                outLst.Add(new DebugMenuOption($"{aspect.Label}", DebugMenuOptionMode.Action,
                                               () => aspectTracker.Remove(tmpAspect)));
            }

            return outLst;
        }

        void RunRaceCheck(Pawn pawn)
        {
            if (pawn == null) return;
            if (pawn.IsAnimalOrMerged()) return;
            var oldRace = pawn.def;
            pawn.CheckRace();
            if (pawn.def == oldRace)
            {
                DebugLogUtils.LogMsg(LogLevel.Messages,$"no change in {pawn.Name}");
            }
            else
            {
                DebugLogUtils.LogMsg(LogLevel.Messages, $"{pawn.Name} was {oldRace.defName} and is now {pawn.def.defName}");
            }
        }


        private void GivePawnRandomMutations([CanBeNull] MorphDef morph)
        {
            Pawn pawn = Find.CurrentMap.thingGrid
                            .ThingsAt(UI.MouseCell())
                            .OfType<Pawn>()
                            .FirstOrDefault();
            if (pawn == null) return;


            var mutations = morph?.AllAssociatedMutations;
            if (mutations == null)
                mutations = DefDatabase<MutationDef>.AllDefs;

           

            var mutList = mutations.ToList();
            if (mutList.Count == 0) return;

            int num = Rand.Range(1, Mathf.Min(10, mutList.Count));

            var i = 0;
            List<Hediff_AddedMutation> givenList = new List<Hediff_AddedMutation>();
            List<MutationDef> triedGive = new List<MutationDef>(); 
            while (i < num && mutList.Count > 0)
            {
                var giver = mutList.RandElement();
                mutList.Remove(giver);
                triedGive.Add(giver); 
                var res = MutationUtilities.AddMutation(pawn, giver);
                givenList.AddRange(res); 
                i++;
            }

            if (givenList.Count > 0)
            {
                Log.Message($"gave {pawn.Name} [{givenList.Join(m => m.Label)}] from [{triedGive.Join(m => m.defName)}]");
            }
            else
            {
                Log.Message($"could not give {pawn.Name} any from [{triedGive.Join(m => m.defName)}]");
            }
        }

        private void ListPawnInitialGraphics(Pawn pawn)
        {
            var initialComp = pawn.GetComp<InitialGraphicsComp>();
            if (initialComp == null) return;

            Log.Message(initialComp.GetDebugStr());
        }

        void DoRemoveAspectsOption(Pawn p)
        {
            var options = GetRemoveAspectOptions(p);
            if (options.Count == 0) return;
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(options)); 
        }

        void DoAddAspectToPawn(Pawn p)
        {
            var options = GetAddAspectOptions(p);
            if (options.Count == 0) return;
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(options)); 
        }

        void DoAddBackstoryToPawn(Pawn pawn)
        {
            if (!pawn.IsFormerHuman()) return;

            Find.WindowStack.Add(new Dialog_DebugOptionListLister(GetGiveBackstoriesOptions(pawn))); 

        }

        void RunRaceCheck()
        {
            StringBuilder builder = new StringBuilder();
            foreach (Pawn pawn in PawnsFinder.AllMaps_FreeColonistsAndPrisonersSpawned)
            {
                RunRaceCheck(pawn); 
            }
        }

        private void ListPlayOptions()
        {
            //TODO move these into the regular action menu 
            DebugAction_NewTmp("shift race", () => { Find.WindowStack.Add(new Dialog_DebugOptionListLister(GetRaceChangeOptions())); }, false);
            DebugAction_NewTmp("give random mutations", GetRandomMutationsOptions, false);
            DebugAction_NewTmp("recalculate all colonist mutation influence", AllRecalculateInfluence, false);
            DebugAction_NewTmp("run race check on all pawn", RunRaceCheck, false);
            DebugAction_NewTmp("get mutation info for all pawns", GetAllMutationInfo, false);
            DebugToolMapForPawns_NewTmp("force full transformation", ForceTransformation, false);
            DebugToolMapForPawns_NewTmp("get initial graphics", ListPawnInitialGraphics, false);
            DebugToolMapForPawns_NewTmp("Remove Aspect", DoRemoveAspectsOption, false);
            DebugToolMapForPawns_NewTmp("Add Aspect", DoAddAspectToPawn, false);
            DebugToolMapForPawns_NewTmp("Add Backstory to Sapient Animal", DoAddBackstoryToPawn, false);
            DebugToolMapForPawns_NewTmp("Try Random Hunt", TryStartRandomHunt, false);
            DebugToolMapForPawns_NewTmp("Make pawn permanently feral", MakePawnPermanentlyFeral, false);
            DebugToolMapForPawns_NewTmp("Restart all mutation progression", ResetMutationProgression, false);
            DebugToolMapForPawns_NewTmp("recalculate mutation influence", RecalculateInfluence, false);
            DebugToolMapForPawns_NewTmp("Run Race Check", RunRaceCheck, false);
            DebugToolMapForPawns_NewTmp("get mutation info for pawn", GetMutationInfo, false); 
        }

        private void MakePawnPermanentlyFeral(Pawn obj)
        {
            if (obj?.IsFormerHuman() != true) return;

            FormerHumanUtilities.MakePermanentlyFeral(obj); 
        }
    }
}