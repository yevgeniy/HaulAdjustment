using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using Verse;

namespace PickUpAndHaul
{

    public class CustomJobDriver_VehicalHaul : CustomJobDriver
    {
        Pawn worker;
        Pawn vehicle;
        private List<LocalTargetInfo> cells;
        private IntVec3 cell;
        private List<Pawn> allPawns;

        public CustomJobDriver_VehicalHaul(Pawn worker, Pawn vehicle)
        {
            this.worker = worker;
            this.vehicle = vehicle;
        }

        public override bool TryMakePreToilReservations()
        {
            Log.Message($"mark hevicle busy ${this.worker} {this.vehicle}");
            this.vehicle.GetHaulInventoryComp().VehicleIsBusy = true;
            return true;
        }


        public override IEnumerable<CustomToil> MakeNewToils()
        {
            Log.Message($"START DRIVER HAUL {this.worker} {this.vehicle}");

            AddFinishAction(() =>
            {
                Log.Message($"FINISHED HAULING");
                this.vehicle.GetHaulInventoryComp().VehicleIsBusy = false;
            });
            AddFailCondition(() =>
            {
                return this.worker.Drafted || this.worker.Downed;
            });
            AddFailCondition(() =>
            {
                return false == this.vehicle.GetHaulInventoryComp().VehicleShouldBeUsedToHaul;
            });
            
            CustomToil exitVehicleAtEnd = ExitVehice();

            yield return Wait(2);

            yield return PreCheckForThings();
            yield return BoardVehicle();
            yield return TurnOnVehicle();
            yield return DriveToFromZone();
            yield return ExitVehice();
            yield return Wait(2);

            yield return WaitForPacked();

            yield return BoardVehicleAllPawns();
            yield return DropWeight();
            yield return DriveBack();
            yield return UnloadAll();


            yield return exitVehicleAtEnd;
        }

        private CustomToil PreCheckForThings()
        {
            return new CustomToil
            {
                initAction = () =>
                {
                    var vehicle = this.vehicle;
                    var worker = this.worker;

                    var firstGoodThingToLoadAtLocation = JobDriver_PackHauler.FindClosestLoadableThingFromLocation(this.worker, vehicle.GetHaulInventoryComp().FromLocation);
                    if (firstGoodThingToLoadAtLocation == null)
                    {
                        Messages.Message($"Nothing more to pack at location {vehicle.GetHaulInventoryComp().FromLocation}", MessageTypeDefOf.NeutralEvent);
                        EndJob();
                        Find.TickManager.Pause();
                        return;
                    }

                    ReadyForNextToil();
                }
            };
        }

        private CustomToil UnloadAll()
        {
            return new CustomToil
            {
                initAction = () =>
                {
                    Log.Message($"UNLOAD ALL");
                },
                tickAction = () =>
                {
                    var vehicle = this.vehicle;
                    var v = new VehiclePawnProxy(vehicle);

                    var firstItem = vehicle.inventory.innerContainer.FirstOrDefault();
                    if (firstItem != null)
                    {
                        vehicle.inventory.innerContainer.TryDrop(firstItem, ThingPlaceMode.Near, firstItem.stackCount, out var _);
                    }
                    else
                    {
                        ReadyForNextToil();
                    }
                }
            };
        }


        private CustomToil DriveBack()
        {
            return new CustomToil
            {
                initAction = () =>
                {
                    var vehicle = this.vehicle;
                    Log.Message($"DRIVE BACK");
                    var toLocation=vehicle.GetHaulInventoryComp().ToLocation;
                    if (toLocation==default(IntVec3))
                    {
                        Log.Message("No to spot?");
                        EndJob();
                        return;
                    }

                    if (!Utils.CanGetToCell(vehicle, toLocation))
                    {
                        Messages.Message($"vehicle cant fit in slot {toLocation}", MessageTypeDefOf.NeutralEvent);
                        Find.TickManager.Pause();
                        EndJob();
                        return;
                    }

                    this.cell = toLocation;

                    Log.Message($"----found valid cell {cell}");
                    var v = new VehiclePawnProxy(vehicle);
                    v.GoTo(cell);
                },
                tickAction = () =>
                {
                    var vehicle = this.vehicle;
                    var destinationCell = this.cell;
                    Log.Message($"----current positions: {vehicle.Position} {destinationCell}");
                    if (vehicle.Position == destinationCell)
                    {
                        ReadyForNextToil();
                    }
                }
            };
        }



