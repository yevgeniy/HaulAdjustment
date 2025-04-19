using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.Sound;
using static Unity.Burst.Intrinsics.X86.Avx;

namespace PickUpAndHaul;
[StaticConstructorOnStartup]
static class HarmonyPatches
{
    public static Type VehiclePawnType = null;
    static HarmonyPatches()
    {
        var assmeblies = AppDomain.CurrentDomain.GetAssemblies();
        VehiclePawnType = assmeblies.SelectMany(v => v.GetTypes()).FirstOrDefault(v => v.Name == "VehiclePawn");
        if (VehiclePawnType != null)
        {

        }


        var harmony = new Harmony("mehni.rimworld.pickupandhaul.main");
#if DEBUG
        Harmony.DEBUG = true;
#endif

        if (!ModCompatibilityCheck.CombatExtendedIsActive)
        {
            harmony.Patch(original: AccessTools.Method(typeof(PawnUtility), nameof(PawnUtility.GetMaxAllowedToPickUp), new[] { typeof(Pawn), typeof(ThingDef) }),
                prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(MaxAllowedToPickUpPrefix)));

            harmony.Patch(original: AccessTools.Method(typeof(PawnUtility), nameof(PawnUtility.CanPickUp)),
                prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(CanBeMadeToDropStuff)));
        }

        harmony.Patch(original: AccessTools.Method(typeof(JobGiver_DropUnusedInventory), nameof(JobGiver_DropUnusedInventory.TryGiveJob)),
            postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(DropUnusedInventory_PostFix)));

        harmony.Patch(original: AccessTools.Method(typeof(JobDriver_HaulToCell), nameof(JobDriver_HaulToCell.MakeNewToils)),
            postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(JobDriver_HaulToCell_PostFix)));

        harmony.Patch(original: AccessTools.Method(typeof(Pawn_InventoryTracker), nameof(Pawn_InventoryTracker.Notify_ItemRemoved)),
            postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Pawn_InventoryTracker_PostFix)));

        harmony.Patch(original: AccessTools.Method(typeof(JobGiver_DropUnusedInventory), nameof(JobGiver_DropUnusedInventory.Drop)),
            prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Drop_Prefix)));

        harmony.Patch(original: AccessTools.Method(typeof(JobGiver_Idle), nameof(JobGiver_Idle.TryGiveJob)),
            postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(IdleJoy_Postfix)));


        harmony.Patch(original: AccessTools.Method(typeof(WorkGiver_Haul), nameof(WorkGiver_Haul.ShouldSkip)),
            prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(SkipCorpses_Prefix)));

        harmony.Patch(AccessTools.Method(typeof(JobGiver_Haul), nameof(JobGiver_Haul.TryGiveJob)),
            transpiler: new(typeof(HarmonyPatches), nameof(JobGiver_Haul_TryGiveJob_Transpiler)));

        harmony.Patch(AccessTools.Method(typeof(StoreUtility), nameof(StoreUtility.TryFindBestBetterNonSlotGroupStorageFor)),
            prefix: new(typeof(HarmonyPatches), nameof(TryFindBestBetterNonSlotGroupStorageFor)));

        harmony.Patch(AccessTools.Method(typeof(ThingOwnerUtility), nameof(ThingOwnerUtility.TryGetInnerInteractableThingOwner)),
            prefix: new(typeof(HarmonyPatches), nameof(TryGetInnerInteractableThingOwner)));

        harmony.Patch(AccessTools.Method(typeof(ThingOwner), nameof(ThingOwner.NotifyRemoved)),
            postfix: new(typeof(HarmonyPatches), nameof(NotifyRemovedItem)));

        harmony.Patch(AccessTools.Method(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob)),
            prefix: new(typeof(HarmonyPatches), nameof(StartJob)));

        harmony.Patch(original: AccessTools.Method(typeof(Targeter), nameof(InMapTargeter.TargeterOnGUI)),
            postfix: new HarmonyMethod(typeof(HarmonyPatches),
            nameof(DrawTargeters)));
        harmony.Patch(original: AccessTools.Method(typeof(Targeter), nameof(InMapTargeter.ProcessInputEvents)),
            postfix: new HarmonyMethod(typeof(HarmonyPatches),
            nameof(ProcessTargeterInputEvents)));
        harmony.Patch(original: AccessTools.Method(typeof(Targeter), nameof(InMapTargeter.TargeterUpdate)),
            postfix: new HarmonyMethod(typeof(HarmonyPatches),
            nameof(TargeterUpdate)));
        harmony.Patch(original: AccessTools.Method(typeof(Targeter), nameof(InMapTargeter.StopTargeting)),
            postfix: new HarmonyMethod(typeof(HarmonyPatches),
            nameof(TargeterStop)));


        var type = assmeblies.SelectMany(assembly => assembly.GetTypes())
            .FirstOrDefault(v => v.Name == "VehiclePawn");
        var method_GetGizmos_on_vehicle = type.GetMethod("GetGizmos", BindingFlags.Public | BindingFlags.Instance);
        harmony.Patch(method_GetGizmos_on_vehicle,
            postfix: new(typeof(HarmonyPatches), nameof(GetVehiclGizmos)));


        var methInfo = typeof(Pawn_CarryTracker).GetMethod(
                "TryDropCarriedThing"
                , new Type[] { typeof(IntVec3), typeof(ThingPlaceMode), typeof(Thing).MakeByRefType(), typeof(Action<Thing, int>)
            }
        );
        harmony.Patch(methInfo, postfix: new(typeof(HarmonyPatches), nameof(TryDropCarriedThing)));

        if (ModCompatibilityCheck.VehicleIsActive)
        {
            methInfo = assmeblies.SelectMany(v => v.GetTypes()).FirstOrDefault(v => v.Name == "WorkGiver_CarryToVehicle")
                .GetMethod("JobOnThing", BindingFlags.Instance | BindingFlags.Public);
            harmony.Patch(methInfo, postfix: new(typeof(HarmonyPatches), nameof(JobOnThing_override_vehicle_pack_job)));
        }



        harmony.PatchAll();

        Verse.Log.Message("PickUpAndHaul v1.1.2¼ welcomes you to RimWorld with pointless logspam.");
    }
    private static void SetHaulLocation(CompHauledToInventory comp)
    {
        Messages.Message("SET FROM LOCATION", MessageTypeDefOf.NeutralEvent);
        SoundDefOf.Click.PlayOneShotOnCamera();
        var targettingFrom = TargetingParameters.ForCell();
        targettingFrom.canTargetLocations = true;
        targettingFrom.validator = (TargetInfo c) => true;

        InMapTargeter.BeginTargeting(
            targettingFrom,
            delegate (LocalTargetInfo target)
            {
                var v = new VehiclePawnProxy(comp.parent as Pawn);
                if (!v.FitsOnCell(target.Cell))
                {
                    Messages.Message("VEHICLE CANT FIT IN TIGHT SPACE", MessageTypeDefOf.NeutralEvent);
                    return;
                }

                if (!Utils.TryFindValidToLocation(comp.parent as Pawn, out var toCell, out var errorMessage))
                {
                    Messages.Message(errorMessage, MessageTypeDefOf.NeutralEvent);
                    return;
                }

                comp.ActivateLocalHaul(target.Cell, toCell);
                
            },
            comp.parent
        );
    }
    public static void GetVehiclGizmos(ref IEnumerable<Gizmo> __result, Pawn __instance)
    {
        var gizs = __result.ToList();

        var comp = __instance.GetHaulInventoryComp();
        if (comp != null && __instance.Faction == Faction.OfPlayerSilentFail)
        {
            if (comp.VehicleShouldBeUsedToHaul)
            {
                Command_Action cancleHaul = new Command_Action
                {
                    defaultLabel = "Stop Local Hauling",
                    icon = ContentFinder<Texture2D>.Get("ADJ_HAUL", true),
                    action = delegate ()
                    {
                        comp.VehicleShouldBeUsedToHaul = false;
                    }
                };
                gizs.Add(cancleHaul);
            }
            else
            {

                var vehicleProxy = new VehiclePawnProxy(__instance);
                Command_Action activateHaulingVehicle = new Command_Action
                {
                    defaultLabel = "Start Local hauling",
                    icon = ContentFinder<Texture2D>.Get("ADJ_HAUL", true),
                    action = delegate ()
                    {
                        SetHaulLocation(comp);                        
                    }
                };
                gizs.Add(activateHaulingVehicle);
            }




            __result = gizs;
        }

    }

    private static void DrawTargeters()
    {

        Targeters.OnGUITargeters();
    }
    private static void ProcessTargeterInputEvents()
    {

        Targeters.ProcessTargeterInputEvents();
    }
    private static void TargeterUpdate()
    {

        Targeters.UpdateTargeters();
    }
    private static void TargeterStop()
    {
        Targeters.StopAllTargeters();
    }

    public static bool StartJob(ref Pawn_JobTracker __instance, Job newJob, ref JobCondition lastJobEndCondition, ThinkNode jobGiver, bool resumeCurJobAfterwards, bool cancelBusyStances, ThinkTreeDef thinkTree, JobTag? tag, bool fromQueue, bool canReturnCurJobToPool, bool? keepCarryingThingOverride, bool continueSleeping, bool addToJobsThisTick, bool preToilReservationsCanFail)
    {
        if (__instance.curJob != null && lastJobEndCondition == JobCondition.None)
        {
            lastJobEndCondition = JobCondition.InterruptForced;
        }

        return true;
    }

    public static void NotifyRemovedItem(ref ThingOwner __instance, Thing item)
    {

        if (__instance.owner is Pawn_InventoryTracker)
        {
            if (!item.TryGetComp<CompHauledToInventory>(out var comp))
            {
                Log.Message($"NO COMP ON {item}");
                return;
            }
            Log.Message($"notify removed {item}");
            comp.Hauling = false;

        }

    }

    public static void JobOnThing_override_vehicle_pack_job(ref Job __result, Pawn pawn, Thing t, bool forced)
    {
        if (__result != null)
        {

            WorkGiver_HaulToInventory haulMoreWork = DefDatabase<WorkGiverDef>.AllDefsListForReading.First(wg => wg.Worker is WorkGiver_HaulToInventory).Worker as WorkGiver_HaulToInventory;

            var thingBeingHauled = __result.targetA.Thing;
            if (__result.targetA == null || __result.targetA.Thing == null)
                return;

            if (!__result.targetA.Thing.TryGetComp<CompHauledToInventory>(out var c))
            {
                return;
            }
            if (MassUtility.WillBeOverEncumberedAfterPickingUp(pawn, t, t.stackCount))
            {
                return;
            }

            if (!haulMoreWork.HasJobOnThing(pawn, __result.targetA.Thing))
                return;

            Log.Message("CHECKING IF BETTER JOB EXISTS for ? " + thingBeingHauled);
            var job = haulMoreWork.JobOnThing(pawn, thingBeingHauled, forced);

            Log.Message("RET JOB: " + job);

            if (job != null && job.def == PickUpAndHaulJobDefOf.HaulToInventory)
            {

                __result = job;
            }

        }

    }


    public static void TryDropCarriedThing(IntVec3 dropLoc, ThingPlaceMode mode, Thing resultingThing, Pawn_CarryTracker __instance, ref bool __result)
    {
        if (!__result)
            return;
        var thing = resultingThing;

        if (ModCompatibilityCheck.CombatExtendedIsActive)
        {
            if (thing != null && thing is ThingWithComps thingWithComps)
            {

                var gun = new GunProxy(thingWithComps);
                var comp = gun.CompAmmoUser;

                //if (comp != null)
                //{
                //    if (!CritDestinationsMap.Guns.Contains(thingWithComps))
                //    {
                //        var slogGroup = thing.Map.haulDestinationManager.SlotGroupParentAt(thing.Position);


                //        if (slogGroup != null && slogGroup.GetStoreSettings().Priority != StoragePriority.Unstored)
                //        {

                //            CritDestinationsMap.Guns.Add(thingWithComps);
                //        }

                //    }

                //}

            }
        }


    }

    private static bool TryGetInnerInteractableThingOwner(ref ThingOwner __result, Thing thing)
    {
        if (thing is CriticalThingHaulDestination)
        {
            thing = (thing as CriticalThingHaulDestination).Thing;
        }

        if (thing is Blueprint)
        {

            Log.Message($"BLUEPRINT: {thing}");
            /* only for blueprints because I think frames have a 'thing owner' */
            __result = new HaulThingOwner(thing, __result);
            return false;
        }

        if (ModCompatibilityCheck.CombatExtendedIsActive && thing is ThingWithComps thingWithComps)
        {

            var gun = new GunProxy(thingWithComps);
            var comp = gun.CompAmmoUser;

            if (comp != null)
            {
                /*is a gun*/
                Log.Message($"GUN: {thing}");
                __result = new GunThingOwner(thing, __result);
                return false;
            }
        }

        if (ModCompatibilityCheck.VehicleIsActive && VehiclePawnType.IsAssignableFrom(thing.GetType()))
        {
            //Log.Message("GET ThingOwner FOR: " + thing);
            Log.Message($"VEHICLE: {thing}");
            __result = new VehicleThingOwner(thing, __result);
            return false;
        }


        return true;
    }


    private static bool TryFindBestBetterNonSlotGroupStorageFor(ref bool __result, Thing t, Pawn carrier, Map map, StoragePriority currentPriority, Faction faction, out IHaulDestination haulDestination, bool acceptSamePriority = false, bool requiresDestReservation = true)
    {
        haulDestination = null;

        if (carrier != null && t.TryGetComp<CompHauledToInventory>(out var c) && !MassUtility.WillBeOverEncumberedAfterPickingUp(carrier, t, t.stackCount))
        {

            if (ModCompatibilityCheck.CombatExtendedIsActive)
            {
                haulDestination = CritDestinationsMap.GetMatchingGunForAmmo(carrier, t);
                if (haulDestination != null)
                {
                    __result = true;
                    return false;
                }
            }


            if (ModCompatibilityCheck.VehicleIsActive)
            {
                haulDestination = CritDestinationsMap.GetMatchingVehiclePackagingForItem(carrier, t);
                if (haulDestination != null)
                {

                    __result = true;
                    return false;
                }
            }



            haulDestination = CritDestinationsMap.GetMatchingConstructableForMaterial(carrier, t);

            if (haulDestination != null)
            {

                __result = true;
                return false;
            }
        }



        return true;
    }


    private static bool Drop_Prefix(Pawn pawn, Thing thing)
    {
        //var takenToInventory = pawn.GetComp<CompHauledToInventory>();
        //if (takenToInventory == null)
        //{
        //    return true;
        //}

        //return !takenToInventory.Contains(thing);
        return true;
    }

    private static void Pawn_InventoryTracker_PostFix(Pawn_InventoryTracker __instance, Thing item)
    {
        //var takenToInventory = __instance.pawn?.GetComp<CompHauledToInventory>();
        //if (takenToInventory == null)
        //{
        //    return;
        //}

        //var carriedThing = takenToInventory.GetHashSet();
        //if (carriedThing?.Count > 0)
        //{
        //    carriedThing.Remove(item);
        //}
    }

    private static void JobDriver_HaulToCell_PostFix(JobDriver_HaulToCell __instance)
    {
        //var pawn = __instance.pawn;
        //var takenToInventory = pawn?.GetComp<CompHauledToInventory>();
        //if (takenToInventory == null)
        //{
        //    return;
        //}

        //var carriedThing = takenToInventory.CarriedThings;

        //if (__instance.job.haulMode == HaulMode.ToCellStorage
        //    && pawn.Faction == Faction.OfPlayerSilentFail
        //    && Settings.IsAllowedRace(pawn.RaceProps)
        //    && (Settings.AllowCorpses || pawn.carryTracker.CarriedThing is not Corpse)
        //    && carriedThing != null
        //    && carriedThing.Count != 0) //deliberate hauling job. Should unload.
        //{
        //    PawnUnloadChecker.CheckIfPawnShouldUnloadInventory(pawn, true);
        //}
    }

    public static void IdleJoy_Postfix(Pawn pawn) => PawnUnloadChecker.CheckIfPawnShouldUnloadInventory(pawn, true);

    public static void DropUnusedInventory_PostFix(Pawn pawn) => PawnUnloadChecker.CheckIfPawnShouldUnloadInventory(pawn);

    public static bool MaxAllowedToPickUpPrefix(Pawn pawn, ref int __result)
    {
        __result = int.MaxValue;
        return pawn.IsQuestLodger();
    }

    public static bool CanBeMadeToDropStuff(Pawn pawn, ref bool __result)
    {
        __result = !pawn.IsQuestLodger();
        return false;
    }

    public static bool SkipCorpses_Prefix(WorkGiver_Haul __instance, ref bool __result, Pawn pawn)
    {
        if (__instance is not WorkGiver_HaulCorpses)
        {
            return true;
        }

        if (Settings.AllowCorpses //Don't use the vanilla HaulCorpses WorkGiver if PUAH is allowed to haul those
            || pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).Count < 1) //...or if there are no corpses to begin with. Indeed Tynan did not foresee this situation
        {
            __result = true;
            return false;
        }

        return true;
    }

    /// <summary>
    /// For animal hauling
    /// </summary>
    public static IEnumerable<CodeInstruction> JobGiver_Haul_TryGiveJob_Transpiler(IEnumerable<CodeInstruction> instructions)
        => instructions.MethodReplacer(HaulAIUtility.HaulToStorageJob, HaulToStorageJobByRace);

    public static Job HaulToStorageJobByRace(Pawn p, Thing t) => Settings.IsAllowedRace(p.RaceProps) ? HaulToInventoryJob(p, t, false) : HaulAIUtility.HaulToStorageJob(p, t);
    private static Func<Pawn, Thing, bool, Job> HaulToInventoryJob => _haulToInventoryJob ??= new(((WorkGiver_Scanner)DefDatabase<WorkGiverDef>.GetNamed("HaulToInventory").Worker).JobOnThing);
    private static Func<Pawn, Thing, bool, Job> _haulToInventoryJob;

}


/* when a constructable things spawns we need to cache it (will clean the cashe every so often ticks in 'map') */
[HarmonyPatch(typeof(GenSpawn))]
[HarmonyPatch(nameof(GenSpawn.Spawn), new Type[] { typeof(Thing), typeof(IntVec3), typeof(Map), typeof(Rot4), typeof(WipeMode), typeof(bool), typeof(bool) })]
public class record_things_to_build
{

    [HarmonyPostfix]
    public static void Postfix(ref Thing __result, Thing newThing, IntVec3 loc, Map map, Rot4 rot, WipeMode wipeMode = 0, bool respawningAfterLoad = false, bool forbidLeavings = false)
    {
        if (__result is IConstructible)
        {
            if (__result is Blueprint_Install)
            {
                return;
            }
            //Log.Message("RECORDING CONSTRUCTABLE");
            //CritDestinationsMap.constructables.Add(__result);
        }

    }
}


