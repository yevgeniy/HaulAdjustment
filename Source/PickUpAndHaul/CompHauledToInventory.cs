using System.Linq;
using Verse.AI;

namespace PickUpAndHaul;

[StaticConstructorOnStartup]
public class CompHauledToInventory : ThingComp
{

    static CompHauledToInventory()
    {
        var defs = DefDatabase<ThingDef>.AllDefs;
        foreach (var i in defs)
        {
            i.comps.Add(new CompProperties
            {
                compClass = typeof(CompHauledToInventory)
            });
        }
    }
    private bool hauling;
    public Pawn Vehicle => this.parent as Pawn;

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

    private bool vehicleShouldBeUsedToHaul;
    public bool VehicleShouldBeUsedToHaul
    {
        get
        {
            return this.vehicleShouldBeUsedToHaul;
        }
        set
        {
            this.vehicleShouldBeUsedToHaul = value;
            CritDestinationsMap.UseVehicleToHaul[this] = value;
        }
    }

    private bool vehicleIsBusy;
    public bool VehicleIsBusy
    {
        get
        {
            return this.vehicleIsBusy;
        }
        set
        {
            this.vehicleIsBusy = value;
        }
    }

    private bool allPacked;
    public bool AllPacked
    {
        get
        {
            return this.allPacked;
        }
        set
        {
            this.allPacked = value;
        }
    }

    public void CustomTick()
    {

    }
    public bool HasPotentialWork()
    {

        if (this.parent == null)
        {
            Log.Message($"NO PARENT ON COMP");
            return false;
        }

        if (this.parent is Pawn pawn)
        {
            Log.Message($"---is busy? {VehicleIsBusy}");
            return false == VehicleIsBusy && Utils.TryFindValidToZone(out var _) && Utils.TryFindValidFromZones(out var _);
        }
        
        Log.Message($"NOT A VEHICLE {this.parent}");
        return false;

    }

    public override void PostExposeData()
    {

        base.PostExposeData();
        Scribe_Values.Look(ref hauling, "comp-hauled-toinv-defs");
        Scribe_Values.Look(ref vehicleShouldBeUsedToHaul, "comp-vehicleShouldBeUsedToHaul");
        Scribe_Values.Look(ref vehicleIsBusy, "comp-vehicleIsBusy");
        Scribe_Values.Look(ref allPacked, "comp-allPacked");
        

        if (vehicleShouldBeUsedToHaul)
            CritDestinationsMap.UseVehicleToHaul[this] = vehicleShouldBeUsedToHaul;

    }

}