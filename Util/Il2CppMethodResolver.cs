using System;
using System.IO;
using System.Reflection;
using Iced.Intel;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime.Runtime;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.MethodInfo;

namespace Bloodstone.Util;

/// Code stolen/adapted from code by Kasuromi.
/// Harmony currently resolves the wrong method pointer for structs with
/// instance methods, since il2cpp generates an unboxing trampoline for them.
///
/// We on-demand disassemble the method and find the correct method pointer.
public class Il2CppMethodResolver
{
    private static ulong ExtractTargetAddress(in Instruction instruction)
    {
        return instruction.Op0Kind switch
        {
            OpKind.FarBranch16 => instruction.FarBranch16,
            OpKind.FarBranch32 => instruction.FarBranch32,
            _ => instruction.NearBranchTarget,
        };
    }

    private static unsafe IntPtr ResolveMethodPointer(IntPtr methodPointer)
    {
        var stream = new UnmanagedMemoryStream((byte*)methodPointer, 256, 256, FileAccess.Read);
        var codeReader = new StreamCodeReader(stream);

        var decoder = Decoder.Create(IntPtr.Size == 8 ? 64 : 32, codeReader);
        decoder.IP = (ulong)methodPointer.ToInt64();

        Instruction instr = default;
        while (instr.Mnemonic != Mnemonic.Int3)
        {
            decoder.Decode(out instr);

            if (instr.Mnemonic != Mnemonic.Jmp && instr.Mnemonic != Mnemonic.Add)
            {
                return methodPointer;
            }

            if (instr.Mnemonic == Mnemonic.Add)
            {
                if (instr.Immediate32 != 0x10)
                {
                    return methodPointer;
                }
            }

            if (instr.Mnemonic == Mnemonic.Jmp)
                return new IntPtr((long)ExtractTargetAddress(instr));
        }

        return methodPointer;
    }

    public static unsafe IntPtr ResolveFromMethodInfo(INativeMethodInfoStruct methodInfo)
    {
        return ResolveMethodPointer(methodInfo.MethodPointer);
    }

    public static unsafe IntPtr ResolveFromMethodInfo(MethodInfo method)
    {
        var methodInfoField = Il2CppInteropUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(method);
        if (methodInfoField == null)
            throw new Exception($"Couldn't obtain method info for {method}");

        var il2cppMethod = UnityVersionHandler.Wrap((Il2CppMethodInfo*)(IntPtr)(methodInfoField.GetValue(null) ?? IntPtr.Zero));
        if (il2cppMethod == null)
            throw new Exception($"Method info for {method} is invalid");

        return ResolveFromMethodInfo(il2cppMethod);
    }
}