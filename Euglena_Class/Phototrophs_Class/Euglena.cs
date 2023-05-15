
using RimWorld;
using UnityEngine;
using Verse;
using System.Collections.Generic;
using Verse.AI;
using System.Linq;
using System;
using System.Text;
using System.Threading;
using RimWorld.SketchGen;
using RimWorld.QuestGen;
using RimWorld.Planet;
using RimWorld.Planet;
using RimWorld.BaseGen;
using Verse.Sound;

namespace Euglena
{
    //test cm
    public class StockGenerator_SingleDefRand : StockGenerator
    {
        private ThingDef thingDef;
        private float spawnChance = 0.5f;

        public override IEnumerable<Thing> GenerateThings(int forTile, Faction faction = null)
        {
            if (Rand.Chance(spawnChance))
            {
                foreach (Thing item in StockGeneratorUtility.TryMakeForStock(thingDef, RandomCountOf(thingDef), faction))
                {
                    yield return item;
                }
            }
        }

        public override bool HandlesThingDef(ThingDef thingDef)
        {
            return thingDef == this.thingDef;
        }

        public override IEnumerable<string> ConfigErrors(TraderKindDef parentDef)
        {
            foreach (string item in base.ConfigErrors(parentDef))
            {
                yield return item;
            }
            if (!thingDef.tradeability.TraderCanSell())
            {
                yield return string.Concat(thingDef, " tradeability doesn't allow traders to sell this thing");
            }
        }
    }

    //Параметры через XML
    public class Eug_DefModExtensionInfo : DefModExtension
    {
        public List<HediffDef> Hediffs;

        public XenotypeDef XenotypeDef;
        public DevelopmentalStage DevelopmentalLifeStage;
        public PawnKindDef PawnKindDef;
    }

    //Имплантатор
    public abstract class Eug_Verb_CastBaseTouch : Verb
    {
        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            if (!base.ValidateTarget(target))
            {
                return false;
            }
            if (!ReloadableUtility.CanUseConsideringQueuedJobs(CasterPawn, base.EquipmentSource))
            {
                return false;
            }
            return true;
        }

        public override void OrderForceTarget(LocalTargetInfo target)
        {
            Job gotoJob = JobMaker.MakeJob(JobDefOf.Goto, target.Cell);
            CasterPawn.jobs.TryTakeOrderedJob(gotoJob, JobTag.Misc);

            Job job = JobMaker.MakeJob(JobDefOf.UseVerbOnThingStatic, target);
            job.verbToUse = this;
            job.locomotionUrgency = LocomotionUrgency.Walk;
            CasterPawn.jobs.jobQueue.EnqueueLast(job, JobTag.Misc);
        }

        public override void OnGUI(LocalTargetInfo target)
        {
            if (CanHitTarget(target) && verbProps.targetParams.CanTarget(target.ToTargetInfo(caster.Map)))
            {
                base.OnGUI(target);
            }
            else
            {
                GenUI.DrawMouseAttachment(TexCommand.CannotShoot);
            }
        }

