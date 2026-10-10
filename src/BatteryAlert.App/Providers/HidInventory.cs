using HidSharp;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace BatteryAlert.App.Providers;

public sealed record HidEndpoint(HidDevice Device, ushort UsagePage, ushort Usage,
    int InputLength, int OutputLength, int FeatureLength, string Product, string Identity)
{
    public string HardwareId => $"{Device.VendorID:X4}:{Device.ProductID:X4}";
}

public static class HidInventory
{
    public static byte[] ReadInputControl(HidEndpoint endpoint, byte reportId)
    {
        using var handle = CreateFile(endpoint.Device.DevicePath, 0x80000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
        var report = new byte[endpoint.InputLength];
        report[0] = reportId;
        if (handle.IsInvalid || !HidD_GetInputReport(handle, report, report.Length))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        return report;
    }
    public static void SendOutputControl(HidEndpoint endpoint, byte[] report)
    {
        using var handle = CreateFile(endpoint.Device.DevicePath, 0x40000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
        if (handle.IsInvalid || !HidD_SetOutputReport(handle, report, report.Length))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Citirea POWER prin USB control a eșuat.");
    }
    public static IReadOnlyList<HidEndpoint> Enumerate()
    {
        var result = new List<HidEndpoint>();
        foreach (var device in DeviceList.Local.GetHidDevices())
        {
            try
            {
                using var handle = CreateFile(device.DevicePath, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
                if (handle.IsInvalid || !HidD_GetPreparsedData(handle, out var data)) continue;
                HidCaps caps;
                try { if (HidP_GetCaps(data, out caps) != 0x00110000) continue; }
                finally { HidD_FreePreparsedData(data); }
                var product = SafeString(device.GetProductName) ?? $"USB {device.VendorID:X4}:{device.ProductID:X4}";
                var serial = SafeString(device.GetSerialNumber);
                // Serial is preferred across cable/receiver changes. Empty/all-zero serials are not identities.
                var identity = !string.IsNullOrWhiteSpace(serial) && serial.Any(c => c != '0' && c != ' ')
                    ? $"serial:{serial}" : $"path:{device.DevicePath.ToLowerInvariant()}";
                identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..20];
                result.Add(new(device, caps.UsagePage, caps.Usage, caps.InputReportByteLength,
                    caps.OutputReportByteLength, caps.FeatureReportByteLength, product, identity));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException) { }
        }
        return result;
    }

    private static string? SafeString(Func<string> getter)
    {
        try { return getter(); }
        catch { return null; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HidCaps
    {
        public ushort Usage, UsagePage, InputReportByteLength, OutputReportByteLength, FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
        public ushort NumberLinkCollectionNodes, NumberInputButtonCaps, NumberInputValueCaps, NumberInputDataIndices;
        public ushort NumberOutputButtonCaps, NumberOutputValueCaps, NumberOutputDataIndices;
        public ushort NumberFeatureButtonCaps, NumberFeatureValueCaps, NumberFeatureDataIndices;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security,
        uint creation, uint flags, IntPtr template);
    [DllImport("hid.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool HidD_GetPreparsedData(SafeFileHandle device, out IntPtr data);
    [DllImport("hid.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool HidD_FreePreparsedData(IntPtr data);
    [DllImport("hid.dll")] private static extern int HidP_GetCaps(IntPtr data, out HidCaps caps);
    [DllImport("hid.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool HidD_SetOutputReport(SafeFileHandle device, byte[] report, int length);
    [DllImport("hid.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool HidD_GetInputReport(SafeFileHandle device, byte[] report, int length);
}
