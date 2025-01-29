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
        res.Add(pawn.Reserve(TargetA, job, 1, job.count));

        if (TargetB.Thing != null && VehiclePawnProxy.VehiclePawnType.IsAssignableFrom(TargetB.Thing.GetType()))
        {
            CritDestinationsMap.AddVehicleHaul(job, TargetB.Thing as Pawn, TargetA.Thing, job.count);

            res.Add(pawn.Reserve(TargetB, job, 99));
        }
        else
        {
            res.Add(pawn.Reserve(TargetB, job));
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
            firstItem.Thing.Position,
            firstDestination.HasThing ? firstDestination.Thing.Position : firstDestination.Cell
        );
        Toil findBestDestinationForItemToil = FindBestDestinationForItemToil();
        Toil reserveAndGoPickupFoundItemToil = ReserveAndGoPickupFoundItemToil(goToPickupTargetToil);



        yield return goToPickupTargetToil;
        yield return pickUpItemToil;
        yield return checkIfReadyToHaulToil;
        yield return findNextItemCloseByToil;

        yield return Toils_Jump.JumpIf(makeUnloadJobToil, () => job.GetTarget(TargetIndex.A) == null);

        yield return findBestDestinationForItemToil;

        yield return Toils_Jump.JumpIf(findNextItemCloseByToil, () => job.GetTarget(TargetIndex.B) == null);

        yield return reserveAndGoPickupFoundItemToil;

        yield return makeUnloadJobToil;
        yield return waitToil;




        //      var nextTarget = Toils_JobTransforms.ExtractNextTargetFromQueue(TargetIndex.A); //also does count
        //yield return nextTarget;

        //yield return CheckForOverencumberedForCombatExtended();

        //var gotoThing = new Toil
        //{
        //	initAction = () => pawn.pather.StartPath(TargetThingA, PathEndMode.ClosestTouch),
        //	defaultCompleteMode = ToilCompleteMode.PatherArrival
        //};
        //gotoThing.FailOnDespawnedNullOrForbidden(TargetIndex.A);
        //yield return gotoThing;

        //var makeUnloadJob = new Toil //Queue next job
        //{
        //    initAction = () =>
        //    {
        //        var actor = pawn;
        //        var curJob = actor.jobs.curJob;
        //        var storeCell = curJob.targetB;

        //        var unloadJob = JobMaker.MakeJob(PickUpAndHaulJobDefOf.UnloadYourHauledInventory, storeCell);
        //        if (unloadJob.TryMakePreToilReservations(actor, false))
        //        {
        //            actor.jobs.jobQueue.EnqueueFirst(unloadJob, JobTag.Misc);
        //            EndJobWith(JobCondition.Succeeded);
        //            //This will technically release the cell reservations in the queue, but what can you do
        //        }
        //    }
        //};

        //yield return takeThing;
        //yield return Toils_Jump.JumpIf(nextTarget, () => !job.targetQueueA.NullOrEmpty());

        //yield return makeUnloadJob;
        //yield return waitToil;
    }



    private Toil ReserveAndGoPickupFoundItemToil(Toil goToPickupTargetToil)
    {
        var t = new Toil
        {
            initAction = () =>
            {

                var item = job.GetTarget(TargetIndex.A);
                var destination = job.GetTarget(TargetIndex.B);

                Log.Message("----MAKING NEXT reservations: " + item + " " + destination);

                pawn.Reserve(item, job, 1, job.count);
                if (destination.Thing != null && VehiclePawnProxy.VehiclePawnType.IsAssignableFrom(destination.Thing.GetType()))
                {
                    CritDestinationsMap.AddVehicleHaul(job, destination.Thing as Pawn, item.Thing, job.count);
                    pawn.Reserve(destination, job, 99);
                }
                else
                {
                    pawn.Reserve(destination, job);
                }

                pawn.jobs.curDriver.JumpToToil(goToPickupTargetToil);
            },

        };
        t.defaultCompleteMode = ToilCompleteMode.Instant;

        return t;
    }

    private Toil FindBestDestinationForItemToil()
    {
        var t = new Toil
        {
            initAction = () =>
            {
                var item = job.GetTarget(TargetIndex.A).Thing;

                Log.Message("----FIND best destination for item: " + item);

                var currentPriority = StoreUtility.CurrentStoragePriorityOf(item);

                if (Utils.FindDestinationForThing(
                    item, pawn, pawn.Map, currentPriority, false,
                    out var destinationTarget, out var count, out var interjectJob, out var _))
                {

                    Log.Message("----found good destination: " + destinationTarget + " " + count);

                    job.SetTarget(TargetIndex.B, destinationTarget);
                    job.count = Math.Min(item.stackCount, count);
                    return;

                }

                Log.Message("----no good destination for item: " + item);
                job.SetTarget(TargetIndex.B, null);
                job.count = -1;
            },

        };
        t.defaultCompleteMode = ToilCompleteMode.Instant;

        return t;
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
                unloadJob.targetQueueA = Aqueue;
                unloadJob.countQueue = Countqueue;

                actor.jobs.jobQueue.EnqueueFirst(unloadJob, JobTag.Misc);
                EndJobWith(JobCondition.Succeeded);
                return;
            },

        };

        return t;
    }

    private static Utils.ThingPositionComparer Comparer { get; } = new();
    public HashSet<Thing> Seen=new();

    private const float SEARCH_FOR_OTHERS_RANGE_FRACTION = 0.5f;

    private Toil FindNextItemCloseByToil(IntVec3 firstThingPosition, IntVec3 firstDestinationPosition)
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
                
                
                

                var distanceToHaul = (firstDestinationPosition - firstThingPosition).LengthHorizontal * SEARCH_FOR_OTHERS_RANGE_FRACTION;
                var distanceToSearchMore = Math.Max(12f, distanceToHaul);

                var maxDistanceSquared = distanceToSearchMore * distanceToSearchMore;

                var items = new List<Thing>();
                var center = firstThingPosition;
                var peMode = PathEndMode.ClosestTouch;
                var traverseParams = TraverseParms.For(pawn);

                

                while (Utils.FindClosestThing(
                    center, 
                    pawn.Map, 
                    pawn,
                    Seen,
                    (Thing i) => validator(i, pawn, itemIsUrgent(i), designationManager),
                    out Thing closestThing)
                )
                {
                    Log.Message("----look at: " + closestThing);
                    Seen.Add(closestThing);

                    if (closestThing.def.thingCategories.Any(v=>v.defName == "StoneChunks") 
                        && designationManager.DesignationOn(closestThing)?.def!=DesignationDefOf.Haul)
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
                    job.SetTarget(TargetIndex.A, closestThing);
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
                Toils_Haul.ErrorCheckForCarry(actor, item);

                var countToPickUp = Mathf.Min(job.count, MassUtility.CountToPickUpUntilOverEncumbered(actor, item));

                /*definitely want to pickup at least one*/
                countToPickUp = Mathf.Max(countToPickUp, 1);
                Log.Message($"----{actor} is hauling to inventory {item}:{countToPickUp}");

                var splitThing = item.SplitOff(countToPickUp);
                actor.inventory.GetDirectlyHeldThings().TryAdd(splitThing, splitThing.def.stackLimit > 1);

                /*Adjust record of things held just for hauling*/
                var i = AQ.FindIndex(v => v.Thing.def.defName == splitThing.def.defName);
                if (i == -1)
                {
                    AQ.Add(item);
                    CQ.Add(countToPickUp);
                }
                else
                {
                    CQ[i] += countToPickUp;
                }
                Log.Message("A queue: " + string.Join(", ", AQ.Select(v => v.Thing.def.defName)));
                Log.Message("Count queue: " + string.Join(", ", CQ.Select(v => v)));
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