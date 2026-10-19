using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Naqla.Core;

namespace Naqla.Windows;

/// <summary>Locks a regular file against writers/deleters and renames that same file by handle,
/// without replacement. Directory handles anchor destinations while the operation is active.</summary>
public sealed class WindowsRenameFileSystem : IRenameFileSystem
{
    public RenameFileState Capture(string path)
    {
        using var handle = OpenFile(path, false);
        return ReadState(handle);
    }
    public IRenameFileHandle Lock(string path) => new LockedFile(path);

    private static SafeFileHandle OpenFile(string path, bool rename)
    {
        if (Path.GetFullPath(path).StartsWith(@"\\", StringComparison.Ordinal)) throw new NotSupportedException("اختر ملفًا محليًا عاديًا");
        var handle = CreateFileW(@"\\?\" + Path.GetFullPath(path), 0x80u | (rename ? 0x10100u : 0u), rename ? 1u : 7u, 0, 3, 0x00200000, 0);
        if (handle.IsInvalid) { handle.Dispose(); throw new Win32Exception(Marshal.GetLastWin32Error()); }
        try { _ = ReadState(handle); return handle; } catch { handle.Dispose(); throw; }
    }
    private static RenameFileState ReadState(SafeFileHandle handle)
    {
        if (!GetFileInformationByHandle(handle, out var info)) throw new Win32Exception(Marshal.GetLastWin32Error());
        if ((info.Attributes & (0x400u | 0x10u)) != 0 || info.Links != 1)
            throw new NotSupportedException("الروابط والملفات ذات نقاط إعادة التحليل تحتاج مسار إعادة تسمية منفصلًا");
        var id = ((ulong)info.IndexHigh << 32) | info.IndexLow;
        if (id == 0) throw new NotSupportedException("نظام الملفات لا يوفر هوية ثابتة للتراجع الآمن");
        return new(info.Volume, id, ((long)info.SizeHigh << 32) | info.SizeLow, Time(info.Write), Time(info.Creation));
    }
    private static long Time(NativeTime time) => ((long)time.High << 32) | time.Low;
    private sealed class LockedFile : IRenameFileHandle
    {
        private readonly SafeFileHandle file, directory;
        private readonly string parent;
        public RenameFileState State { get; }
        public string CurrentPath { get; private set; }
        public LockedFile(string path)
        {
            CurrentPath = Path.GetFullPath(path); parent = Path.GetDirectoryName(CurrentPath)!;
            directory = CreateFileW(@"\\?\" + parent, 0x81, 3, 0, 3, 0x02000000, 0);
            if (directory.IsInvalid) { directory.Dispose(); throw new Win32Exception(Marshal.GetLastWin32Error()); }
            try
            {
                file = OpenFile(CurrentPath, true); State = ReadState(file);
                var parentActual = FinalPath(directory).TrimEnd('\\');
                if (!string.Equals(FinalPath(file), parentActual + "\\" + Path.GetFileName(CurrentPath), StringComparison.OrdinalIgnoreCase))
                    throw new IOException("تغير موقع الملف أثناء التحقق؛ أعد المعاينة");
            }
            catch { file?.Dispose(); directory.Dispose(); throw; }
        }
        public void RenameToLeaf(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename) || filename != Path.GetFileName(filename) || filename.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException("اسم الملف غير صالح");
            var bytes = Encoding.Unicode.GetBytes(FinalPath(directory).TrimEnd('\\') + "\\" + filename);
            var offset = Marshal.OffsetOf<RenameInfo>(nameof(RenameInfo.FirstChar)).ToInt32();
            var length = offset + bytes.Length + 2;
            var memory = Marshal.AllocHGlobal(length);
            try
            {
                Marshal.Copy(new byte[length], 0, memory, length);
                Marshal.StructureToPtr(new RenameInfo { Length = (uint)bytes.Length }, memory, false);
                Marshal.Copy(bytes, 0, memory + offset, bytes.Length);
                if (!SetFileInformationByHandle(file, 3, memory, (uint)length)) throw new Win32Exception(Marshal.GetLastWin32Error());
                CurrentPath = Path.Combine(parent, filename);
                var after = ReadState(file);
                if (after.Volume != State.Volume || after.FileId != State.FileId || after.Length != State.Length || after.ModifiedFileTime != State.ModifiedFileTime)
                    throw new IOException("تغيرت هوية الملف أو محتواه أثناء إعادة التسمية");
                // NTFS name tunneling can substitute a former occupant's creation time during swaps.
                // Restore only this same locked file's captured creation time; never write its contents.
                if (after.CreatedFileTime != State.CreatedFileTime)
                {
                    var created = new NativeTime { Low = (uint)State.CreatedFileTime, High = (uint)(State.CreatedFileTime >> 32) };
                    if (!SetFileTime(file, in created, 0, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
                    if (ReadState(file) != State) throw new IOException("تعذر الحفاظ على تاريخ إنشاء الملف أثناء إعادة التسمية");
                }
            }
            finally { Marshal.FreeHGlobal(memory); }
        }
        public void Dispose() { file.Dispose(); directory.Dispose(); }
    }
    private static string FinalPath(SafeFileHandle handle)
    {
        var buffer = new StringBuilder(32768);
        var count = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Capacity, 0);
        if (count == 0 || count >= buffer.Capacity) throw new Win32Exception(Marshal.GetLastWin32Error());
        return buffer.ToString();
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativeTime { public uint Low, High; }
    [StructLayout(LayoutKind.Sequential)] private struct FileInfoNative
    {
        public uint Attributes; public NativeTime Creation, Access, Write;
        public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct RenameInfo
    { public uint Flags; public nint Root; public uint Length; public char FirstChar; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateFileW(string name, uint access, uint share, nint security, uint disposition, uint flags, nint template);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetFileInformationByHandle(SafeFileHandle file, out FileInfoNative info);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetFileInformationByHandle(SafeFileHandle file, int kind, nint data, uint size);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetFileTime(SafeFileHandle file, in NativeTime created, nint accessed, nint modified);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern uint GetFinalPathNameByHandleW(SafeFileHandle file, StringBuilder name, uint length, uint flags);
}
