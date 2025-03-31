using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityStandardAssets.ImageEffects;
using Verse;
using Verse.AI;

namespace PickUpAndHaul
{
    [StaticConstructorOnStartup]
    public class CritDestinationsMap : MapComponent
    {

        //IConstructible c
        public static HashSet<Thing> Constructables = new HashSet<Thing>();
        public static HashSet<ThingWithComps> Guns = new HashSet<ThingWithComps>();

        public static StatDef ReloadSpeed = null;


        /*We need to keep track how many things are being hauled to
         * vehicle to make propper count of still needed thigns */
        public static Dictionary<Job, Dictionary<Pawn/*vehicle*/, Dictionary<Def, int>>> ToVehicleHauling = new ();


        static CritDestinationsMap()
        {
            

            if (ModCompatibilityCheck.CombatExtendedIsActive)
            {
                ReloadSpeed = StatDef.Named("ReloadSpeed");
            }


        }

        public CritDestinationsMap(Map map) : base(map)
        {

        }

        public static void AddVehicleHaul(Job job, Pawn vehicle, Thing item, int count)
        {
            Log.Message($"hauling to vehicle {vehicle} {item} {count}");

            if (!ToVehicleHauling.ContainsKey(job))
            {
                ToVehicleHauling[job] = new Dictionary<Pawn, Dictionary<Def, int>>();
            }

            if (!ToVehicleHauling[job].ContainsKey(vehicle))
            {
                ToVehicleHauling[job][vehicle] = new Dictionary<Def, int>();
            }

            if (!ToVehicleHauling[job][vehicle].ContainsKey(item.def))
            {
                ToVehicleHauling[job][vehicle][item.def] = 0;
            }

            ToVehicleHauling[job][vehicle][item.def] += count;

            
        }
        private static int GetAlreadyHauling(Pawn vehicle, Def itemDef)
        {
            int count = 0;
            foreach (var (job, byVehicle) in ToVehicleHauling)
            {
                
                if (byVehicle.TryGetValue(vehicle, out var byItem))
                {
                    if (byItem.TryGetValue(itemDef, out var c))
                    {
                        count += c;
                    }
                }

            }
            return count;

        }
        public static void JustLoaded(Job job, Pawn vehicle, Thing item, int count)
        {
            if (!ToVehicleHauling.TryGetValue(job, out var byvehicle))
            {
                Log.Message("COULD NOT FIND job-vehicle");
            }
            if (!byvehicle.TryGetValue(vehicle, out var byitem))
            {
                Log.Message("COULD NOT FIND vehicle-item");
            }
            if (!byitem.TryGetValue(item.def, out var c) )
            {
                Log.Message("COUNT NOT FIND item-count");
            }
            byitem[item.def] -= count;
        }
        public static void RemoveVehicleHaul(Job job)
        {
            Log.Message($"removing hauling record");

            ToVehicleHauling.Remove(job);
        }



