
using System;
using System.Runtime.InteropServices;
using Stunlock.Network;
using Bloodstone.API;

namespace Bloodstone.Network;

// Helper class that serializes a blittable type.
internal class VBlittableNetworkMessage<T> : VNetworkMessage
where T : unmanaged
{
    public T Value { get; private set; }

    public VBlittableNetworkMessage()
    {
        Value = default;
    }

    public VBlittableNetworkMessage(T instance)
    {
        Value = instance;
    }

    public void Deserialize(NetBufferIn reader)
    {
        var size = reader.ReadInt32();

        if (size != Marshal.SizeOf<T>())
            throw new Exception($"Received a message with invalid size {size} for type {typeof(T)}");

        unsafe
        {
            var bytes = stackalloc byte[size];

            // NetBufferIn.ReadBytes(void*, int) crashes, so we do it manually
            for (var i = 0; i < size; i++)
                bytes[i] = reader.ReadByte();

            Value = Marshal.PtrToStructure<T>(new IntPtr(bytes));
        }
    }

    public void Serialize(ref NetBufferOut writer)
    {
        unsafe
        {
            var size = Marshal.SizeOf<T>();
            var bytes = stackalloc byte[size];
            Marshal.StructureToPtr(Value, new IntPtr(bytes), false);

            // NetBufferOut.WriteBytes(void*, int) crashes, so we do it manually
            writer.Write((int)size);
            for (var i = 0; i < size; i++)
                writer.Write(bytes[i]);
        }
    }
}