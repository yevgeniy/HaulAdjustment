using System.Linq;

namespace PickUpAndHaul;
public class JobDriver_HaulToInventory : JobDriver
{
	public override bool TryMakePreToilReservations(bool errorOnFailed)
	{
		Log.Message($"{pawn} starting HaulToInventory job: {job.targetQueueA.ToStringSafeEnumerable()}:{job.countQueue.ToStringSafeEnumerable()}");
		pawn.ReserveAsManyAsPossible(job.targetQueueA, job);
		pawn.ReserveAsManyAsPossible(job.targetQueueB, job);
		return pawn.Reserve(job.targetQueueA[0], job) && pawn.Reserve(job.targetB, job);
	}

	//get next, goto, take, check for more. Branches off to "all over the place"
	public override IEnumerable<Toil> MakeNewToils()
	{
		var takenToInventory = pawn.TryGetComp<CompHauledToInventory>();

		var wait = Toils_General.Wait(2);

		var nextTarget = Toils_JobTransforms.ExtractNextTargetFromQueue(TargetIndex.A); //also does count
		yield return nextTarget;

		yield return CheckForOverencumberedForCombatExtended();

		var gotoThing = new Toil
		{
			initAction = () => pawn.pather.StartPath(TargetThingA, PathEndMode.ClosestTouch),
			defaultCompleteMode = ToilCompleteMode.PatherArrival
		};
		gotoThing.FailOnDespawnedNullOrForbidden(TargetIndex.A);
		yield return gotoThing;

        var makeUnloadJob = new Toil //Queue next job
        {
            initAction = () =>
            {
                var actor = pawn;
                var curJob = actor.jobs.curJob;
                var storeCell = curJob.targetB;

                var unloadJob = JobMaker.MakeJob(PickUpAndHaulJobDefOf.UnloadYourHauledInventory, storeCell);
                if (unloadJob.TryMakePreToilReservations(actor, false))
                {
                    actor.jobs.jobQueue.EnqueueFirst(unloadJob, JobTag.Misc);
                    EndJobWith(JobCondition.Succeeded);
                    //This will technically release the cell reservations in the queue, but what can you do
                }
            }
        };
        var takeThing = new Toil
		{
			initAction = () =>
			{
				var actor = pawn;
				var thing = actor.CurJob.GetTarget(TargetIndex.A).Thing;
				Toils_Haul.ErrorCheckForCarry(actor, thing);

				//get max we can pick up
				var countToPickUp = Mathf.Min(job.count, MassUtility.CountToPickUpUntilOverEncumbered(actor, thing));
				Log.Message($"{actor} is hauling to inventory {thing}:{countToPickUp}");

				if (ModCompatibilityCheck.CombatExtendedIsActive)
				{
					countToPickUp = CompatHelper.CanFitInInventory(pawn, thing);
				}

				if (countToPickUp > 0)
				{
					var splitThing = thing.SplitOff(countToPickUp);
					var shouldMerge = takenToInventory.GetHashSet().Any(x => x.def == thing.def);
					actor.inventory.GetDirectlyHeldThings().TryAdd(splitThing, shouldMerge);
					takenToInventory.RegisterHauledItem(splitThing);

					if (ModCompatibilityCheck.CombatExtendedIsActive)
					{
						CompatHelper.UpdateInventory(pawn);
					}
				}

				//thing still remains, so queue up hauling if we can + end the current job (smooth/instant transition)
				//This will technically release the reservations in the queue, but what can you do
				if (thing.Spawned)
				{
					actor.jobs.curDriver.JumpToToil(makeUnloadJob);
				}
			}
		};
		yield return takeThing;
		yield return Toils_Jump.JumpIf(nextTarget, () => !job.targetQueueA.NullOrEmpty());
		
		yield return makeUnloadJob;
		yield return wait;
	}

	private static List<Thing> TempListForThings { get; } = new();

	/// <summary>
	/// the workgiver checks for encumbered, this is purely extra for CE
	/// </summary>
	/// <returns></returns>
	public Toil CheckForOverencumberedForCombatExtended()
	{
		var toil = new Toil();

		if (!ModCompatibilityCheck.CombatExtendedIsActive)
		{
			return toil;
		}

		toil.initAction = () =>
		{
			var actor = toil.actor;
			var curJob = actor.jobs.curJob;
			var nextThing = curJob.targetA.Thing;

			var ceOverweight = CompatHelper.CeOverweight(pawn);

			if (!(MassUtility.EncumbrancePercent(actor) <= 0.9f && !ceOverweight))
			{
				var haul = HaulAIUtility.HaulToStorageJob(actor, nextThing);
				if (haul?.TryMakePreToilReservations(actor, false) ?? false)
				{
					//note that HaulToStorageJob etc doesn't do opportunistic duplicate hauling for items in valid storage. REEEE
					actor.jobs.jobQueue.EnqueueFirst(haul, JobTag.Misc);
					EndJobWith(JobCondition.Succeeded);
				}
			}
		};

		return toil;
	}
}