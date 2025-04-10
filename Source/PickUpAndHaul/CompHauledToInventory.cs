using Verse.AI;

namespace PickUpAndHaul;

[StaticConstructorOnStartup]
public class CompHauledToInventory : ThingComp
{

	static CompHauledToInventory()
	{
		var defs = DefDatabase<ThingDef>.AllDefs;
		foreach(var i in defs)
		{
			i.comps.Add(new CompProperties
			{
				compClass = typeof(CompHauledToInventory)
			});
		}
	}
    private bool hauling;
    
    public bool Hauling
	{
		get
		{
			return this.hauling;
		}
		set
		{
			this.hauling = value;
		}
	}

    public override void PostExposeData()
	{

		base.PostExposeData();
		Scribe_Values.Look(ref hauling, "comp-hauled-toinv-defs");
  //      Scribe_Collections.Look(ref counts, "comp-hauled-toinv-counts", LookMode.Reference);

    }

}