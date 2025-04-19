using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;
using Verse.Noise;
using static PickUpAndHaul.WorkGiver_HaulToInventory;

namespace PickUpAndHaul;
public class JobDriver_HaulToInventory : JobDriver
{
    private int _countToDrop = -1;
    private int unloadDuration = 3;
    private int? progressBarDelay = null;


    private static FieldInfo countToTransferFieldInfo = AccessTools.Field(typeof(TransferableOneWay), "countToTransfer");


    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {       
        

        if (this.pawn.CanReserve(TargetA))
        {
            pawn.Reserve(TargetA, job);
        }
        else
        {
            Log.Message($"cant reserve item: {pawn} {TargetA}");
            return false;
        }

        
        
        if (TargetB.Thing != null && VehiclePawnProxy.VehiclePawnType.IsAssignableFrom(TargetB.Thing.GetType()))
        {
            CritDestinationsMap.AddVehicleHaul(job, TargetB.Thing as Pawn, TargetA.Thing, job.count);
        }
        else
        {
            if (this.pawn.CanReserve(TargetB))
            {
                this.pawn.Reserve(TargetB, this.job);
            }
            else
            {
                Log.Message($"cant reserve location: {pawn} {TargetB}");
                return false;
            }
            
        }

        Log.Message($"{pawn} HAUL TO INVENTORY all reserved: {TargetA} to {TargetB}");
        return true;

    }



    //get next, goto, take, check for more. Branches off to "all over the place"
    public override IEnumerable<Toil> MakeNewToils()
    {
        Log.Message($"HAUL TO INVENTORY: {pawn} {TargetA} {TargetB}");

        AddFinishAction(_ =>
        {
            CritDestinationsMap.RemoveVehicleHaul(job);
        });

        var firstItem = TargetA;
        var firstDestination = TargetB;


        Toil readyToUnload = Toils_General.Wait(2);

        Toil goToPickupTarget = GoToTargetToil();
        Toil pickUpItemToil = PickUpItemToil();
        Toil checkIfReadyToUnload = CheckIfReadyToUnload(readyToUnload);
        Toil findNextItemCloseBy = FindNextItemCloseByToil(
            firstDestination.HasThing ? firstDestination.Thing.Position : firstDestination.Cell
        );

        yield return goToPickupTarget;
        yield return pickUpItemToil;
        yield return checkIfReadyToUnload;
        yield return Toils_General.Wait(5);
        yield return findNextItemCloseBy;
        yield return Toils_Jump.JumpIf(goToPickupTarget, () => job.GetTarget(TargetIndex.A) != null);

        yield return readyToUnload;

        Toil beginWait = Toils_General.Wait(2);
        Toil endWait = Toils_General.Wait(2);
        Toil successToil = new()
        {
            initAction = () =>
            {
                Log.Message($"END JOB DRIVER for pawn {pawn}");
                //EndJobWith(JobCondition.Succeeded);
                Log.Message($"END END");
            }
        };


        Toil findNextDestinationForFirstItem = FindNextDestinationForFirstItem(beginWait, successToil);
        Toil pullItemFromInventoryToil = PullItemFromInventoryToil(beginWait);
        Toil carryToCellToil = Toils_Haul.CarryHauledThingToCell(TargetIndex.B);
        Toil carryToContainerToil = Toils_Haul.CarryHauledThingToContainer();
        Toil showProgressBarToil = ShowProgressBarToil();
        Toil depositToContainerToil = Toils_Haul.DepositHauledThingInContainer(TargetIndex.B, TargetIndex.None);
        Toil thingLoaded = Toils_General.Wait(2);

        yield return beginWait;
        yield return findNextDestinationForFirstItem;
        yield return pullItemFromInventoryToil;

        var notAContainer = Toils_General.Wait(2);
        yield return Toils_Jump.JumpIf(notAContainer, () => false == job.GetTarget(TargetIndex.B).HasThing);

        yield return carryToContainerToil;
        yield return Toils_Construct.MakeSolidThingFromBlueprintIfNecessary(TargetIndex.B);
        yield return Toils_Jump.JumpIf(depositToContainerToil, () =>
        {
            return progressBarDelay == null;
        });

        yield return new Toil
        {
            debugName = "SET PROGRESSBAR DELAY",
            initAction = () =>
            {

                showProgressBarToil.defaultDuration = progressBarDelay.Value;
            }
        };
        yield return showProgressBarToil;
        var notAVehicle = Toils_General.Wait(2);
        if (ModCompatibilityCheck.VehicleIsActive)
        {
            yield return Toils_Jump.JumpIf(notAVehicle, () => DestinationNotVehicle());
            yield return DepositToVehicle();
            yield return Toils_Jump.Jump(thingLoaded);
        }

        yield return notAVehicle;
        yield return depositToContainerToil;
        yield return Toils_Jump.Jump(thingLoaded);


        yield return notAContainer;
        yield return carryToCellToil;

        yield return Toils_Haul.PlaceHauledThingInCell(TargetIndex.B, carryToCellToil, true);



        yield return thingLoaded;
        yield return Toils_General.Do(() =>
        {
            /*If for some reason pawn is still carrying anything, just drop it*/
            if (this.pawn.carryTracker.CarriedThing != null)
            {
                this.pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out var __);
            }

        });
        yield return Toils_Jump.Jump(beginWait);

