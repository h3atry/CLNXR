using System;
using System.Runtime.InteropServices;

namespace Clnxr.Platform.Windows
{
    public sealed class RecycleBinSnapshot
    {
        public RecycleBinSnapshot(bool available, long itemCount, long bytes, string message)
        {
            Available = available;
            ItemCount = itemCount;
            Bytes = bytes;
            Message = message ?? string.Empty;
        }

        public bool Available { get; private set; }
        public long ItemCount { get; private set; }
        public long Bytes { get; private set; }
        public string Message { get; private set; }
    }

    public sealed class RecycleBinEmptyResult
    {
        public RecycleBinEmptyResult(bool succeeded, string message)
        {
            Succeeded = succeeded;
            Message = message ?? string.Empty;
        }

        public bool Succeeded { get; private set; }
        public string Message { get; private set; }
    }

    public sealed class RecycleBinService
    {
        private const uint NoConfirmation = 0x00000001;
        private const uint NoProgressUi = 0x00000002;
        private const uint NoSound = 0x00000004;

        [StructLayout(LayoutKind.Sequential)]
        private struct ShellQueryRecycleBinInfo
        {
            public int Size;
            public long Bytes;
            public long Items;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHQueryRecycleBin(string rootPath, ref ShellQueryRecycleBinInfo info);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHEmptyRecycleBin(IntPtr ownerWindow, string rootPath, uint flags);

        public RecycleBinSnapshot QueryAllVolumes()
        {
            try
            {
                ShellQueryRecycleBinInfo info = new ShellQueryRecycleBinInfo();
                info.Size = Marshal.SizeOf(typeof(ShellQueryRecycleBinInfo));
                int result = SHQueryRecycleBin(null, ref info);
                if (result != 0) return new RecycleBinSnapshot(false, 0, 0, "A API do Windows retornou o codigo " + result + " ao consultar a Lixeira.");
                return new RecycleBinSnapshot(true, info.Items, info.Bytes, "Consulta somente leitura concluida para todas as unidades.");
            }
            catch (Exception ex)
            {
                return new RecycleBinSnapshot(false, 0, 0, "Nao foi possivel consultar a Lixeira: " + ex.Message);
            }
        }

        public RecycleBinEmptyResult EmptyAllVolumes()
        {
            try
            {
                int result = SHEmptyRecycleBin(IntPtr.Zero, null, NoConfirmation | NoProgressUi | NoSound);
                if (result != 0) return new RecycleBinEmptyResult(false, "A API do Windows retornou o codigo " + result + " ao esvaziar a Lixeira.");
                return new RecycleBinEmptyResult(true, "Lixeira esvaziada pela API oficial do Windows.");
            }
            catch (Exception ex)
            {
                return new RecycleBinEmptyResult(false, "Nao foi possivel esvaziar a Lixeira: " + ex.Message);
            }
        }
    }
}