        private CustomToil DropWeight()
        {
            return new CustomToil
            {
                initAction = () =>
                {
                    Log.Message($"IS VEHICLE READY TO DRIVE BACK");

                },
                tickAction = () =>
                {
                    var vehicle = this.vehicle;
                    var v = new VehiclePawnProxy(vehicle);
                    if (!v.IsOverloaded)
                    {
                        Log.Message($"----weight good.");
                        ReadyForNextToil();
                        return;
                    }

                    var firstItem = vehicle.inventory.innerContainer.First();
                    Log.Message($"----still overweight dropping {firstItem}");

                    vehicle.inventory.innerContainer.TryDrop(firstItem, ThingPlaceMode.Near, firstItem.stackCount, out var _);

                }
            };
        }


        private CustomToil WaitForPacked()
        {
            return new CustomToil
            {
                initAction = () =>
                {
                    Log.Message($"WAITING FOR VEHICLE PACKED");

                    this.vehicle.GetHaulInventoryComp().AllPacked = false;

                    Log.Message($"--boarded pawns {string.Join(", ", this.allPawns)}");
                    foreach(var worker in this.allPawns)
                    {

                        var job = new Job_PackHauler();
                        job.targetA = this.vehicle;
                        job.targetB = this.cell;

                        worker.jobs.StopAll();

                        Log.Message($"--starting pack hauling vehicle job for {worker}");
                        worker.jobs.StartJob(job);
                    }

                },
                tickAction = () =>
                {
                    if (this.vehicle.GetHaulInventoryComp().AllPacked)
                    {
                        ReadyForNextToil();
                    }
                }
            };
        }

        private CustomToil DriveToFromZone()
        {
            return new CustomToil
            {
                initAction = () =>
                {
                    var vehicle = this.vehicle;
                    Log.Message($"DRIVE TO FROM ZONE");
                    var fromLocation = vehicle.GetHaulInventoryComp().FromLocation;

                    if (!Utils.CanGetToCell(vehicle, fromLocation))
                    {
                        Messages.Message($"vehicle can't fit in this cell {fromLocation}", MessageTypeDefOf.NeutralEvent);
                        Find.TickManager.Pause();
                        EndJob();
                        
                        return;
                    }
                    this.cell = fromLocation;

                    var v = new VehiclePawnProxy(vehicle);
                    v.GoTo(cell);
                },
                tickAction = () =>
                {
                    var vehicle = this.vehicle;
                    var destinationCell = this.cell;
                    Log.Message($"----current positions: {vehicle.Position} {destinationCell}");
                    if (vehicle.Position == destinationCell)
                    {
                        ReadyForNextToil();
                    }
                }
            };
        }
        private CustomToil TurnOnVehicle()
        {
            return new CustomToil
            {
                initAction = () =>
                {
                    var vehicle = this.vehicle;
                    var v = new VehiclePawnProxy(vehicle);
                    v.Drafted = true;

                    ReadyForNextToil();
                }
            };
            
        }

