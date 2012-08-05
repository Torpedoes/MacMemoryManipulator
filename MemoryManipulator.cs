using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MacMemoryManipulator
{
    public partial class MemoryManipulator
    {
        protected uint _self;

        private int _id;
        /// <summary>
        /// Gets or sets the process id of the target process.
        /// </summary>
        /// <value>
        /// The process id of the target process.
        /// </value>
        public int Id
        {
            get { return _id; }
            set {
                _id = value;
                if (0 != task_for_pid(_self, Id, out _task)) {
                    throw new Exception("Task for the target process couldn't be received.");
                }
            }
        }

        protected uint _task;
        /// <summary>
        /// Gets the task id of the target process.
        /// </summary>
        /// <value>
        /// The task id of the target process.
        /// </value>
        public uint Task
        {
            get { return _task; }
            protected set { _task = value; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MacMemoryManipulator.MemoryManipulator"/> class.
        /// </summary>
        /// <param name='processId'>
        /// The process id of the target process.
        /// </param>
        public MemoryManipulator(int processId)
            :this()
        {
            Id = processId;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MacMemoryManipulator.MemoryManipulator"/> class.
        /// </summary>
        /// <param name='processName'>
        /// A part of the process name of the target process (see <see cref="System.Diagnostics.Process.GetProcessesByName"/>).
        /// </param>
        public MemoryManipulator(string processName)
            :this()
        {
            var processes = System.Diagnostics.Process.GetProcessesByName(processName);
            if (processes.Length == 0) {
                throw new Exception("No process found.");
            }
            Id = processes[0].Id;
        }

        protected MemoryManipulator()
        {
            _self = mach_task_self();
        }

        /// <summary>
        /// Read the specified address.
        /// </summary>
        /// <param name='address'>
        /// Address.
        /// </param>
        /// <typeparam name='T'>
        /// The type of the value. Can be a primitive type or a struct.
        /// </typeparam>
        public T Read<T>(ulong address) where T : struct
        {
            object result;
            Type type = typeof(T);
            TypeCode typeCode = Type.GetTypeCode(type);

            if (typeCode == TypeCode.Object) {
                result = ReadStruct(address, type);
            }
            else {
                int size = type == typeof(char) ? Marshal.SystemDefaultCharSize : Marshal.SizeOf(type);
                result = BytesToType(ReadBytes(address, size), typeCode);
            }

            return (T)result;
        }

        /// <summary>
        /// Read the specified address with the specified offset chain (32-bit version).
        /// </summary>
        /// <param name='address'>
        /// Address.
        /// </param>
        /// <param name='offsets'>
        /// Offsets.
        /// </param>
        /// <typeparam name='T'>
        /// The type of the value. Can be a primitive type or a struct.
        /// </typeparam>
        public T Read<T>(uint address, uint[] offsets) where T : struct
        {
            address = Read<uint>(address);

            for (int i = 0; i < offsets.Length - 1; i++)
            {
                address = Read<uint>(address + offsets[i]);
            }

            return Read<T>(address + offsets[offsets.Length - 1]);
        }

        /// <summary>
        /// Read the specified address with the specified offset chain (64-bit version).
        /// </summary>
        /// <param name='address'>
        /// Address.
        /// </param>
        /// <param name='offsets'>
        /// Offsets.
        /// </param>
        /// <typeparam name='T'>
        /// The type of the value. Can be a primitive type or a struct.
        /// </typeparam>
        public T Read<T>(ulong address, ulong[] offsets) where T : struct
        {
            address = Read<ulong>(address);

            for (int i = 0; i < offsets.Length - 1; i++)
            {
                address = Read<ulong>(address + offsets[i]);
            }

            return Read<T>(address + offsets[offsets.Length - 1]);
        }

        /// <summary>
        /// Reads the specified address as byte array.
        /// </summary>
        /// <returns>
        /// The byte array.
        /// </returns>
        /// <param name='address'>
        /// Address.
        /// </param>
        /// <param name='size'>
        /// Size of the region to read in bytes.
        /// </param>
        public byte[] ReadBytes(ulong address, int size)
        {
            if (size <= 0) {
                throw new ArgumentOutOfRangeException("size", size, "Must be bigger than 0.");
            }

            IntPtr dataPointer = Read(address, (ulong)size);
            byte[] data = new byte[size];
            Marshal.Copy(dataPointer, data, 0, size);
            Marshal.FreeHGlobal(dataPointer);

            return data;
        }

        /// <summary>
        /// Reads a null terminated string with the specified max size and encoding.
        /// </summary>
        /// <returns>
        /// The string.
        /// </returns>
        /// <param name='address'>
        /// Address.
        /// </param>
        /// <param name='maxSize'>
        /// Max size.
        /// </param>
        /// <param name='encoding'>
        /// Encoding.
        /// </param>
        public string ReadString(ulong address, int maxSize, Encoding encoding)
        {
            if (!(encoding.Equals(Encoding.UTF8) || encoding.Equals(Encoding.Unicode) || encoding.Equals(Encoding.ASCII)))
            {
                throw new ArgumentException(string.Format("Encoding type {0} is not supported", encoding.EncodingName), "encoding");
            }

            IntPtr dataPointer = Read(address, (ulong)maxSize);
            string result;

            if (encoding == Encoding.ASCII) {
                result = Marshal.PtrToStringAnsi(dataPointer);
            }
            else {
                result = Marshal.PtrToStringUni(dataPointer);    
            }

            Marshal.FreeHGlobal(dataPointer);

            return result;
        }

        /// <summary>
        /// Reads a struct.
        /// </summary>
        /// <returns>
        /// The struct.
        /// </returns>
        /// <param name='address'>
        /// Address.
        /// </param>
        /// <param name='type'>
        /// Type of the struct.
        /// </param>
        /// <remarks>
        /// Read<structType> can be used instead.
        /// </remarks>
        public object ReadStruct(ulong address, Type type)
        {
            IntPtr pointer = Read(address, (ulong)Marshal.SizeOf(type));
            var structure = Marshal.PtrToStructure(pointer, type);
            Marshal.FreeHGlobal(pointer);

            return structure;
        }

        /// <summary>
        /// Reads a array.
        /// </summary>
        /// <returns>
        /// The array.
        /// </returns>
        /// <param name='address'>
        /// Address.
        /// </param>
        /// <param name='size'>
        /// Size of the array.
        /// </param>
        /// <typeparam name='T'>
        /// The type of the array values.
        /// </typeparam>
        public T[] ReadArray<T>(ulong address, int size) where T : struct
        {
            int typeSize = Marshal.SizeOf(typeof(T));
            int dataSize = typeSize * size;
            IntPtr dataPointer = Read(address, (ulong)dataSize);

            try {
                switch (Type.GetTypeCode(typeof(T)))
                {
                    case TypeCode.Byte:
                        var bytes = new byte[size];
                        Marshal.Copy(dataPointer, bytes, 0, size);
                        return bytes.Cast<T>().ToArray();
                    case TypeCode.Char:
                        var chars = new char[size];
                        Marshal.Copy(dataPointer, chars, 0, size);
                        return chars.Cast<T>().ToArray();
                    case TypeCode.Int16:
                        var shorts = new short[size];
                        Marshal.Copy(dataPointer, shorts, 0, size);
                        return shorts.Cast<T>().ToArray();
                    case TypeCode.Int32:
                        var ints = new int[size];
                        Marshal.Copy(dataPointer, ints, 0, size);
                        return ints.Cast<T>().ToArray();
                    case TypeCode.Int64:
                        var longs = new long[size];
                        Marshal.Copy(dataPointer, longs, 0, size);
                        return longs.Cast<T>().ToArray();
                    case TypeCode.Single:
                        var floats = new float[size];
                        Marshal.Copy(dataPointer, floats, 0, size);
                        return floats.Cast<T>().ToArray();
                    case TypeCode.Double:
                        var doubles = new double[size];
                        Marshal.Copy(dataPointer, doubles, 0, size);
                        return doubles.Cast<T>().ToArray();
                    default:
                        var objects = new T[size];
                        IntPtr currentObjectPointer = dataPointer;
                        for (int i = 0; i < size; i++) {
                            objects[i] = (T)Marshal.PtrToStructure(currentObjectPointer, typeof(T));
                            currentObjectPointer = new IntPtr(currentObjectPointer.ToInt64() + typeSize);
                        }
                        return objects;
                }
            }
            finally {
                Marshal.FreeHGlobal(dataPointer);
            }
        }

        /// <summary>
        /// Writes data to the specified address.
        /// </summary>
        /// <param name='address'>
        /// Address.
        /// </param>
        /// <param name='data'>
        /// Data to write.
        /// </param>
        /// <typeparam name='T'>
        /// The type of the data.
        /// </typeparam>
        public void Write<T>(ulong address, T data) where T : struct
        {
            GCHandle pinnedData = GCHandle.Alloc(data, GCHandleType.Pinned);
            IntPtr pointer = pinnedData.AddrOfPinnedObject();

            try {
                if (0 != mach_vm_write(Task, address, pointer, (uint)Marshal.SizeOf(data))) {
                    throw new Exception("Unable to write to address " + address.ToString("X"));
                }
            }
            finally {
                pinnedData.Free();
            }
        }

        /// <summary>
        /// Reads memory of the specified address and size.
        /// </summary>
        /// <param name='address'>
        /// Address.
        /// </param>
        /// <param name='size'>
        /// Size.
        /// </param>
        /// <remarks>
        /// The returned pointer should be freed with Marshal.FreeHGlobal in the calling method to avoid memory leaks!
        /// </remarks>
        protected IntPtr Read(ulong address, ulong size)
        {
            IntPtr dataPointer = Marshal.AllocHGlobal((int)size);
            ulong bytesRead;

            if (0 != mach_vm_read_overwrite(Task, address, size, (ulong)dataPointer.ToInt64(), out bytesRead)) {
                throw new Exception("Unable to read address " + address.ToString("X"));
            }

            if (bytesRead != size) {
                throw new Exception(string.Format("Unable to read {0} bytes from process", size));
            }

            return dataPointer;
        }

        /// <summary>
        /// Converts a bytes to the specified type.
        /// </summary>
        /// <returns>
        /// The converted value.
        /// </returns>
        /// <param name='bytes'>
        /// Bytes.
        /// </param>
        /// <param name='type'>
        /// Type.
        /// </param>
        protected object BytesToType(byte[] bytes, TypeCode type)
        {
            object result;

            switch (type) {
                case TypeCode.Boolean:
                    result = BitConverter.ToBoolean(bytes, 0);
                    break;
                case TypeCode.Char:
                    result = BitConverter.ToChar(bytes, 0);
                    break;
                case TypeCode.Byte:
                    result = bytes[0];
                    break;
                case TypeCode.SByte:
                    result = (sbyte)bytes[0];
                    break;
                case TypeCode.Int16:
                    result = BitConverter.ToInt16(bytes, 0);
                    break;
                case TypeCode.Int32:
                    result = BitConverter.ToInt32(bytes, 0);
                    break;
                case TypeCode.Int64:
                    result = BitConverter.ToInt64(bytes, 0);
                    break;
                case TypeCode.UInt16:
                    result = BitConverter.ToUInt16(bytes, 0);
                    break;
                case TypeCode.UInt32:
                    result = BitConverter.ToUInt32(bytes, 0);
                    break;
                case TypeCode.UInt64:
                    result = BitConverter.ToUInt64(bytes, 0);
                    break;
                case TypeCode.Single:
                    result = BitConverter.ToSingle(bytes, 0);
                    break;
                case TypeCode.Double:
                    result = BitConverter.ToDouble(bytes, 0);
                    break;
                default:
                    throw new NotSupportedException(type + " is not supported yet");
            }

            return result;
        }
    }
}