        yield return successToil;
        yield return endWait;
    }


    private Toil DepositToVehicle()
    {
        return new Toil
        {
            initAction = () =>
            {
                var item = job.targetA.Thing;
                if (item is null || item.stackCount == 0)
                {
                    pawn.jobs.EndCurrentJob(JobCondition.Incompletable, true);
                }
                else
                {

                    int stackCount = item.stackCount; //store before transfer for transferable recache
                    var vehicle = new VehiclePawnProxy(job.targetB.Thing as Pawn);
                    Log.Message("----ADDING TO VEHICLE " + vehicle.Thing + " " + item + " " + stackCount);
                    int result = vehicle.AddOrTransfer(item, stackCount);
                    TransferableOneWay transferable = VehiclePawnProxy.GetTransferable(vehicle.CargoToLoad, item);
                    if (transferable != null)
                    {
                        int count = transferable.CountToTransfer - stackCount;
                        countToTransferFieldInfo.SetValue(transferable, count);
                        if (transferable.CountToTransfer <= 0)
                        {
                            vehicle.CargoToLoad.Remove(transferable);
                        }


                    }
                    CritDestinationsMap.JustLoaded(job, vehicle.Thing as Pawn, item, stackCount);
                }
            }
        };
    }

    private bool DestinationNotVehicle()
    {
        return HarmonyPatches.VehiclePawnType.IsAssignableFrom(job.targetB.Thing.GetType())
            == false;
    }

    private Toil ShowProgressBarToil()
    {
        Toil reloadWait = ToilMaker.MakeToil("reload-wait");
        reloadWait.defaultCompleteMode = ToilCompleteMode.Delay;
        reloadWait.defaultDuration = 10; /* will be adjusted in previous toil */
        reloadWait.WithProgressBarToilDelay(TargetIndex.B);

        return reloadWait;
    }


    private Toil PullItemFromInventoryToil(Toil startingToil)
    {
        Toil t = new()
        {
            initAction = () =>
            {

                var item = job.GetTarget(TargetIndex.A).Thing;
                var desiredShoulderCount = job.count;

                Log.Message("----PULL ITEM FROM INVENTORY: " + pawn + " " + item + " desired: " + desiredShoulderCount);

                /* First shoulder the item for which the destination was derived to give us a concrete
                 * def/stuff definition of the item going to that destination */
                pawn.inventory.innerContainer.TryTransferToContainer(item, pawn.carryTracker.innerContainer,
                        Math.Min(desiredShoulderCount, item.stackCount), out var shoulderedItem, true);

                if (shoulderedItem == null)
                {
                    Log.Message($"COULD NOT SHOULDER THE INITIAL ITEM FOR SOME REAONS {this.pawn} {item}");
                }


                var shoulderedCount = pawn.carryTracker.CarriedThing.stackCount;

                /* if shoulder count is under desired then keep looking at inventory to pull upto desired count*/
                var c = 0;
                while (shoulderedCount < desiredShoulderCount)
                {
                    c++;
                    if (c > 1000)
                    {
                        Log.Message("TERMINATE LOOP!");
                        break;
                    }
                    var stillNeed = desiredShoulderCount - shoulderedCount;
                    var anotherInventoryItem = pawn.inventory.innerContainer.FirstOrDefault(v => v.CanStackWith(item) && v.GetHaulInventoryComp().Hauling);

                    /*no more same item in inventory*/
                    if (anotherInventoryItem == null)
                    {
                        break;
                    }

                    Log.Message($"----inventory item: {anotherInventoryItem} we still need: {stillNeed}");

                    pawn.inventory.innerContainer.TryTransferToContainer(anotherInventoryItem, pawn.carryTracker.innerContainer,
                        desiredShoulderCount - shoulderedCount, out var attemptedShoulderedItem, true);

                    /*could not pull out new item out of inventory for some reason*/
                    if (attemptedShoulderedItem == null)
                    {
                        break;
                    }

                    shoulderedCount = pawn.carryTracker.CarriedThing.stackCount;
                }

                shoulderedItem = pawn.carryTracker.CarriedThing;
                shoulderedItem.GetHaulInventoryComp().Hauling = true;

                if (shoulderedItem == null)
                {
                    Log.Message($"NO ITEM ON SHOULDER! {this.pawn} {shoulderedItem}");
                    return;
                }

                if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
                {
                    Log.Message($"----Pawn {pawn} incapable of hauling, dropping {item}");

                    pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out var __);
                    pawn.jobs.curDriver.JumpToToil(startingToil);
                    return;
                }

                /* At this point we shouldered the item. */
                job.SetTarget(TargetIndex.A, shoulderedItem);
                job.count = shoulderedItem.stackCount;

                if (pawn.CanReserve(shoulderedItem))
                    pawn.Reserve(shoulderedItem, job, 1, shoulderedCount);

            }
        };
        t.defaultCompleteMode = ToilCompleteMode.Instant;
        return t;
    }


    private Toil FindNextDestinationForFirstItem(Toil beginWait, Toil successToil)
    {
        var t = Toils_General.Do(() =>
        {
            Log.Message($"FindNextDestinationForFirstItem {pawn}");

            var hauledInventoryItem = pawn.inventory.innerContainer.FirstOrDefault(v => v.GetHaulInventoryComp().Hauling);

            if (hauledInventoryItem == null)
            {
                /*No more items.  Job is good?*/
                Log.Message("----No more items hauled");
                pawn.jobs.curDriver.JumpToToil(successToil);
                return;
            }

            var currentPriority = StoragePriority.Unstored;

            /*When we picked up the item we added a haul record so that no one else 
             * picks the same thing for the same vehicle.  As we now search to unload, we need to
             * clear that reservation so we can find the same vehicle destination.
             * Will re-reserve after finding destination.
             * 
             * If there's more than 1 vehicle recorded to this job, this will cause issues. Don't pack multiple vehicles? */
            CritDestinationsMap.RemoveVehicleHaul(job);

            if (Utils.FindDestinationForThing(hauledInventoryItem, pawn, pawn.Map,
                currentPriority, false, out var destinationTarget, out var desiredCountAtDestination, out var _,
                out int? progressBarDelay)
            )
            {

                Log.Message("----desired count at destination: " + desiredCountAtDestination);

                job.SetTarget(TargetIndex.A, hauledInventoryItem);
                job.SetTarget(TargetIndex.B, destinationTarget);
                job.count = Math.Min(desiredCountAtDestination, hauledInventoryItem.def.stackLimit);

                /*reserve */
                if (destinationTarget.HasThing && VehiclePawnProxy.VehiclePawnType.IsAssignableFrom(destinationTarget.Thing.GetType()))
                {
                    CritDestinationsMap.AddVehicleHaul(job, destinationTarget.Thing as Pawn, hauledInventoryItem, job.count);
                }
                else
                {
                    if (pawn.CanReserve(TargetB))
                        pawn.Reserve(TargetB, job);
                }

                this.progressBarDelay = progressBarDelay;

            }
            else
            {
                Log.Message("----could not find destination for item: " + hauledInventoryItem + " drop it");
                pawn.inventory.innerContainer.TryDrop(hauledInventoryItem, ThingPlaceMode.Near, hauledInventoryItem.stackCount, out _);

                pawn.jobs.curDriver.JumpToToil(beginWait);
                return;
            }

        });


        return t;
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
                    12,
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

                    if (closestThing.def.thingCategories!=null 
                        && closestThing.def.thingCategories.Where(v=>v!=null).Any(v => v.defName == "StoneChunks")
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
                Log.Message($"CheckIfReadyToUnload {pawn}");
                /* if pawn is encumbered then we have pickup just enough to start hauling */
                if (MassUtility.IsOverEncumbered(pawn) || HoldMultipleThings_Support.OverAllowedGearCapacity(pawn))
                {
                    Log.Message($"CheckIfReadyToUnload YES!");
                    pawn.jobs.curDriver.JumpToToil(makeUnloadJobToil);
                }
            },

        };

        return t;

    }

    private Toil PickUpItemToil()
    {
        var t = new Toil
        {
            initAction = () =>
            {
                
                var actor = pawn;
                Log.Message($"PickUpItemToil {actor}");
                var item = actor.CurJob.GetTarget(TargetIndex.A).Thing;
                
                this.LastPickedupThingsPosition = item.Position;
                

                Toils_Haul.ErrorCheckForCarry(actor, item);
                var countToPickUp = Mathf.Min(job.count, MassUtility.CountToPickUpUntilOverEncumbered(actor, item));

                /*definitely want to pickup at least one*/
                countToPickUp = Mathf.Max(countToPickUp, 1);

                

                var splitThing = item.SplitOff(countToPickUp);

                Log.Message($"{item}:{countToPickUp} {this.LastPickedupThingsPosition} splitThign: {splitThing}:{splitThing.stackCount}");
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

}

//if (pawn.Map.reservationManager.ReservedBy(job.targetA, pawn, pawn.CurJob))
//{
//    pawn.Map.reservationManager.Release(job.targetA, pawn, pawn.CurJob);
//}

//if (pawn.Map.reservationManager.ReservedBy(job.targetB, pawn, pawn.CurJob))
//{
//    pawn.Map.reservationManager.Release(job.targetB, pawn, pawn.CurJob);
//}