        private CustomToil ExitVehice()
        {
            return new CustomToil
            {
                initAction = () =>
                {
                    var vehicle = this.vehicle;
                    var v = new VehiclePawnProxy(vehicle);
                    this.allPawns = v.AllPawnsAboard.ToList();
                    Log.Message($"--all pawns on board {string.Join(", ", this.allPawns)}");

                    v.Drafted = false;

                    v.DisembarkAll();

                    ReadyForNextToil();
                }
            };
        }
        private CustomToil BoardVehicleAllPawns()
        {
            return new CustomToil
            {
                initAction = () =>
                {
                    var vehicle = this.vehicle;
                    var allPawns = this.allPawns;

                    Log.Message($"LOAD PAWNs IN VEHICLE {string.Join(", ", allPawns)} {vehicle}");
                    var vehicleProxy = new VehiclePawnProxy(vehicle);

                    var boarded = vehicleProxy.AllPawnsAboard;

                    if (boarded.Count > 0 && allPawns.All(v=> boarded.Contains(v)))
                    {
                        Log.Message($"--worker already border boarded");
                        ReadyForNextToil();
                        return;
                    }



                    Log.Message($"--telling pawns to board");

                    foreach(var worker in allPawns)
                    {
                        var handler = vehicleProxy.NextAvailableHandler();
                        Log.Message($"----hander {handler} {worker}");


                        if (worker.carryTracker.CarriedThing != null)
                        {
                            worker.carryTracker.TryDropCarriedThing(worker.Position, ThingPlaceMode.Near, out var _);
                        }
                        vehicleProxy.PromptToBoardVehicle(worker, handler);
                    }
                    
                },
                tickAction = () =>
                {
                    Log.Message($"waiting");
                    var vehicle = this.vehicle;
                    var allPawns = this.allPawns;
                    var vehicleProxy = new VehiclePawnProxy(vehicle);
                    var boarded = vehicleProxy.AllPawnsAboard;
                    if (boarded.Count > 0 && allPawns.All(v => boarded.Contains(v)))
                    {
                        Log.Message($"--all workers borded vehicle");
                        this.ReadyForNextToil();
                        return;
                    }
                }
            };
        }
        private CustomToil BoardVehicle()
        {


            return new CustomToil
            {
                initAction = () =>
                {
                    var vehicle = this.vehicle;
                    var worker = this.worker;

                    Log.Message($"LOAD PAWN IN VEHICLE {worker} {vehicle}");
                    var vehicleProxy = new VehiclePawnProxy(vehicle);

                    var boarded = vehicleProxy.AllPawnsAboard;
                    if (boarded.Count > 0 && boarded.Contains(worker))
                    {
                        Log.Message($"--worker already border boarded");
                        ReadyForNextToil();
                        return;
                    }

                    Log.Message($"--telling pawn {worker} to board");
                    var handler = vehicleProxy.NextAvailableHandler();
                    Log.Message($"----hander {handler}");

                    if (worker.carryTracker.CarriedThing!=null)
                    {
                        worker.carryTracker.TryDropCarriedThing(worker.Position, ThingPlaceMode.Near, out var _);
                    }
                    vehicleProxy.PromptToBoardVehicle(worker, handler);
                },
                tickAction = () =>
                {
                    Log.Message($"waiting");
                    var vehicle = this.vehicle;
                    var worker = this.worker;
                    var vehicleProxy = new VehiclePawnProxy(vehicle);
                    var boarded = vehicleProxy.AllPawnsAboard;
                    if (boarded.Count > 0 && boarded.Contains(worker))
                    {
                        Log.Message($"--worker already border boarded");
                        this.ReadyForNextToil();
                        return;
                    }
                }
            };
        }


        private CustomToil Wait(int v)
        {
            var cur = 0;
            return new CustomToil
            {
                tickAction = () =>
                {
                    cur++;
                    if (cur == v)
                    {
                        ReadyForNextToil();
                    }
                }
            };
        }


    }

    public class JobDriver_PackHauler : JobDriver
    {
        public override string GetReport()
        {
            if (this.job.def==null)
            {
                Log.Message($"SOMEHOW THIS JOB GOT NO DEF");
                this.job.def= new JobDef
                {
                    driverClass = typeof(JobDriver_PackHauler)
                };
            }
            return "Using vehicle to haul.";
        }
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            
            return true;
        }
        public override IEnumerable<Toil> MakeNewToils()
        {
            Log.Message($"packing stuff in vehicle as much as can {this.pawn}");
            
            var packStart = Toils_General.Wait(2);
            var packEnd = Toils_General.Wait(2);
            Toil findThingToLoad = FindThingToLoad(packEnd, TargetB.Cell);
            Toil gotoThing = Toils_Goto.Goto(TargetIndex.C, PathEndMode.Touch);
            Toil startHaul = Toils_Haul.StartCarryThing(TargetIndex.C, false, true);
            Toil goToVehicle = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            Toil depositIntoVehicle = DepositIntoVehicle();


            yield return packStart;
            yield return Toils_Jump.JumpIf(packEnd, () => MassUtility.IsOverEncumbered(TargetA.Pawn));
            yield return findThingToLoad;
            yield return gotoThing;
            yield return startHaul;
            yield return goToVehicle;
            yield return CreateTransfereRechord();
            yield return ShowProgressBarToil();
            yield return depositIntoVehicle;
            yield return Toils_Jump.Jump(packStart);
            yield return packEnd;

            yield return Toils_General.Do(() =>
            {
                TargetA.Pawn.GetComp<CompHauledToInventory>().AllPacked = true;
            });
        }

        private Toil ShowProgressBarToil()
        {
            Toil reloadWait = ToilMaker.MakeToil("reload-wait");
            reloadWait.defaultCompleteMode = ToilCompleteMode.Delay;
            reloadWait.defaultDuration = 25;
            reloadWait.WithProgressBarToilDelay(TargetIndex.A);

            return reloadWait;
        }

        private Toil CreateTransfereRechord()
        {
            return Toils_General.Do(() =>
            {
                
                var vehicle = TargetA.Pawn;
                var worker = this.pawn;
                Log.Message($"CREATING RECORD {worker.carryTracker.CarriedThing} {worker.carryTracker.CarriedThing.stackCount}");
                var v = new VehiclePawnProxy(vehicle);
                v.ScheduleItemsToLoad(new List<Thing>() { worker.carryTracker.CarriedThing });

            });
        }

