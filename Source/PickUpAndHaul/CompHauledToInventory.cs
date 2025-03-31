using Verse.AI;

namespace PickUpAndHaul;

public class CompHauledToInventory : ThingComp
{
	//private HashSet<Thing> takenToInventory = new();
	private List<ThingDef> defs = new();
	private List<int> counts = new();

	public void Add(Thing thing)
	{
		var existingindex = defs.FindIndex(v => v == thing.def);
		if (existingindex == -1)
		{
			defs.Add(thing.def);
			counts.Add(thing.stackCount);
		}
		else
		{
			counts[existingindex] += thing.stackCount;
		}

		Log.Message($"defs: {string.Join(", ", defs)}");
		Log.Message($"counts: {string.Join(", ", counts)}");
	}
	public void RemoveFirst()
	{
		this.defs.RemoveAt(0);
		this.counts.RemoveAt(0);
	}

    public bool TryGetFirstCount(out int i)
	{
		Log.Message($"----looking at first count in: {string.Join(", ", this.counts)}");
		i = 0;
		if (this.counts.Count>0)
		{
			i = this.counts[0];
			return true;
		}
		return false;
	}
	public bool TryGetFirstDefName(out ThingDef s)
	{
        Log.Message($"----looking at first def in: {string.Join(", ", this.defs)}");
        s = null;
		if (this.defs.Count>0)
		{
			s = this.defs[0];
			return true;
		}
		return false;
	}
	public bool TryDecrementCountBy(int n)
	{
		Log.Message($"----decramenting count by: {n}");
        this.counts[0] -= n;
        if (this.counts[0] < 0)
        {
			return false;
        }

        else if (this.counts[0] == 0)
        {
            /*if no more scheduled deliveries for this item/count might as well remove it now. */
            RemoveFirst();
            Log.Message("----no more deliveries for this item.");
            //job.targetQueueA.RemoveAt(0);
            //job.countQueue.RemoveAt(0);
        }

        Log.Message($"after dec defs: {string.Join(", ", defs)}");
        Log.Message($"after dec counts: {string.Join(", ", counts)}");
        return true;
    }

	public List<ThingDef> CarriedThings => this.defs;


    public bool Contains(Thing thing)
	{
		return this.defs.Any(v => v == thing.def);
	}


    public override void PostExposeData()
	{
		base.PostExposeData();
		Scribe_Collections.Look(ref defs, "comp-hauled-toinv-defs", LookMode.Reference);
        Scribe_Collections.Look(ref counts, "comp-hauled-toinv-counts", LookMode.Reference);

		if (defs == null)
			defs = new();
		if (counts == null)
			counts = new();
    }
}