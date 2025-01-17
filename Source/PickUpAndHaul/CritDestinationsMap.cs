using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityStandardAssets.ImageEffects;
using Verse;

namespace PickUpAndHaul
{
    [StaticConstructorOnStartup]
    public class CritDestinationsMap : MapComponent
    {

        //IConstructible c
        public static HashSet<Thing> Constructables = new HashSet<Thing>();
        public static HashSet<ThingWithComps> Guns = new HashSet<ThingWithComps>();

        public static StatDef ReloadSpeed = null;

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



        public override void MapComponentTick()
        {
            base.MapComponentTick();

            if (Find.TickManager.TicksGame % 2000 == 0)
            {
                Log.Message("LENGTH OF CONSTRUCTABLES: " + Constructables.Count());
                Constructables.RemoveWhere(v => v == null || !v.Spawned);

                Log.Message("LENGTH OF GUNS: " + Guns.Count());
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
        }

        public static IHaulDestination GetMatchingGun(Pawn pawn, Thing thing)
        {

            foreach (var i in Guns.ToList())
            {
                //Log.Message("CONSIDERING GUN TO RELOAD: " + i);
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
                
                if (ammoDef.defName != thing.def.defName)
                {
                    continue;
                }

                int howMuchNeededForFullReload = gun.TotalMagCount - gun.CurrentMagCount;

                if (howMuchNeededForFullReload == 0)
                {
                    /*this gun is already fully loaded*/
                    //Log.Message("REMOVING GUN SINCE IT HAS FULL AMMO: " + i);
                    Guns.Remove(i);
                    continue;
                }

                return new ThingHaulDestination(i)
                {
                    CountNeeded = howMuchNeededForFullReload,
                    ProgressBarDelay = pawn != null ? Mathf.CeilToInt(gun.ReloadTime.SecondsToTicks() / pawn.GetStatValue(ReloadSpeed)) : null
                };
            }

            return null;
        }


        public static IHaulDestination GetLocationOfMatchingConstructable(Pawn pawn, Thing thing)
        {
            
            foreach (var i in Constructables)
            {
                if (i == null)
                    continue;


                if (!i.Spawned)
                {
                    continue;
                }

                if (pawn!=null && i.Faction != pawn.Faction)
                {
                    continue;
                }

                var project = i as IConstructible;

                if (project == null)
                    continue;

                var numberOfThisThingNeeded = project.ThingCountNeeded(thing.def);
                if (numberOfThisThingNeeded > 0)
                {
                    return new ThingHaulDestination(i);
                }
            }

            return null;
        }

    }


    public class ThingHaulDestination : IHaulDestination
    {
        private Thing _thing;

        public ThingHaulDestination(Thing t)
        {
            _thing = t;
        }

        public Thing Thing => _thing;
        public int? CountNeeded = null;
        public int? ProgressBarDelay = null;

        public static explicit operator Thing(ThingHaulDestination haulableDestination)
        {
            return haulableDestination.Thing;
        }

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
    }

    public class HaulThingOwner : ThingOwner
    {
        public HaulThingOwner(Thing t)
        {
            Thing = t;
        }

        public override int Count => throw new NotImplementedException();

        public Thing Thing { get; set; }

        public override int GetCountCanAccept(Thing item, bool canMergeWithExistingStacks = true)
        {
            var project = Thing as IConstructible;

            var numberOfThisThingNeeded = project.ThingCountNeeded(item.def);
            return numberOfThisThingNeeded;

        }

        public override int IndexOf(Thing item)
        {
            throw new NotImplementedException();
        }

        public override bool Remove(Thing item)
        {
            throw new NotImplementedException();
        }

        public override int TryAdd(Thing item, int count, bool canMergeWithExistingStacks = true)
        {
            throw new NotImplementedException();
        }

        public override bool TryAdd(Thing item, bool canMergeWithExistingStacks = true)
        {
            throw new NotImplementedException();
        }

        public override Thing GetAt(int index)
        {
            throw new NotImplementedException();
        }
    }


    public class GunThingHaulDestination : ThingHaulDestination
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
        public GunThingOwner(Thing t) : base(t)
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
}
