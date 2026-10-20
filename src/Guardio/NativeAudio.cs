using System.Runtime.InteropServices;
using Guardio.Core;

namespace Guardio;

public record Endpoint(string Id, string Name, string Description, string InterfaceName,
    uint State, bool Input)
{
    public bool Active => State == 1;
    // Detected from the device names; GuardService applies the user's manual overrides.
    public bool IsController { get; init; } = !Input && ControllerDetector.Matches(Name, Description, InterfaceName);
    public OutputDevice ToOutput() => new(Id, Name, Active, IsController);
    public override string ToString() => Name;
}

public sealed class NativeAudio : IDisposable
{
    private readonly IMMDeviceEnumerator enumerator =
        (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
    private Notifications? notifications;
    public event Action<string>? DevicesChanged;
    public event Action<int, AudioRole, string?>? DefaultChanged;
    public List<Endpoint> Endpoints(bool input = false, bool all = false)
    {
        Check(enumerator.EnumAudioEndpoints(input ? 1 : 0, all ? 15u : 1u, out var collection));
        var result = new List<Endpoint>();
        try
        {
            Check(collection.GetCount(out var count));
            for (uint i = 0; i < count; i++)
            {
                Check(collection.Item(i, out var device));
                try
                {
                    Check(device.GetId(out var id)); Check(device.GetState(out var state));
                    Check(device.OpenPropertyStore(0, out var store));
                    try
                    {
                        result.Add(new(id, ReadString(store, PropertyKey.FriendlyName),
                            ReadString(store, PropertyKey.Description),
                            ReadString(store, PropertyKey.InterfaceName), state, input));
                    }
                    finally { Marshal.ReleaseComObject(store); }
                }
                finally { Marshal.ReleaseComObject(device); }
            }
        }
        finally { Marshal.ReleaseComObject(collection); }
        return result;
    }

    public string? Default(AudioRole role, bool input = false)
    {
        int hr = enumerator.GetDefaultAudioEndpoint(input ? 1 : 0, (int)role, out var device);
        if (hr < 0) return null;
        try { Check(device.GetId(out var id)); return id; }
        finally { Marshal.ReleaseComObject(device); }
    }

    public Dictionary<AudioRole, string?> Defaults(bool input = false) =>
        Enum.GetValues<AudioRole>().ToDictionary(r => r, r => Default(r, input));

    public void SetDefault(string id, AudioRole role)
    {
        // Windows' PolicyConfig interface is used only for the default output.
        // No device visibility, driver, volume or HID state is changed.
        if (!Endpoints().Any(e => e.Id == id))
            throw new InvalidOperationException("Cette sortie n’est plus disponible.");
        object client = new PolicyConfigCom();
        try { Check(((IPolicyConfig)client).SetDefaultEndpoint(id, (int)role)); }
        finally { Marshal.ReleaseComObject(client); }
    }

    public void StartNotifications()
    {
        if (notifications is not null) return;
        notifications = new Notifications(this);
        Check(enumerator.RegisterEndpointNotificationCallback(notifications));
    }

    public void Dispose()
    {
        if (notifications is not null)
        {
            enumerator.UnregisterEndpointNotificationCallback(notifications);
            notifications = null;
        }
        Marshal.ReleaseComObject(enumerator);
        GC.SuppressFinalize(this);
    }

    private static string ReadString(IPropertyStore store, PropertyKey key)
    {
        if (store.GetValue(ref key, out var value) < 0) return "";
        try { return value.Type == 31 ? Marshal.PtrToStringUni(value.Pointer) ?? "" : ""; }
        finally { PropVariantClear(ref value); }
    }
    private static void Check(int hr) => Marshal.ThrowExceptionForHR(hr);
    [DllImport("ole32.dll")] private static extern int PropVariantClear(ref PropVariant value);

    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    private sealed class Notifications(NativeAudio owner) : IMMNotificationClient
    {
        public int OnDeviceStateChanged(string id, uint state) { owner.DevicesChanged?.Invoke(id); return 0; }
        public int OnDeviceAdded(string id) { owner.DevicesChanged?.Invoke(id); return 0; }
        public int OnDeviceRemoved(string id) { owner.DevicesChanged?.Invoke(id); return 0; }
        public int OnDefaultDeviceChanged(int flow, int role, string? id)
        { owner.DefaultChanged?.Invoke(flow, (AudioRole)role, id); return 0; }
        public int OnPropertyValueChanged(string id, PropertyKey key)
        { owner.DevicesChanged?.Invoke(id); return 0; }
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct PropertyKey(Guid format, uint id)
{
    public Guid Format = format; public uint Id = id;
    public static PropertyKey FriendlyName => new(new("a45c254e-df1c-4efd-8020-67d146a850e0"), 14);
    public static PropertyKey Description => new(new("a45c254e-df1c-4efd-8020-67d146a850e0"), 2);
    public static PropertyKey InterfaceName => new(new("026e516e-b814-414b-83cd-856d6fef4822"), 2);
}
[StructLayout(LayoutKind.Explicit, Size = 24)]
public struct PropVariant { [FieldOffset(0)] public ushort Type; [FieldOffset(8)] public IntPtr Pointer; }

[ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
internal class MMDeviceEnumeratorCom { }
[ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    [PreserveSig] int EnumAudioEndpoints(int flow, uint state, out IMMDeviceCollection devices);
    [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice device);
    [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
    [PreserveSig] int RegisterEndpointNotificationCallback(IMMNotificationClient client);
    [PreserveSig] int UnregisterEndpointNotificationCallback(IMMNotificationClient client);
}
[ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceCollection
{
    [PreserveSig] int GetCount(out uint count);
    [PreserveSig] int Item(uint index, out IMMDevice device);
}
[ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    [PreserveSig] int Activate(ref Guid iid, uint context, IntPtr activation, [MarshalAs(UnmanagedType.IUnknown)] out object result);
    [PreserveSig] int OpenPropertyStore(uint access, out IPropertyStore store);
    [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    [PreserveSig] int GetState(out uint state);
}
[ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPropertyStore
{
    [PreserveSig] int GetCount(out uint count);
    [PreserveSig] int GetAt(uint index, out PropertyKey key);
    [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
    [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant value);
    [PreserveSig] int Commit();
}
[ComVisible(true), Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMMNotificationClient
{
    [PreserveSig] int OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string id, uint state);
    [PreserveSig] int OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string id);
    [PreserveSig] int OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string id);
    [PreserveSig] int OnDefaultDeviceChanged(int flow, int role, [MarshalAs(UnmanagedType.LPWStr)] string? id);
    [PreserveSig] int OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string id, PropertyKey key);
}
[ComImport, Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")] internal class PolicyConfigCom { }
[ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPolicyConfig
{
    [PreserveSig] int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string id, out IntPtr format);
    [PreserveSig] int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id, int @default, out IntPtr format);
    [PreserveSig] int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id);
    [PreserveSig] int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr endpoint, IntPtr mix);
    [PreserveSig] int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string id, int @default, out long normal, out long minimum);
    [PreserveSig] int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string id, ref long period);
    [PreserveSig] int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr mode);
    [PreserveSig] int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr mode);
    [PreserveSig] int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string id, ref PropertyKey key, out PropVariant value);
    [PreserveSig] int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string id, ref PropertyKey key, ref PropVariant value);
    [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string id, int role);
    [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string id, int visible);
}