        private static HashSet<Thing> Seen = new();
        public static Thing FindClosestLoadableThingFromLocation(Pawn pawn, IntVec3 center)
        {
            var map = pawn.Map;
            var designationManager = map.designationManager;

            Func<Thing, Pawn, bool> validator = (Thing t, Pawn pawn) =>
            {
                var a= Utils.OkThingToHaul(t, pawn);
#if DEBUG
                Log.Message($"--validator {t}, {pawn} {a}");
#endif
                return Utils.OkThingToHaul(t, pawn);
            };

            var items = new List<Thing>();
            var peMode = PathEndMode.ClosestTouch;
            var traverseParams = TraverseParms.For(pawn);

            Seen.Clear();
            var c = 0;

            Thing closestThing = null;

            Log.Message($"FindClosestLoadableThingFromLocation {pawn} {center}");


            while (Utils.FindClosestThing(
                center,
                pawn.Map,
                pawn,
                Seen,
                15,
                (Thing i) => validator(i, pawn),
                out closestThing)
            )
            {
                c++;
                if (c > 100)
                {
                    Log.Message("TERM SEARCH REACHED.");
                }
#if DEBUG
                Log.Message("----look at: " + closestThing);
#endif
                Seen.Add(closestThing);

                if (!map.reachability.CanReach(center, closestThing, peMode, traverseParams))
                {
                    Log.Message("----no path to raech");
                    continue;
                }

                if (closestThing.def.thingCategories != null
                    && closestThing.def.thingCategories.Where(v => v != null).Any(v => v.defName.Contains("Chunks")))
                {
                    if (designationManager.DesignationOn(closestThing)?.def == DesignationDefOf.Haul
                        || designationManager.DesignationOn(closestThing)?.def == PickUpAndHaulDesignationDefOf.haulUrgently)
                    {
                        /* good item */
                        break;
                    }

                    continue;
                }


                
                break;

                
            }
            if (closestThing!=null)
            {
                Log.Message($"----found closest thing {closestThing}");
            }
            else
            {
                Log.Message("---no closest thing");
            }

            return closestThing;
        }

        private Toil FindThingToLoad(Toil noMoreThingsToLoad, IntVec3 firstDestinationPosition)
        {

            return Toils_General.Do(() =>
            {
                Log.Message("FindThingToLoad");

                var closestThing = FindClosestLoadableThingFromLocation(this.pawn, firstDestinationPosition);
                if (closestThing == null)
                {
                    Log.Message("no more close things found to load");
                    SetNextToil(noMoreThingsToLoad);
                    return;
                }

                this.pawn.Reserve(closestThing, this.job);
                this.job.SetTarget(TargetIndex.C, closestThing);
                this.job.count = closestThing.stackCount;

            });
        }

        private static FieldInfo countToTransferFieldInfo = AccessTools.Field(typeof(TransferableOneWay), "countToTransfer");
        private Toil DepositIntoVehicle()
        {

            return new Toil
            {
                initAction = () =>
                {
                    var vehicle = TargetA.Pawn;
                    var worker = this.pawn;
                    Log.Message($"PLACE IN VEHICLE");
                    var item = worker.carryTracker.CarriedThing;
                    Log.Message($"----item {item}");
                    if (item is null || item.stackCount == 0)
                    {
                        Log.Message($"----incompatible {item}");
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }
                    else
                    {
                        int stackCount = item.stackCount;
                        var v = new VehiclePawnProxy(vehicle);
                        Log.Message("----ADDING TO VEHICLE " + v.Thing + " " + item + " " + stackCount);
                        
                        int result = v.AddOrTransfer(item, stackCount);
                        TransferableOneWay transferable = VehiclePawnProxy.GetTransferable(v.CargoToLoad, item);
                        if (transferable != null)
                        {
                            int count = transferable.CountToTransfer - stackCount;
                            countToTransferFieldInfo.SetValue(transferable, count);
                            if (transferable.CountToTransfer <= 0)
                            {
                                v.CargoToLoad.Remove(transferable);
                            }


                        }
                    }
                }
            };

        }


    }

    
    
    public class Job_PackHauler : Job
    {
        public Job_PackHauler()
        {
            this.count = 1;
            this.def = new JobDef
            {
                driverClass = typeof(JobDriver_PackHauler)
            };
        }
    }


}
