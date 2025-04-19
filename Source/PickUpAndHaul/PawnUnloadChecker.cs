namespace PickUpAndHaul;
public class PawnUnloadChecker
{
	public static void CheckIfPawnShouldUnloadInventory(Pawn pawn, bool forced = false)
	{
		/*TODO*/
		//var job = JobMaker.MakeJob(PickUpAndHaulJobDefOf.UnloadYourHauledInventory, pawn);
		//var haulComp = pawn?.GetHaulInventoryComp();

		//if (haulComp == null)
		//{
		//	return;
		//}


		//var carriedThing = haulComp.CarriedThings;

		//if (pawn.Faction != Faction.OfPlayerSilentFail || !Settings.IsAllowedRace(pawn.RaceProps)
		//	|| carriedThing == null || carriedThing.Count == 0
		//	|| pawn.inventory.innerContainer is not { } inventoryContainer || inventoryContainer.Count == 0)
		//{
		//	return;
		//}

  //      pawn.jobs.jobQueue.EnqueueFirst(job, JobTag.Misc);
	}
}

[DefOf]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Has to match defName")]
public static class PickUpAndHaulJobDefOf
{
	public static JobDef UnloadYourHauledInventory;
	public static JobDef HaulAdj_VehicleHaul_Job;

	public static ThingDef HaulAdj_VehicleHaul_Spot;


    /*TODO: depricated*/
    public static JobDef HaulToInventory;
}