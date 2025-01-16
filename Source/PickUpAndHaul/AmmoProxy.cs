using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PickUpAndHaul
{
    public class AmmoProxy
    {
        public AmmoProxy(ThingWithComps ammo)
        {
            _ammo = ammo;
        }

        private readonly ThingWithComps _ammo;
        public ThingWithComps Thing { get { return _ammo; } }

        private Assembly[] assemblies => AppDomain.CurrentDomain.GetAssemblies();

        static private Type _ammothingtype = null;
        private Type AmmoThingType
        {
            get
            {
                if (_ammothingtype == null)
                    _ammothingtype = assemblies.SelectMany(v => v.GetTypes()).FirstOrDefault(v => v.Name == "AmmoThing");
                return _ammothingtype;
            }
        }

        static private PropertyInfo _iscookingoff = null;
        private PropertyInfo IsCookingOffPropInfo
        {
            get
            {
                if (_iscookingoff == null)
                    _iscookingoff = AmmoThingType.GetProperty("IsCookingOff");
                return _iscookingoff;
            }
        }

        internal bool IsCookingOff
        {
            get
            {
                return (bool)IsCookingOffPropInfo.GetValue(Thing);
            }
        }
    }
}
