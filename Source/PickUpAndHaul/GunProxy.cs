using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PickUpAndHaul
{
    public class GunProxy
    {
        public GunProxy(ThingWithComps gun)
        {
            _gun = gun;
        }

        private readonly ThingWithComps _gun;
        public ThingWithComps Thing { get { return _gun; } }
        private Assembly[] assemblies => AppDomain.CurrentDomain.GetAssemblies();

        static Type _compammousertype;
        Type CompAmmoUserType
        {
            get
            {
                if (_compammousertype == null)
                    _compammousertype = assemblies.SelectMany(v => v.GetTypes()).FirstOrDefault(v => v.Name == "CompAmmoUser");
                return _compammousertype;
            }
        }
        ThingComp _compammouser;
        public ThingComp CompAmmoUser
        {
            get
            {
                if (_compammouser == null)
                {
                    var methinfo = typeof(ThingWithComps).GetMethod("GetComp");
                    var genMethod = methinfo.MakeGenericMethod(CompAmmoUserType);
                    var comp = genMethod.Invoke(Thing, null);
                    _compammouser = comp as ThingComp;
                }
                return _compammouser;
            }
        }

        static FieldInfo _reloadtimefieldinfo;
        FieldInfo ReloadTimeFieldInfo
        {
            get
            {
                if (_reloadtimefieldinfo == null)
                    _reloadtimefieldinfo = CompProperties_AmmoUserType.GetField("reloadTime", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                return _reloadtimefieldinfo;
            }
        }
        public float ReloadTime
        {
            get
            {
                var props = PropsPropInfo.GetValue(CompAmmoUser);
                return (float)ReloadTimeFieldInfo.GetValue(props);
            }
        }

        static PropertyInfo _currentammopropinfo;
        PropertyInfo CurrentAmmoPropInfo
        {
            get
            {
                if (_currentammopropinfo == null)
                    _currentammopropinfo = CompAmmoUserType.GetProperty("CurrentAmmo");
                return _currentammopropinfo;
            }
        }
        public ThingDef CurrentAmmo
        {
            get
            {
                var currentAmmo = (ThingDef)CurrentAmmoPropInfo.GetValue(CompAmmoUser);
                return currentAmmo;
            }
        }

        static PropertyInfo _curmagcountpropinfo;
        PropertyInfo CurMagCountPropInfo
        {
            get
            {
                if (_curmagcountpropinfo == null)
                    _curmagcountpropinfo = CompAmmoUserType.GetProperty("CurMagCount");
                return _curmagcountpropinfo;
            }
        }

        public int CurrentMagCount
        {
            get
            {
                return (int)CurMagCountPropInfo.GetValue(CompAmmoUser);
            }
        }
        public void AddAmmo(int add)
        {
            var current = CurrentMagCount;
            CurMagCountPropInfo.SetValue(CompAmmoUser, Mathf.Min(TotalMagCount, current + add));
        }

        static PropertyInfo _propspropinfo;
        PropertyInfo PropsPropInfo
        {
            get
            {
                if (_propspropinfo == null)
                    _propspropinfo = CompAmmoUserType.GetProperty("Props");
                return _propspropinfo;
            }
        }

        static Type _compproperties_ammousertype = null;
        Type CompProperties_AmmoUserType
        {
            get
            {
                if (_compproperties_ammousertype == null)
                    _compproperties_ammousertype = assemblies.SelectMany(v => v.GetTypes()).FirstOrDefault(v => v.Name == "CompProperties_AmmoUser");
                return _compproperties_ammousertype;
            }
        }

        static FieldInfo _magazinesizefieldinfo;
        FieldInfo MagazineSizeFieldInfo
        {
            get
            {
                if (_magazinesizefieldinfo == null)
                    _magazinesizefieldinfo = CompProperties_AmmoUserType.GetField("magazineSize", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                return _magazinesizefieldinfo;
            }
        }
        public int TotalMagCount
        {
            get
            {
                var props = PropsPropInfo.GetValue(CompAmmoUser);
                return (int)MagazineSizeFieldInfo.GetValue(props);
            }
        }

        static FieldInfo _reloadoneatatimefieldinfo = null;
        FieldInfo ReloadOneAtATimeFieldInfo
        {
            get
            {
                if (_reloadoneatatimefieldinfo == null)
                    _reloadoneatatimefieldinfo = CompProperties_AmmoUserType.GetField("reloadOneAtATime", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                return _reloadoneatatimefieldinfo;
            }
        }
        public bool OneAtATimeReload
        {
            get
            {
                var props = PropsPropInfo.GetValue(CompAmmoUser);
                return (bool)ReloadOneAtATimeFieldInfo.GetValue(props);
            }
        }

        public SoundDef SoundInteract
        {
            get
            {
                return CompAmmoUser.parent.def.soundInteract;
            }
        }

        static PropertyInfo _hasmagazinepropinfo;
        PropertyInfo HasMagazinePropInfo
        {
            get
            {
                if (_hasmagazinepropinfo == null)
                    _hasmagazinepropinfo = CompAmmoUserType.GetProperty("HasMagazine");
                return _hasmagazinepropinfo;
            }
        }
        public bool HasMagazine
        {
            get
            {
                var r = (bool)HasMagazinePropInfo.GetValue(CompAmmoUser);
                return r;
            }
        }
    }
}
