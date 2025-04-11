using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.Noise;
using static PickUpAndHaul.WorkGiver_HaulToInventory;

namespace PickUpAndHaul;
public class JobDriver_HaulToInventory : JobDriver
{

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        var res = new List<bool>();

        if (!this.pawn.CanReserve(TargetA))
        {
            Log.Message($"cant reserve item: {pawn} {TargetA}");
            return false;
        }
        if (!this.pawn.CanReserve(TargetB))
        {
            Log.Message($"cant reserve location: {pawn} {TargetB}");
            return false;
        }


        pawn.Reserve(TargetA, job);
        
        if (TargetB.Thing != null && VehiclePawnProxy.VehiclePawnType.IsAssignableFrom(TargetB.Thing.GetType()))
        {
            CritDestinationsMap.AddVehicleHaul(job, TargetB.Thing as Pawn, TargetA.Thing, job.count);
            //this.pawn.Reserve(TargetB, this.job, 99);
        }
        else
        {
            this.pawn.Reserve(TargetB, this.job);
        }

        var rr = res.All(v => v);
        Log.Message($"{pawn} starting HaulToInventory job: {TargetA} to {TargetB} {rr}");
        return rr;

    }



    //get next, goto, take, check for more. Branches off to "all over the place"
    public override IEnumerable<Toil> MakeNewToils()
    {
        Log.Message("HAUL TO INVENTORY DRIVER: " + pawn);

        AddFinishAction(_ =>
        {
            CritDestinationsMap.RemoveVehicleHaul(job);
        });


        var firstItem = TargetA;
        var firstDestination = TargetB;

        /* Aqueu will contain items (i need that just for defnames)
         * Countqueue will tell me how much of that item I intend to unload.
         * This way if pawn has other items in the inventory he will not unload
         * more than was intended */
        var Aqueue = new List<LocalTargetInfo>() { };
        var Countqueue = new List<int> { };

        var waitToil = Toils_General.Wait(2);
        Toil makeUnloadJobToil = MakeUnloadJobToil(Aqueue, Countqueue);

        Toil goToPickupTargetToil = GoToTargetToil();
        Toil pickUpItemToil = PickUpItemToil(Aqueue, Countqueue);
        Toil checkIfReadyToHaulToil = CheckIfReadyToUnload(makeUnloadJobToil);
        Toil findNextItemCloseByToil = FindNextItemCloseByToil(
            firstDestination.HasThing ? firstDestination.Thing.Position : firstDestination.Cell
        );

        yield return goToPickupTargetToil;
        yield return pickUpItemToil;
        yield return checkIfReadyToHaulToil;
        yield return Toils_General.Wait(5);
        yield return findNextItemCloseByToil;
        yield return Toils_Jump.JumpIf(goToPickupTargetToil, () => job.GetTarget(TargetIndex.A) != null);

        yield return makeUnloadJobToil;
        yield return waitToil;
    }


    private LocalTargetInfo FindDestination(LocalTargetInfo ThingA, out int count)
    {
        var item = ThingA.Thing;

        Log.Message("----FIND best destination for item: " + item);

        var currentPriority = StoreUtility.CurrentStoragePriorityOf(item);

        if (Utils.FindDestinationForThing(
            item, pawn, pawn.Map, currentPriority, false,
            out var destinationTarget, out count, out var interjectJob, out var _))
        {

            Log.Message("----found good destination: " + destinationTarget + " " + count);

            return destinationTarget;

        }

        Log.Message("----no good destination for item: " + item);
        count = -1;
        return null;
    }

    private Toil MakeUnloadJobToil(List<LocalTargetInfo> Aqueue, List<int> Countqueue)
    {
        var t = new Toil
        {
            initAction = () =>
            {
                Log.Message("----QUEUE up unload job: " + pawn);
                var actor = pawn;

                var unloadJob = JobMaker.MakeJob(PickUpAndHaulJobDefOf.UnloadYourHauledInventory, null);

                actor.jobs.jobQueue.EnqueueFirst(unloadJob, JobTag.Misc);
                EndJobWith(JobCondition.Succeeded);
                return;
            },

        };

        return t;
    }

    private static Utils.ThingPositionComparer Comparer { get; } = new();
    public HashSet<Thing> Seen = new();
    private IntVec3 LastPickedupThingsPosition
    {
        get
        {
            return this.job.GetTarget(TargetIndex.C).Cell;
        }
        set
        {
            this.job.SetTarget(TargetIndex.C, value);
        }
    }
    private const float SEARCH_FOR_OTHERS_RANGE_FRACTION = 0.5f;

    private Toil FindNextItemCloseByToil(IntVec3 firstDestinationPosition)
    {
        var haulUrgentlyDesignation = PickUpAndHaulDesignationDefOf.haulUrgently;
        var map = pawn.Map;
        var designationManager = map.designationManager;

        Func<Thing, Pawn, bool, DesignationManager, bool> validator = (Thing t, Pawn pawn, bool isUrgent, DesignationManager designationManager) =>
            (!isUrgent || designationManager.DesignationOn(t)?.def == haulUrgentlyDesignation)
            && GoodThingToHaul(t, pawn) && HaulAIUtility.PawnCanAutomaticallyHaulFast(pawn, t, false); //forced is false, may differ from first thing


        Func<Thing, bool> itemIsUrgent = (item) =>
        {
            return ModCompatibilityCheck.AllowToolIsActive && designationManager.DesignationOn(item)?.def
                == haulUrgentlyDesignation;
        };

        var t = new Toil
        {


            initAction = () =>
            {
                Log.Message("----LOCATE NEXT THING");

                var distanceToHaul = (firstDestinationPosition - this.LastPickedupThingsPosition).LengthHorizontal * SEARCH_FOR_OTHERS_RANGE_FRACTION;
                var distanceToSearchMore = Math.Max(12f, distanceToHaul);

                var maxDistanceSquared = distanceToSearchMore * distanceToSearchMore;

                var items = new List<Thing>();
                var center = this.LastPickedupThingsPosition;
                var peMode = PathEndMode.ClosestTouch;
                var traverseParams = TraverseParms.For(pawn);


                Seen.Clear();
                var c = 0;

                while (Utils.FindClosestThing(
                    center,
                    pawn.Map,
                    pawn,
                    Seen,
                    (Thing i) => validator(i, pawn, itemIsUrgent(i), designationManager),
                    out Thing closestThing)
                )
                {
                    c++;
                    if (c > 100)
                    {
                        Log.Message("TERM SEARCH REACHED.");
                    }
                    Log.Message("----look at: " + closestThing);
                    Seen.Add(closestThing);

                    if (!closestThing.TryGetComp<CompHauledToInventory>(out var _))
                    {
                        continue;
                    }

                    if (closestThing.def.thingCategories.Any(v => v.defName == "StoneChunks")
                        && designationManager.DesignationOn(closestThing)?.def != DesignationDefOf.Haul)
                    {
                        continue;
                    }


                    if ((center - closestThing.Position).LengthHorizontalSquared > maxDistanceSquared)
                    {
                        Log.Message("----too far");
                        break;
                    }

                    if (!map.reachability.CanReach(center, closestThing, peMode, traverseParams))
                    {
                        Log.Message("----cant react");
                        continue;
                    }

                    Log.Message("----found next closes thing: " + pawn + " " + closestThing);

                    var dest = FindDestination(closestThing, out var count);
                    if (dest == null)
                    {
                        continue;
                    }

                    Log.Message($"----found good item and destination: {closestThing} {dest}.  making reservations.");

                    pawn.Reserve(closestThing, job);
                    if (dest.Thing != null && VehiclePawnProxy.VehiclePawnType.IsAssignableFrom(dest.Thing.GetType()))
                    {
                        CritDestinationsMap.AddVehicleHaul(job, dest.Thing as Pawn, closestThing, job.count);
                        //this.pawn.Reserve(destination, this.job, 99);
                    }
                    else
                    {
                        this.pawn.Reserve(dest, this.job);

                    }
                    

                    job.SetTarget(TargetIndex.A, closestThing);
                    job.SetTarget(TargetIndex.B, dest);
                    job.count = Math.Min(closestThing.stackCount, count);

                    return;
                }

                Log.Message("----no close thing near by to haul");
                job.SetTarget(TargetIndex.A, null);

                return;

            },

        };
        t.defaultCompleteMode = ToilCompleteMode.Instant;

        return t;
    }

    private Toil CheckIfReadyToUnload(Toil makeUnloadJobToil)
    {
        var t = new Toil
        {
            initAction = () =>
            {
                /* if pawn is encumbered then we have pickup just enough to start hauling */
                if (MassUtility.IsOverEncumbered(pawn) || HoldMultipleThings_Support.OverAllowedGearCapacity(pawn))
                {
                    Log.Message("----We are ready to unload: " + pawn);
                    pawn.jobs.curDriver.JumpToToil(makeUnloadJobToil);
                }
            },

        };

        return t;

    }

    private Toil PickUpItemToil(List<LocalTargetInfo> AQ, List<int> CQ)
    {
        var t = new Toil
        {
            initAction = () =>
            {
                var actor = pawn;
                var item = actor.CurJob.GetTarget(TargetIndex.A).Thing;
                this.LastPickedupThingsPosition = item.Position;
                Toils_Haul.ErrorCheckForCarry(actor, item);

                var countToPickUp = Mathf.Min(job.count, MassUtility.CountToPickUpUntilOverEncumbered(actor, item));

                /*definitely want to pickup at least one*/
                countToPickUp = Mathf.Max(countToPickUp, 1);
                Log.Message($"----{actor} is hauling to inventory {item}:{countToPickUp}");

                var splitThing = item.SplitOff(countToPickUp);
                if (actor.inventory.GetDirectlyHeldThings().TryAdd(splitThing, false))
                {
                    splitThing.GetHaulInventoryComp().Hauling = true;
                }
                else
                {
                    Log.Message($"COULDNOT ADD ITEM TO INVENTORY {this.pawn} {splitThing}");
                }
            }
        };
        return t;
    }

    private Toil GoToTargetToil()
    {
        var t = new Toil
        {
            initAction = () => pawn.pather.StartPath(job.GetTarget(TargetIndex.A).Thing, PathEndMode.ClosestTouch),
            defaultCompleteMode = ToilCompleteMode.PatherArrival
        };
        t.FailOnDespawnedNullOrForbidden(TargetIndex.A);
        return t;
    }

    //private static List<Thing> TempListForThings { get; } = new();

    ///// <summary>
    ///// the workgiver checks for encumbered, this is purely extra for CE
    ///// </summary>
    ///// <returns></returns>
    //public Toil CheckForOverencumberedForCombatExtended()
    //{
    //    var toil = new Toil();

    //    if (!ModCompatibilityCheck.CombatExtendedIsActive)
    //    {
    //        return toil;
    //    }

    //    toil.initAction = () =>
    //    {
    //        var actor = toil.actor;
    //        var curJob = actor.jobs.curJob;
    //        var nextThing = curJob.targetA.Thing;

    //        var ceOverweight = CompatHelper.CeOverweight(pawn);

    //        if (!(MassUtility.EncumbrancePercent(actor) <= 0.9f && !ceOverweight))
    //        {
    //            var haul = HaulAIUtility.HaulToStorageJob(actor, nextThing);
    //            if (haul?.TryMakePreToilReservations(actor, false) ?? false)
    //            {
    //                //note that HaulToStorageJob etc doesn't do opportunistic duplicate hauling for items in valid storage. REEEE
    //                actor.jobs.jobQueue.EnqueueFirst(haul, JobTag.Misc);
    //                EndJobWith(JobCondition.Succeeded);
    //            }
    //        }
    //    };

    //    return toil;
    //}
}