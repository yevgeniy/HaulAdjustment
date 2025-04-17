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

        public static T Call<T>(object instance, string methodName, object[] parameters = null, Type[] findForSignature = null, bool hasReturnValue = true)
        {

            Type type = instance.GetType();

            MethodInfo method;
            if (findForSignature == null)
                method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            else
                method = type.GetMethod(methodName, findForSignature);

            if (method == null)
            {
                throw new ArgumentException($"Method '{methodName}' not found on type {type.Name}");
            }

            try
            {
                if (!hasReturnValue)
                {
                    method.Invoke(instance, parameters ?? new object[] { });
                    return (T)new System.Object();
                }
                else
                {
                    object result = method.Invoke(instance, parameters ?? new object[] { });
                    return (T)result;
                }

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
        public static void Call(object instance, string methodName, object[] parameters = null, Type[] findForSignature = null)
        {
            Call<object>(instance, methodName, parameters, findForSignature, hasReturnValue: false);
        }

        public static void CallStatic(string className, string methodName, object[] parameters = null, Type[] findForSignature = null)
        {
            CallStatic<object>(className, methodName, parameters, findForSignature, hasReturnValue: false);
        }

        public static T CallStatic<T>(string className, string methodName, object[] parameters = null, Type[] findForSignature = null, bool hasReturnValue = true)
        {
            // Find the type by its name
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.Name == className);

            if (type == null)
            {
                throw new ArgumentException($"Class {className} not found.");
            }

            MethodInfo method;
            if (findForSignature == null)
            {
                method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            }
            else
            {
                method = type.GetMethod(methodName, findForSignature);
            }

            if (method == null)
            {
                throw new ArgumentException($"Method {methodName} not found in class {className}.");
            }


            // Invoke the method
            try
            {
                if (hasReturnValue)
                {
                    return (T)method.Invoke(null, parameters);
                }
                else
                {
                    method.Invoke(null, parameters);
                    return (T)new System.Object();
                }

            }
            catch (InvalidCastException)
            {
                throw new ArgumentException($"The method {methodName} does not return a type compatible with {typeof(T).Name}.");
            }
            catch (TargetInvocationException tie)
            {
                // Wrap the inner exception to provide more context
                throw new Exception($"Error invoking {className}.{methodName}", tie.InnerException);
            }
        }

        public static void SetValue(object instance, string nameOfPropOrField, object value)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            Type type = instance.GetType();

            // Try to get the field first
            FieldInfo fieldInfo = type.GetField(nameOfPropOrField, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (fieldInfo != null)
            {
                fieldInfo.SetValue(instance, value);
                return;
            }

            // If not a field, try to get the property
            PropertyInfo propertyInfo = type.GetProperty(nameOfPropOrField, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (propertyInfo != null)
            {
                if (!propertyInfo.CanWrite)
                {
                    throw new ArgumentException($"The property '{nameOfPropOrField}' does not have a setter.");
                }
                propertyInfo.SetValue(instance, value);
                return;
            }

            throw new ArgumentException($"No field or property named '{nameOfPropOrField}' was found on the type '{type.Name}'.");
        }
        public static object GetValue(object instance, string nameOfPropOrField)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            Type type = instance.GetType();

            // Try to get the field first
            FieldInfo fieldInfo = type.GetField(nameOfPropOrField, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (fieldInfo != null)
            {
                return fieldInfo.GetValue(instance);
            }

            // If not a field, try to get the property
            PropertyInfo propertyInfo = type.GetProperty(nameOfPropOrField, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (propertyInfo != null)
            {
                if (!propertyInfo.CanRead)
                {
                    throw new ArgumentException($"The property '{nameOfPropOrField}' does not have a getter.");
                }
                return propertyInfo.GetValue(instance);
            }

            throw new ArgumentException($"No field or property named '{nameOfPropOrField}' was found on the type '{type.Name}'.");
        }

    }

}
