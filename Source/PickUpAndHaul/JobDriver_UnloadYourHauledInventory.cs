using HarmonyLib;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;
using Verse.AI;

namespace PickUpAndHaul;

public class JobDriver_UnloadYourHauledInventory : JobDriver
{
    private int _countToDrop = -1;
    private int unloadDuration = 3;
    private int? progressBarDelay = null;

    private static FieldInfo countToTransferFieldInfo = AccessTools.Field(typeof(TransferableOneWay), "countToTransfer");



    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        return true;
    }


    /// <summary>
    /// Find spot, reserve spot, pull thing out of inventory, go to spot, drop stuff, repeat.
    /// </summary>
    /// <returns></returns>
    public override IEnumerable<Toil> MakeNewToils()
    {
        Log.Message("UNLOAD DRIVER FOR: " + pawn);


        AddFinishAction(_ =>
        {
            CritDestinationsMap.RemoveVehicleHaul(job);
        });
        

        Toil successToil = new()
        {
            initAction = () =>
            {
                Log.Message($"END JOB DRIVER for pawn {pawn}");
                EndJobWith(JobCondition.Succeeded);
                Log.Message($"END END");
            }
        };
        Toil endWait = Toils_General.Wait(2);

        Toil findNextDestinationForFirstItem = FindNextDestinationForFirstItem(successToil);
        Toil pullItemFromInventoryToil = PullItemFromInventoryToil(findNextDestinationForFirstItem);
        Toil carryToCellToil = Toils_Haul.CarryHauledThingToCell(TargetIndex.B);
        Toil carryToContainerToil = Toils_Haul.CarryHauledThingToContainer();
        Toil showProgressBarToil = ShowProgressBarToil();
        Toil depositToContainerToil = Toils_Haul.DepositHauledThingInContainer(TargetIndex.B, TargetIndex.None);
        Toil releaseReservation = ReleaseReservation();

        yield return findNextDestinationForFirstItem;
        yield return pullItemFromInventoryToil;

        yield return Toils_Jump.JumpIf(carryToCellToil, () => false == job.GetTarget(TargetIndex.B).HasThing);

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
        if (ModCompatibilityCheck.VehicleIsActive)
        {
            yield return Toils_Jump.JumpIf(depositToContainerToil, () => DestinationNotVehicle());
            yield return DepositToVehicle();
            yield return Toils_Jump.Jump(releaseReservation);
        }
        yield return depositToContainerToil;
        yield return Toils_Jump.Jump(releaseReservation);


        yield return carryToCellToil;
        yield return Toils_Haul.PlaceHauledThingInCell(TargetIndex.B, carryToCellToil, true);



        yield return releaseReservation;
        yield return Toils_Jump.Jump(findNextDestinationForFirstItem);

        yield return successToil;
        yield return endWait;

    }

    private Toil FindNextDestinationForFirstItem(Toil successToil)
    {
        Toil t = null;
        t = new Toil
        {
            initAction = () =>
            {
                Log.Message("----FIND NEXT DESTINATION");

                if (job.targetQueueA.Count==0)
                {
                    /*No more items.  Job is good?*/

                    Log.Message("----No more items hauled");
                    pawn.jobs.curDriver.JumpToToil(successToil);
                    return;
                }

                var item = job.targetQueueA[0].Thing;
                var haulingQuantity = job.countQueue[0];

                Log.Message("----item: " + item + " current count: " + haulingQuantity);

                var inventoryItem = pawn.inventory.innerContainer.FirstOrDefault(v => v.def.defName == item.def.defName);

                /*Along the way we may have droped? that item.  Do we still have it in our inventory?*/
                if (inventoryItem == null)
                {
                    Log.Message("----item not found in inventory for some reason.  Next item.");
                    job.targetQueueA.RemoveAt(0);
                    job.countQueue.RemoveAt(0);

                    pawn.jobs.curDriver.SetNextToil(t);
                    return;
                }

                var currentPriority = StoragePriority.Unstored;

                if (Utils.FindDestinationForThing(inventoryItem, pawn, pawn.Map,
                    currentPriority, false, out var destinationTarget, out var desiredCountAtDestination, out var _,
                    out int? progressBarDelay)

                )
                {
                    /*we may have shelf that can accept 200 rice but rice can only be shouldered at 99 stackLimit*/
                    desiredCountAtDestination = Math.Min(desiredCountAtDestination, item.def.stackLimit);
                    
                    Log.Message("----found place that will accept count: " + destinationTarget + " " + desiredCountAtDestination);
                    var desiredShoulderQuantity = Mathf.Min(desiredCountAtDestination, haulingQuantity);

                    Log.Message("----will deliver to that place amount: " + desiredShoulderQuantity);

                    job.SetTarget(TargetIndex.A, inventoryItem);
                    job.SetTarget(TargetIndex.B, destinationTarget);
                    job.count = desiredShoulderQuantity;

                    /*reserve */
                    if (destinationTarget.HasThing && VehiclePawnProxy.VehiclePawnType.IsAssignableFrom(destinationTarget.Thing.GetType()))
                    {
                        CritDestinationsMap.AddVehicleHaul(job, destinationTarget.Thing as Pawn, inventoryItem, job.count);
                        pawn.Reserve(TargetB, job, 99);
                    }
                    else
                    {
                        pawn.Reserve(TargetB, job);
                    }

                    this.progressBarDelay = progressBarDelay;

                }
                else
                {
                    Log.Message("----could not find destination for item: " + item + " drop all of it");

                    /*there could be lots of stacks of this item in our inventory.  LOL */
                    do
                    {
                        pawn.inventory.innerContainer.TryDrop(inventoryItem, ThingPlaceMode.Near,
                        inventoryItem.stackCount, out _);

                        if (item.def.stackLimit==1)
                        {
                            break;
                        }

                        inventoryItem = pawn.inventory.innerContainer.FirstOrDefault(v => v.def.defName == item.def.defName);
                    } while (inventoryItem != null);
                    

                    job.targetQueueA.RemoveAt(0);
                    job.countQueue.RemoveAt(0);

                    pawn.jobs.curDriver.SetNextToil(t);
                    return;

                    
                    //EndJobWith(JobCondition.Succeeded);
                }

            }
        };
        t.defaultCompleteMode = ToilCompleteMode.Instant;

        return t;
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

    private bool TargetIsCell() => !TargetB.HasThing;

    private Toil ShowProgressBarToil()
    {
        Toil reloadWait = ToilMaker.MakeToil("reload-wait");
        reloadWait.defaultCompleteMode = ToilCompleteMode.Delay;
        reloadWait.defaultDuration = 10; /* will be adjusted in previous toil */
        reloadWait.WithProgressBarToilDelay(TargetIndex.B);

        return reloadWait;
    }

    private Toil ReleaseReservation()
    {
        return new()
        {
            initAction = () =>
            {
                if (pawn.Map.reservationManager.ReservedBy(job.targetA, pawn, pawn.CurJob))
                {
                    pawn.Map.reservationManager.Release(job.targetA, pawn, pawn.CurJob);
                }

                if (pawn.Map.reservationManager.ReservedBy(job.targetB, pawn, pawn.CurJob))
                {
                    pawn.Map.reservationManager.Release(job.targetB, pawn, pawn.CurJob);
                }

            }
        };
    }

    private Toil PullItemFromInventoryToil(Toil findNextDestinationForFirstItem)
    {
        Toil t = new()
        {
            initAction = () =>
            {

                var item = job.GetTarget(TargetIndex.A).Thing;
                var desiredShoulderedCount = job.count;

                Log.Message("----PULL ITEM FROM INVENTORY: " + pawn + " " + item + " desired: " + desiredShoulderedCount);

                if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) || !item.def.EverStorable(false))
                {
                    Log.Message($"----Pawn {pawn} incapable of hauling, dropping {item}");

                    job.targetQueueA.RemoveAt(0);
                    job.countQueue.RemoveAt(0);

                    pawn.jobs.curDriver.JumpToToil(findNextDestinationForFirstItem);
                    return;
                }

                var shoulderedCount = 0;
                Thing shoulderedItem = null;

                while (shoulderedCount < desiredShoulderedCount)
                {
                    var inventoryItem = pawn.inventory.innerContainer.FirstOrDefault(v => v.def.defName == item.def.defName);
                    if (inventoryItem == null)
                    {
                        /* did we lose some along the way? */
                        Log.Message($"----no more items in the inventory to satisfy the count for {item}.  Resulting count {shoulderedCount}");

                        break;
                    }

                    pawn.inventory.innerContainer.TryTransferToContainer(inventoryItem, pawn.carryTracker.innerContainer,
                        desiredShoulderedCount - shoulderedCount, out shoulderedItem, true);

                    shoulderedItem = pawn.carryTracker.CarriedThing;
                    if (shoulderedItem == null)
                    {
                        Log.Message("----SOMETHING IS WRONG.  TRANSFERED ITEM TO SHOULDER BUT NO ITEM THERE?! " + pawn + " " + item + " " + desiredShoulderedCount);
                    }
                    shoulderedCount = shoulderedItem.stackCount;
                }

                /* At this point we shouldered the item. */
                job.SetTarget(TargetIndex.A, shoulderedItem);
                job.count = shoulderedCount;

                job.countQueue[0] -= shoulderedCount;
                if (job.countQueue[0] < 0)
                {
                    Log.Message("----SOMETHING WENT TERRIBLY WRONG!  CHECK YOUR LOGIC, BLOCK HEAD!");
                }

                if (job.countQueue[0] == 0)
                {
                    Log.Message("----no more deliveries for this item.");
                    /*if no more scheduled deliveries for this item/count might as well remove it now. */
                    job.targetQueueA.RemoveAt(0);
                    job.countQueue.RemoveAt(0);
                }


                pawn.Reserve(shoulderedItem, job, 1, shoulderedCount);


                /*TODO: don't know why this is called */
                //shoulderedItem.SetForbidden(false, false);

            }
        };
        t.defaultCompleteMode = ToilCompleteMode.Instant;
        return t;
    }
    public static Thing ExtractThing(IHaulDestination t, out LocalTargetInfo loc, out int? countNeeded, out int? progressBarDelay)
    {
        Thing thing = null;

        loc = null;
        countNeeded = null;
        progressBarDelay = null;

        if (t is null)
            return null;

        if (t is CriticalThingHaulDestination wrapper)
        {
            thing = wrapper.Thing;
            loc = new LocalTargetInfo(thing);
            countNeeded = wrapper.CountNeeded;
            Log.Message("PROGRESS BAR DELAY: " + wrapper.ProgressBarDelay);
            progressBarDelay = wrapper.ProgressBarDelay;
        }
        else if (t is Thing tt)
        {
            thing = tt;
        }

        return thing;
    }

    private Toil FindTargetOrDrop(HashSet<Thing> carriedThings)
    {
        return new()
        {
            initAction = () =>
            {
                var unloadableThing = FirstUnloadableThing(pawn, carriedThings);

                if (unloadableThing.Count == 0)
                {
                    if (carriedThings.Count == 0)
                    {
                        EndJobWith(JobCondition.Succeeded);
                    }
                    return;
                }

                var currentPriority = StoragePriority.Unstored; // Currently in pawns inventory, so it's unstored
                int? countNeeded = null;
                if (StoreUtility.TryFindBestBetterStorageFor(unloadableThing.Thing, pawn, pawn.Map, currentPriority,
                        pawn.Faction, out var cell, out var destination))
                {
                    job.SetTarget(TargetIndex.A, unloadableThing.Thing);

                    var destinationThing = ExtractThing(destination, out var _, out countNeeded, out progressBarDelay);
                    Log.Message("DESTINATION: " + destinationThing + " " + cell + " " + destination);
                    if (cell == IntVec3.Invalid)
                    {
                        job.SetTarget(TargetIndex.B, destinationThing);
                    }
                    else
                    {
                        job.SetTarget(TargetIndex.B, cell);
                    }

                    Log.Message($"{pawn} found destination {job.targetB} for thing {unloadableThing.Thing}");
                    if (!pawn.Map.reservationManager.Reserve(pawn, job, job.targetB))
                    {
                        Log.Message(
                            $"{pawn} failed reserving destination {job.targetB}, dropping {unloadableThing.Thing}");
                        pawn.inventory.innerContainer.TryDrop(unloadableThing.Thing, ThingPlaceMode.Near,
                            unloadableThing.Thing.stackCount, out _);
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }
                    _countToDrop = countNeeded.HasValue ? countNeeded.Value : unloadableThing.Thing.stackCount;
                }
                else
                {
                    Log.Message(
                        $"Pawn {pawn} unable to find hauling destination, dropping {unloadableThing.Thing}");
                    pawn.inventory.innerContainer.TryDrop(unloadableThing.Thing, ThingPlaceMode.Near,
                        unloadableThing.Thing.stackCount, out _);
                    EndJobWith(JobCondition.Succeeded);
                }
            }
        };
    }

    private static ThingCount FirstUnloadableThing(Pawn pawn, HashSet<Thing> carriedThings)
    {
        var innerPawnContainer = pawn.inventory.innerContainer;

        foreach (var thing in carriedThings.OrderBy(t => t.def.FirstThingCategory?.index).ThenBy(x => x.def.defName))
        {
            //find the overlap.
            if (!innerPawnContainer.Contains(thing))
            {
                //merged partially picked up stacks get a different thingID in inventory
                var stragglerDef = thing.def;
                carriedThings.Remove(thing);

                //we have no method of grabbing the newly generated thingID. This is the solution to that.
                for (var i = 0; i < innerPawnContainer.Count; i++)
                {
                    var dirtyStraggler = innerPawnContainer[i];
                    if (dirtyStraggler.def == stragglerDef)
                    {
                        return new ThingCount(dirtyStraggler, dirtyStraggler.stackCount);
                    }
                }
            }
            return new ThingCount(thing, thing.stackCount);
        }
        return default;
    }
}
