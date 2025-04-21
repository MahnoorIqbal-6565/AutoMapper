using System.Reflection.Emit;

namespace AutoMapper.Execution;

// Removed ProxyBase as it's specific to the Proxy DP
// public abstract class ProxyBase
// {
//     public ProxyBase() { }
//     protected void NotifyPropertyChanged(PropertyChangedEventHandler handler, string method) => handler?.Invoke(this, new(method));
// }

public readonly record struct TypeDescription(Type Type, PropertyDescription[] AdditionalProperties)
{
    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(Type);
        foreach (var property in AdditionalProperties)
        {
            hashCode.Add(property);
        }
        return hashCode.ToHashCode();
    }
    public bool Equals(TypeDescription other) => Type == other.Type && AdditionalProperties.SequenceEqual(other.AdditionalProperties);
}

[DebuggerDisplay("{Name}-{Type.Name}")]
public readonly record struct PropertyDescription(string Name, Type Type, bool CanWrite = true)
{
    public PropertyDescription(PropertyInfo property) : this(property.Name, property.PropertyType, property.CanWrite) { }
}

public static class DynamicTypeFactory // Renamed from ProxyGenerator to reflect general dynamic type creation
{
    private static readonly ModuleBuilder DynamicModule = CreateDynamicModule();
    private static ModuleBuilder CreateDynamicModule()
    {
        var assemblyName = new AssemblyName("AutoMapper.DynamicTypes.emit");
        var builder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        return builder.DefineDynamicModule(assemblyName.Name);
    }

    public static Type CreateType(string typeName, Type baseType, Type[] interfaces, IEnumerable<PropertyDescription> properties)
    {
        TypeBuilder typeBuilder = DynamicModule.DefineType(typeName,
            TypeAttributes.Class | TypeAttributes.Public | TypeAttributes.Sealed, baseType, interfaces);

        // Define default constructor
        var constructorBuilder = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);
        var ctorIl = constructorBuilder.GetILGenerator();
        ctorIl.Emit(OpCodes.Ldarg_0);
        ctorIl.Emit(OpCodes.Call, baseType?.GetConstructor(Type.EmptyTypes) ?? typeof(object).GetConstructor(Type.EmptyTypes));
        ctorIl.Emit(OpCodes.Ret);

        foreach (var property in properties)
        {
            CreateProperty(typeBuilder, property);
        }

        return typeBuilder.CreateTypeInfo().AsType();
    }

    private static void CreateProperty(TypeBuilder typeBuilder, PropertyDescription property)
    {
        var fieldBuilder = typeBuilder.DefineField($"<{property.Name}>", property.Type, FieldAttributes.Private);
        var propertyBuilder = typeBuilder.DefineProperty(property.Name, PropertyAttributes.None, property.Type, null);

        // Define getter
        var getterBuilder = typeBuilder.DefineMethod($"get_{property.Name}",
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
            property.Type, Type.EmptyTypes);
        var getterIl = getterBuilder.GetILGenerator();
        getterIl.Emit(OpCodes.Ldarg_0);
        getterIl.Emit(OpCodes.Ldfld, fieldBuilder);
        getterIl.Emit(OpCodes.Ret);
        propertyBuilder.SetGetMethod(getterBuilder);

        // Define setter if CanWrite is true
        if (property.CanWrite)
        {
            var setterBuilder = typeBuilder.DefineMethod($"set_{property.Name}",
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
                typeof(void), new[] { property.Type });
            var setterIl = setterBuilder.GetILGenerator();
            setterIl.Emit(OpCodes.Ldarg_0);
            setterIl.Emit(OpCodes.Ldarg_1);
            setterIl.Emit(OpCodes.Stfld, fieldBuilder);
            setterIl.Emit(OpCodes.Ret);
            propertyBuilder.SetSetMethod(setterBuilder);
        }
    }

    // Removed methods related to Proxy-specific concerns like caching by TypeDescription and INotifyPropertyChanged handling
    // public static Type GetProxyType(Type interfaceType) => ProxyTypes.GetOrAdd(new(interfaceType, []));
    // public static Type GetSimilarType(Type sourceType, IEnumerable<PropertyDescription> additionalProperties) =>
    //     ProxyTypes.GetOrAdd(new(sourceType, [..additionalProperties.OrderBy(p => p.Name)]));
}