using System;
using System.Runtime.InteropServices;

namespace MacMemoryManipulator
{
    public partial class MemoryManipulator
    {
        [DllImport("libsystem_kernel")]
        protected static extern uint mach_task_self();

        [DllImport("libsystem_kernel")]
        protected static extern int task_for_pid(uint target_tport, int pid, out uint task);

        [DllImport("libsystem_kernel")]
        protected static extern int mach_vm_read_overwrite
        (
            uint target_task,
            ulong address,
            ulong size,
            ulong data,
            out ulong outsize
        );

        [DllImport("libsystem_kernel")]
        protected static extern int mach_vm_write
        (
            uint target_task,
            ulong address,
            IntPtr source,
            uint size
        );
    }
}
