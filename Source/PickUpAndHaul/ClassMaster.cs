using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace PickUpAndHaul
{
    public static class ClassMaster
    {
        public static T GetValueOnInstance<T>(object instance, string memberName)
        {
            Type type = instance != null ? instance.GetType() : typeof(T);  // Handle null instance for static members

            // First, check for properties
            PropertyInfo property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (property != null)
            {
                return (T)property.GetValue(instance);
            }

            // If no property found, check for fields
            FieldInfo field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (field != null)
            {
                return (T)field.GetValue(instance);
            }

            throw new ArgumentException($"No field or property named '{memberName}' found on type {type.Name}");
        }
        public static T GetValueOnInstanceOfType<T>(object instance, string memberName, Type type)
        {
            // First, check for properties
            PropertyInfo property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (property != null)
            {
                return (T)property.GetValue(instance);
            }

            // If no property found, check for fields
            FieldInfo field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (field != null)
            {
                return (T)field.GetValue(instance);
            }

            throw new ArgumentException($"No field or property named '{memberName}' found on type {type.Name}");
        }

        public static T Call<T>(object instance, string methodName, object[] parameters, Type[] findForSignature=null)
        {

            Type type = instance.GetType();

            MethodInfo method;
            if (findForSignature==null)
                method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            else
                method = type.GetMethod(methodName, findForSignature);

            if (method == null)
            {
                throw new ArgumentException($"Method '{methodName}' not found on type {type.Name}");
            }

            if (method.ReturnType == typeof(void) && typeof(T) != typeof(void))
            {
                throw new ArgumentException($"Method '{methodName}' returns void, but the generic type T is not void.");
            }

            if (method.ReturnType != typeof(void) && typeof(T) == typeof(void))
            {
                throw new ArgumentException($"Method '{methodName}' does not return void, but the generic type T is void.");
            }

            try
            {
                object result = method.Invoke(instance, parameters);
                return (T)result;
            }
            catch (TargetParameterCountException)
            {
                throw new ArgumentException($"Parameter count mismatch for method '{methodName}'.");
            }
            catch (ArgumentException e)
            {
                throw new ArgumentException($"Failed to call method '{methodName}': " + e.Message);
            }
        }

        // Overload for methods that don't return anything (void)
        public static void Call(object instance, string methodName, params object[] parameters)
        {
            Call<object>(instance, methodName, parameters);
        }

    }
}