        public override void DrawHighlight(LocalTargetInfo target)
        {
            if (target.IsValid && CanHitTarget(target))
            {
                GenDraw.DrawTargetHighlightWithLayer(target.CenterVector3, AltitudeLayer.MetaOverlays);
                DrawHighlightFieldRadiusAroundTarget(target);
            }
        }

    }

    public class Eug_VerbProperties_AddGenes : VerbProperties
    {
        public List<GeneDef> genesToRemove = new List<GeneDef>();
        public List<GeneDef> genesToAdd = new List<GeneDef>();

        public Eug_VerbProperties_AddGenes()
        {
            this.verbClass = typeof(Eug_Verb_AddGenes);
        }
    }
    public class Eug_Verb_AddGenes : Eug_Verb_CastBaseTouch
    {
        private Eug_VerbProperties_AddGenes Props => (Eug_VerbProperties_AddGenes)this.verbProps;

        public override bool MultiSelect => true;

        protected override bool TryCastShot()
        {
            if (!currentTarget.Pawn.Dead)
            {
                foreach (GeneDef geneDef in Props.genesToRemove)
                {
                    Gene geneToRemove = currentTarget.Pawn.genes.GenesListForReading.FirstOrDefault(g => g.def == geneDef);
                    if (geneToRemove != null)
                    {
                        currentTarget.Pawn.genes.RemoveGene(geneToRemove);
                    }
                }

                foreach (GeneDef geneDef in Props.genesToAdd)
                {
                    currentTarget.Pawn.genes.AddGene(geneDef, false);
                }
            }

            base.ReloadableCompSource.UsedOnce();
            return true;
        }
    }

    public class Eug_Gene_AddHeddifs : Gene
    {
        public List<Hediff> LinkedHediffs
        {
            get
            {
                List<Hediff> list = new List<Hediff>();
                List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
                foreach (HediffDef linkedHediffDef in def.GetModExtension<Eug_DefModExtensionInfo>().Hediffs)
                {
                    list.AddRange(hediffs.Where(h => h.def == linkedHediffDef));
                }
                return list;
            }
        }

        public override void PostAdd()
        {
            base.PostAdd();

            if (!pawn.Dead && pawn.Spawned)
            {
                foreach (HediffDef hediffDef in def.GetModExtension<Eug_DefModExtensionInfo>().Hediffs)
                {
                    pawn.health.AddHediff(hediffDef);
                }
            }
        }

        public override void PostRemove()
        {
            base.PostRemove();

            foreach (Hediff item in LinkedHediffs)
            {
                pawn.health.RemoveHediff(item);
            }
        }
    }

    public class Eug_HediffCompProperties_Spawner : HediffCompProperties
    {
        public IntRange harvestTicksRange;
        public IntRange stackRange;
        public List<ThingDef> harvestThingDefs;
        public bool writeTimeLeftToSpawn = true;

        public Eug_HediffCompProperties_Spawner()
        {
            this.compClass = typeof(Eug_HediffComp_Spawner);
        }
    }
    public class Eug_HediffComp_Spawner : HediffComp
    {
        private int growthTicks;
        private int harvestTicks;
        private Eug_HediffCompProperties_Spawner Props => (Eug_HediffCompProperties_Spawner)this.props;

        public override void CompPostMake()
        {
            base.CompPostMake();
            harvestTicks = Props.harvestTicksRange.RandomInRange;
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            growthTicks++;

            if (growthTicks >= harvestTicks)
            {
                var harvestThingDef = Props.harvestThingDefs.RandomElement();

                var harvest = ThingMaker.MakeThing(harvestThingDef);
                harvest.stackCount = Props.stackRange.RandomInRange;
                GenPlace.TryPlaceThing(harvest, this.parent.pawn.Position, this.parent.pawn.Map, ThingPlaceMode.Near);

                this.parent.pawn.needs.food.CurLevel -= 0.3f;

                growthTicks = 0;

                harvestTicks = Props.harvestTicksRange.RandomInRange;
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref growthTicks, "growthTicks", 0);
            Scribe_Values.Look(ref harvestTicks, "harvestTicks", 0);
        }

        public override string CompDebugString()
        {
            if (Props.writeTimeLeftToSpawn)
            {
                return "NextHarvestIn".Translate() + ": " + (harvestTicks - growthTicks).ToStringTicksToPeriod().Colorize(ColoredText.DateTimeColor);
            }
            return null;
        }
    }

    //Превращение в дерево
    public class Eug_CompProperties_PlantSpawn : CompProperties
    {
        public List<ThingDef> plantDefs;
        public IntRange ticksToSpawnRange;
        public float minFertility = 0;
        public float spawnRadius = 3;
        public float plantGrowth = 0.01f;

        public Eug_CompProperties_PlantSpawn()
        {
            compClass = typeof(Eug_CompPlantSpawn);
        }
    }
    public class Eug_CompPlantSpawn : ThingComp
    {
        private int spawnCounter = 0;
        private int ticksToSpawn;
        private Eug_CompProperties_PlantSpawn Props => (Eug_CompProperties_PlantSpawn)props;

        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);
            ticksToSpawn = Props.ticksToSpawnRange.RandomInRange;
        }

        public override void CompTick()
        {
            base.CompTick();
            if (spawnCounter >= ticksToSpawn)
            {
                IntVec3 spawnCell;
                if (CellFinderLoose.TryGetRandomCellWith((IntVec3 c) => c.GetTerrain(this.parent.Map).fertility >= Props.minFertility &&
                    (c - this.parent.Position).LengthHorizontal <= Props.spawnRadius && c.GetPlant(this.parent.Map) == null, this.parent.Map, 1000, out spawnCell))
                {
                    spawnCounter = 0;
                    ThingDef plantDef = Props.plantDefs.RandomElement();
                    if (plantDef != null)
                    {
                        Plant newPlant = (Plant)ThingMaker.MakeThing(plantDef);
                        newPlant.Growth = Props.plantGrowth;
                        GenSpawn.Spawn(newPlant, spawnCell, this.parent.Map);
                        ticksToSpawn = Props.ticksToSpawnRange.RandomInRange;
                    }
                }
            }
            else
            {
                spawnCounter++;
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref spawnCounter, "spawnCounter");
            Scribe_Values.Look(ref ticksToSpawn, "ticksToSpawn");
        }
    }

    //Великое древо
    public class Eug_GetPawnThing : MoteThrown
    {
        public override void Tick()
        {
            if (base.Map == null)
            {
                Destroy();
            }
            PawnGenerationRequest request = new PawnGenerationRequest((def.GetModExtension<Eug_DefModExtensionInfo>().PawnKindDef), Faction.OfPlayer, PawnGenerationContext.NonPlayer, -1, forceGenerateNewPawn: false, allowDead: false, allowDowned: true, canGeneratePawnRelations: true, mustBeCapableOfViolence: false, 1f, forceAddFreeWarmLayerIfNeeded: false, allowGay: true, allowPregnant: false, allowFood: true, allowAddictions: true, inhabitant: false, certainlyBeenInCryptosleep: false, forceRedressWorldPawnIfFormerColonist: false, worldPawnFactionDoesntMatter: false, 0f, 0f, null, 1f, null, null, null, null, null, null, null, null, null, null, null, null, forceNoIdeo: false, forceNoBackstory: false, forbidAnyTitle: false, forceDead: false, null, null, null, null, null, 0f, (def.GetModExtension<Eug_DefModExtensionInfo>().DevelopmentalLifeStage));
            Pawn pawn = PawnGenerator.GeneratePawn(request);
            pawn.genes.SetXenotype(def.GetModExtension<Eug_DefModExtensionInfo>().XenotypeDef);
            GenSpawn.Spawn(pawn, base.Position, base.Map);
            Destroy();
        }
    }

    public class Eug_CompProperties_SpawnOnCorpse : CompProperties
    {
        public float radius;
        public ThingDef spawnThingOnCorpse;
        public int ticksToAbsorption;
        public float chanceToAbsorb; // Шанс на поглощение

        public Eug_CompProperties_SpawnOnCorpse()
        {
            compClass = typeof(Eug_CompSpawnOnCorpse);
        }
    }
    public class Eug_CompSpawnOnCorpse : ThingComp
    {
        private const int SearchTickInterval = 5000; // Интервал поиска трупов
        private int tickCounter = 0;
        private int searchTickCounter = 0; // Счетчик тиков для поиска трупов
        private bool corpseFound = false;

        private Eug_CompProperties_SpawnOnCorpse Props => (Eug_CompProperties_SpawnOnCorpse)props;

        public override void CompTick()
        {
            base.CompTick();

            if (!corpseFound && searchTickCounter++ >= SearchTickInterval)
            {
                searchTickCounter = 0; // сбросить счетчик тиков поиска
                foreach (Thing thing in GenRadial.RadialDistinctThingsAround(parent.Position, parent.Map, Props.radius, true))
                {
                    if (thing is Corpse corpse && corpse.InnerPawn.RaceProps.Humanlike)
                    {
                        corpseFound = true;
                        break;
                    }
                }
            }

            if (corpseFound)
            {
                tickCounter++;

                if (tickCounter >= Props.ticksToAbsorption)
                {
                    IntVec3 position = parent.Position;

                    foreach (Thing thing in GenRadial.RadialDistinctThingsAround(position, parent.Map, Props.radius, true))
                    {
                        if (thing is Corpse corpse && corpse.InnerPawn.RaceProps.Humanlike)
                        {
                            if (corpse.GetRotStage() == RotStage.Rotting || corpse.InnerPawn.ageTracker.CurLifeStageIndex < 2)
                            {
                                continue;
                            }

                            if (Rand.Value > Props.chanceToAbsorb) // Используем шанс на поглощение
                            {
                                continue;
                            }

                            IntVec3 corpsePosition = corpse.Position;

                            corpse.Destroy();

                            ThingDef thingDef = Props.spawnThingOnCorpse;
                            Thing item = ThingMaker.MakeThing(thingDef);
                            GenPlace.TryPlaceThing(item, corpsePosition, parent.Map, ThingPlaceMode.Near);

                            tickCounter = 0;
                            break;
                        }
                    }
                }
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref tickCounter, "tickCounter");
            Scribe_Values.Look(ref corpseFound, "corpseFound");
            Scribe_Values.Look(ref searchTickCounter, "searchTickCounter"); // Добавлено сохранение счетчика поиска трупов
        }
    }

    //Органические импланты
    public class CompProperties_UseEffect_IncidentStart : CompProperties_UseEffect
    {
        public string incidentDefName;

        public CompProperties_UseEffect_IncidentStart()
        {
            compClass = typeof(CompUseEffect_IncidentStart);
        }
    }
    public class CompUseEffect_IncidentStart : CompUseEffect
    {
        public override void DoEffect(Pawn user)
        {
            base.DoEffect(user);
            IncidentDef incidentDef = DefDatabase<IncidentDef>.GetNamed(((CompProperties_UseEffect_IncidentStart)props).incidentDefName);
            IncidentParms parms = new IncidentParms
            {
                target = parent.Map,
                spawnCenter = parent.PositionHeld
            };
            incidentDef.Worker.TryExecute(parms);
        }
    }

    public class CompProperties_SpawnOnDestroy : CompProperties
    {
        public ThingDef spawnThingDef;
        public float spawnChance = 1f;

        public CompProperties_SpawnOnDestroy()
        {
            compClass = typeof(CompSpawnOnDestroy);
        }
    }
    public class CompSpawnOnDestroy : ThingComp
    {
        public CompProperties_SpawnOnDestroy Props => (CompProperties_SpawnOnDestroy)props;

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            if (mode == DestroyMode.KillFinalize && Props.spawnThingDef != null && Rand.Chance(Props.spawnChance))
            {
                GenSpawn.Spawn(Props.spawnThingDef, parent.Position, previousMap);
            }

            base.PostDestroy(mode, previousMap);
        }
    }

    public class Verb_Spawn : Verb_CastBase
    {
        protected override bool TryCastShot()
        {
            if (currentTarget.HasThing && currentTarget.Thing.Map != caster.Map)
            {
                return false;
            }
            Thing SpawnThing = GenSpawn.Spawn(verbProps.spawnDef, currentTarget.Cell, caster.Map);

            if (SpawnThing.Faction == null && SpawnThing.def.CanHaveFaction)
            {
                SpawnThing.SetFaction(caster.Faction);
            }

            return true;
        }
    }

    public class CompProperties_AbilityReleaseGas : CompProperties_AbilityEffect
    {
        public float durationSeconds;
        public float cellsToFill = 1f;
        public GasType gasType;

        public CompProperties_AbilityReleaseGas()
        {
            this.compClass = typeof(CompAbilityEffect_ReleaseGas);
        }
    }
    public class CompAbilityEffect_ReleaseGas : CompAbilityEffect
    {
        private int remainingGas;
        private bool started;
        private const int ReleaseGasInterval = 30;

        private CompProperties_AbilityReleaseGas Props => (CompProperties_AbilityReleaseGas)props;

        private int TotalGas => Mathf.CeilToInt(Props.cellsToFill * 255f);

        private float GasReleasedPerTick => (float)TotalGas / Props.durationSeconds / 60f;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            if (target.Cell.InBounds(parent.pawn.Map))
            {
                var gasType = Props.gasType;
                var position = target.Cell;
                var map = parent.pawn.Map;
                remainingGas = TotalGas;
                started = true;
            }
        }

        public override void CompTick()
        {
            if (!started || parent.pawn.MapHeld == null)
            {
                return;
            }

            if (remainingGas > 0 && parent.pawn.IsHashIntervalTick(30))
            {
                int num = Mathf.Min(remainingGas, Mathf.RoundToInt(GasReleasedPerTick * 30f));
                GasUtility.AddGas(parent.pawn.Position, parent.pawn.Map, Props.gasType, num);
                remainingGas -= num;
                if (remainingGas <= 0)
                {
                    started = false;
                    remainingGas = TotalGas;
                }
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref remainingGas, "remainingGas", 0);
            Scribe_Values.Look(ref started, "started", defaultValue: false);
        }
    }
}