        public override void MapComponentTick()
        {
            base.MapComponentTick();

            if (Find.TickManager.TicksGame % 2000 == 0)
            {
                Constructables.RemoveWhere(v => v == null || !v.Spawned);


                Guns.RemoveWhere(v => v == null || !v.Spawned);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Collections.Look(ref Constructables, "nimm-crit-constructables", LookMode.Reference);
            Scribe_Collections.Look(ref Guns, "nimm-crit-guns", LookMode.Reference);
            


            if (Constructables == null)
                Constructables = new HashSet<Thing>();

            if (Guns == null)
                Guns = new HashSet<ThingWithComps>();

            if (ToVehicleHauling == null)
                ToVehicleHauling = new Dictionary<Job, Dictionary<Pawn, Dictionary<Def, int>>>();
        }

        public static IHaulDestination GetMatchingVehiclePackagingForItem(Pawn pawn, Thing thing)
        {
            if (pawn == null)
                return null;

            var listers = Utils.GetVehiclePackingListers(pawn.Map);

            if (listers == null)
                return null;


            foreach (var i in listers)
            {
                var vehicle = new VehiclePawnProxy(i as Pawn);

                if (pawn != null && pawn.Faction != vehicle.Faction)
                {
                    continue;
                }

                if (vehicle.CanAccept(thing, out var howMuch))
                {
                    Log.Message($"Vehicle {i} can accept: {thing} of amount: {howMuch}");
                    
                    var ah= GetAlreadyHauling(i as Pawn, thing.def);
                    Log.Message($"already houling {ah}");

                    howMuch -= ah;

                    if (howMuch > 0)
                        return new CriticalThingHaulDestination(i as Thing)
                        {
                            CountNeeded = howMuch,
                            ProgressBarDelay = 25,
                        };
                }

            }

            return null;
        }



        public static IHaulDestination GetMatchingGunForAmmo(Pawn pawn, Thing thing)
        {
            if (pawn == null)
                return null;


            Log.Message("GUNS: " + Guns.Count);
            foreach (var i in Guns.ToList())
            {
                if (i == null)
                    continue;

                if (!i.Spawned)
                {
                    continue;
                }

                var gun = new GunProxy(i);

                var ammoDef = gun.CurrentAmmo;
                if (ammoDef == null)
                {
                    return null;
                }

                Log.Message("AMMO: " + string.Join(", ", ammoDef.thingCategories.Select(v => v.defName)));

                if (ammoDef.defName != thing.def.defName)
                {
                    continue;
                }

                int howMuchNeededForFullReload = gun.TotalMagCount - gun.CurrentMagCount;

                if (howMuchNeededForFullReload == 0)
                {
                    Guns.Remove(i);
                    continue;
                }

                return new GunThingHaulDestination(i)
                {
                    CountNeeded = howMuchNeededForFullReload,
                    ProgressBarDelay = pawn != null ? Mathf.CeilToInt(gun.ReloadTime.SecondsToTicks() / pawn.GetStatValue(ReloadSpeed)) : null
                };
            }

            return null;
        }


        public static IHaulDestination GetMatchingConstructableForMaterial(Pawn pawn, Thing thing)
        {
            if (pawn == null)
                return null;

            Log.Message("CONSTRUCTS: " + Constructables.Count);
            foreach (var i in Constructables)
            {
                if (i == null)
                    continue;


                if (!i.Spawned)
                {
                    continue;
                }

                if (pawn != null && i.Faction != pawn.Faction)
                {
                    continue;
                }

                var project = i as IConstructible;

                if (project == null)
                    continue;

                
                var numberOfThisThingNeeded = project.ThingCountNeeded(thing.def);
                

                if (numberOfThisThingNeeded > 0)
                {

                    Log.Message($"CONSTURCTABLE FOUND: {project} {(project as Thing).def.defName} for: {thing} {numberOfThisThingNeeded}");
                    return new CriticalThingHaulDestination(i)
                    {
                        CountNeeded = numberOfThisThingNeeded
                    };
                }
            }

            return null;
        }

        
    }


    public class CriticalThingHaulDestination : ThingWithComps, IHaulDestination, IThingHolder
    {
        private Thing _thing;

        public CriticalThingHaulDestination(Thing t)
        {
            _thing = t;
        }

        public Thing Thing => _thing;
        public int? CountNeeded = null;
        public int? ProgressBarDelay = null;


        public IntVec3 Position => Thing.Position;

        public Map Map => Thing.Map;

        public bool StorageTabVisible => true;

        public virtual bool Accepts(Thing t)
        {
            var project = Thing as IConstructible;

            if (project == null)
                return false;

            var numberOfThisThingNeeded = project.ThingCountNeeded(t.def);
            if (numberOfThisThingNeeded > 0)
            {
                return true;
            }

            return false;
        }

        public StorageSettings GetParentStoreSettings()
        {
            return null;
        }

        public StorageSettings GetStoreSettings()
        {
            return new StorageSettings
            {
                Priority = StoragePriority.Critical
            };

        }
        
        public void Notify_SettingsChanged()
        {

        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            if (Thing is IThingHolder thingHolder)
            {
                thingHolder.GetChildHolders(outChildren);
            }
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            if (Thing is IThingHolder thingHolder)
            {
                return thingHolder.GetDirectlyHeldThings();
            }
            return null;
        }
    }

    public class HaulThingOwner : ThingOwner
    {
        public HaulThingOwner(Thing t, ThingOwner orig)
        {
            Thing = t;
            Original = orig;
        }

        public override int Count
        {
            get
            {
                return Original.Count;
            }
        }

        public Thing Thing { get; set; }
        public ThingOwner Original { get; }

        public override int GetCountCanAccept(Thing item, bool canMergeWithExistingStacks = true)
        {
            var project = Thing as IConstructible;

            if (project is Blueprint_Install)
                return 1;

            var numberOfThisThingNeeded = project.ThingCountNeeded(item.def);
            return numberOfThisThingNeeded;

        }

        public override int IndexOf(Thing item)
        {
            return Original.IndexOf(item);
        }

        public override bool Remove(Thing item)
        {
            return Original.Remove(item);
        }

        public override int TryAdd(Thing item, int count, bool canMergeWithExistingStacks = true)
        {
            return Original.TryAdd(item, count, canMergeWithExistingStacks);
        }

        public override bool TryAdd(Thing item, bool canMergeWithExistingStacks = true)
        {
            return Original.TryAdd(item, canMergeWithExistingStacks);
        }

