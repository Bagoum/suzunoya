using System.Reflection;

namespace BagoumLib.Reflection {
public static class BuilderHelpers {
    public const TypeAttributes InterfaceType =
        TypeAttributes.Interface | TypeAttributes.Public | TypeAttributes.Abstract;
    public const TypeAttributes ClassType =
        TypeAttributes.Class | TypeAttributes.Public;

    //ignoring: FamANDAssem, Family, NewSlot
    public const MethodAttributes InterfaceProperty =
        MethodAttributes.Abstract | MethodAttributes.Virtual |
        MethodAttributes.Public | MethodAttributes.SpecialName |
        MethodAttributes.HideBySig;
    public const MethodAttributes ImplementedProperty =
        MethodAttributes.Public | MethodAttributes.SpecialName |
        MethodAttributes.HideBySig | MethodAttributes.Virtual;

    public const MethodAttributes AbstractMethod =
        MethodAttributes.Abstract | MethodAttributes.Virtual |
        MethodAttributes.Public;
    public const MethodAttributes VirtualMethod =
        MethodAttributes.Virtual | MethodAttributes.Public;

}
}