        public override Thing GetAt(int index)
        {
            return Original.GetAt(index);
        }
    }


    public class GunThingHaulDestination : CriticalThingHaulDestination
    {
        public GunThingHaulDestination(Thing t) : base(t)
        {
        }

        public override bool Accepts(Thing t)
        {
            var gun = new GunProxy(this.Thing as ThingWithComps);

            var ammoDef = gun.CurrentAmmo;
            if (ammoDef.defName != t.def.defName)
                return false;

            int howMuchNeededForFullReload = gun.TotalMagCount - gun.CurrentMagCount;

            return howMuchNeededForFullReload > 0;

        }
    }

    public class GunThingOwner : HaulThingOwner
    {
        public GunThingOwner(Thing t, ThingOwner orig) : base(t, orig)
        {
        }

        public Map Map => Thing.Map;

        public override int GetCountCanAccept(Thing item, bool canMergeWithExistingStacks = true)
        {


            var r = this.SpaceRemainingFor(item.def);

            return r;

        }

        public string GetUniqueLoadID()
        {
            throw new NotImplementedException();
        }

        private int? _countCanAccept = null;
        public int SpaceRemainingFor(ThingDef stuff)
        {
            //Log.Message("GET COUNT CAN ACCEPT: " + stuff.defName);
            if (_countCanAccept != null)
            {
                //  Log.Message(_countCanAccept.Value + " (cached)");
                return _countCanAccept.Value;
            }


            var gun = new GunProxy(this.Thing as ThingWithComps);

            var ammoDef = gun.CurrentAmmo;

            if (ammoDef.defName != stuff.defName)
                return 0;

            int howMuchNeededForFullReload = gun.TotalMagCount - gun.CurrentMagCount;
            _countCanAccept = howMuchNeededForFullReload;
            Log.Message(howMuchNeededForFullReload.ToString());

            return howMuchNeededForFullReload;
        }

        public override int TryAdd(Thing item, int count, bool canMergeWithExistingStacks = true)
        {
            //Log.Message("TRY ADDING: " + item + " " + count);
            var gun = new GunProxy(this.Thing as ThingWithComps);

            int currentMag = gun.CurrentMagCount;
            //Log.Message("CURRENT MAG COUNT: " + currentMag);

            int total = gun.TotalMagCount;
            //Log.Message("TOTAL MAG COUNT: " + total);

            var needed = total - currentMag;
            //Log.Message("NEEDED: " + needed);

            var isOneAtATime = gun.OneAtATimeReload;
            //Log.Message("ONE AT A TIME: " + isOneAtATime);

            int toAdd = isOneAtATime ? Mathf.Min(needed, count, 1) : Mathf.Min(needed, count);

            gun.AddAmmo(toAdd);

            return toAdd;
        }

        public override bool TryAdd(Thing item, bool canMergeWithExistingStacks = true)
        {
            return this.TryAdd(item, item.stackCount, canMergeWithExistingStacks) > 0;
        }
    }

    public class VehicleThingHaulDestination : CriticalThingHaulDestination
    {
        public VehicleThingHaulDestination(Thing t) : base(t)
        {
        }

        public override bool Accepts(Thing t)
        {
            Log.Message("ACCEPT? " + t + " for: " + Thing);

            var vehicle = new VehiclePawnProxy(Thing as Pawn);

            if (vehicle.CanAccept(t, out var _))
            {
                Log.Message("YES");
                return true;
            }
            return false;
        }
    }
    public class VehicleThingOwner : HaulThingOwner
    {
        public VehicleThingOwner(Thing t, ThingOwner owner) : base(t, owner)
        {
        }

        public Map Map => Thing.Map;

        public override int GetCountCanAccept(Thing item, bool canMergeWithExistingStacks = true)
        {
            Log.Message("GET COUNT CAN ACCET: " + item + " for: " + Thing);
            var vehicle = new VehiclePawnProxy(Thing as Pawn);

            if (vehicle.CanAccept(item, out var howMuch))
            {
                Log.Message(howMuch.Value.ToString());
                return howMuch.Value;
            }
            return 0;

        }

        public override int TryAdd(Thing item, int count, bool canMergeWithExistingStacks = true)
        {
            /*TODO: not used? */

            Log.Message("TRY ADDING: " + item + " " + count);
            var vehicle = new VehiclePawnProxy(Thing as Pawn);

            int result = vehicle.AddOrTransfer(item, count);

            return result;
        }

        public override bool TryAdd(Thing item, bool canMergeWithExistingStacks = true)
        {
            /*TODO: not used? */

            return this.TryAdd(item, item.stackCount, canMergeWithExistingStacks) > 0;
        }
    }
}
