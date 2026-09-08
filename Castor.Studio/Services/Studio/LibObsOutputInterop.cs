using System.Reflection;
using System.Runtime.InteropServices;
using LibObs;

namespace CastorApplication.Services.Studio;

internal static class LibObsOutputInterop
{
    private static readonly PropertyInfo OutputHandleProperty = GetHandleProperty(typeof(ObsOutput));
    private static readonly PropertyInfo EncoderHandleProperty = GetHandleProperty(typeof(ObsEncoder));
    private static readonly PropertyInfo ViewHandleProperty = GetHandleProperty(typeof(ObsView));

    private static PropertyInfo GetHandleProperty(Type type) => type.GetProperty(
        "Handle",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        ?? throw new MissingMemberException(type.FullName, "Handle");

    // LibObs 0.2.0 does not yet expose obs_output_set_mixers. Raw ffmpeg outputs
    // require this bit mask to create their audio streams.
    public static void SetAudioMixers(ObsOutput output, nuint mixers)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (OutputHandleProperty.GetValue(output) is not SafeHandle handle)
            throw new InvalidOperationException("Le handle natif de la sortie LibObs est inaccessible.");

        var addedReference = false;
        try
        {
            handle.DangerousAddRef(ref addedReference);
            ObsOutputSetMixers(handle.DangerousGetHandle(), mixers);
        }
        finally
        {
            if (addedReference) handle.DangerousRelease();
            GC.KeepAlive(output);
        }
    }

    public static void SetEncoderVideo(ObsEncoder encoder, nint video)
    {
        ArgumentNullException.ThrowIfNull(encoder);
        WithHandle(EncoderHandleProperty, encoder, handle => ObsEncoderSetVideo(handle, video));
    }

    public static void SetEncoderAudio(ObsEncoder encoder, nint audio)
    {
        ArgumentNullException.ThrowIfNull(encoder);
        WithHandle(EncoderHandleProperty, encoder, handle => ObsEncoderSetAudio(handle, audio));
    }

    public static nint AddView(ObsView view, ObsVideoInfo videoInfo)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(videoInfo);

        var graphicsModule = Marshal.StringToCoTaskMemAnsi(videoInfo.GraphicsModule ?? "");
        try
        {
            var native = new ObsVideoInfoNative
            {
                GraphicsModule = graphicsModule,
                FpsNumerator = videoInfo.FpsNumerator,
                FpsDenominator = videoInfo.FpsDenominator,
                BaseWidth = videoInfo.BaseWidth,
                BaseHeight = videoInfo.BaseHeight,
                OutputWidth = videoInfo.OutputWidth,
                OutputHeight = videoInfo.OutputHeight,
                OutputFormat = videoInfo.OutputFormat,
                Adapter = videoInfo.Adapter,
                GpuConversion = videoInfo.GpuConversion ? (byte)1 : (byte)0,
                ColorSpace = videoInfo.ColorSpace,
                Range = videoInfo.Range,
                ScaleType = videoInfo.ScaleType
            };

            return WithHandle(ViewHandleProperty, view, handle => ObsViewAdd2(handle, ref native));
        }
        finally
        {
            Marshal.FreeCoTaskMem(graphicsModule);
        }
    }

    public static void RemoveView(ObsView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        WithHandle(ViewHandleProperty, view, ObsViewRemove);
    }

    public static nint GetMainAudio() => ObsGetAudio();

    private static void WithHandle(PropertyInfo property, object value, Action<nint> action)
    {
        _ = WithHandle(property, value, handle =>
        {
            action(handle);
            return 0;
        });
    }

    private static T WithHandle<T>(PropertyInfo property, object value, Func<nint, T> action)
    {
        if (property.GetValue(value) is not SafeHandle handle)
            throw new InvalidOperationException("Le handle natif LibObs est inaccessible.");

        var addedReference = false;
        try
        {
            handle.DangerousAddRef(ref addedReference);
            return action(handle.DangerousGetHandle());
        }
        finally
        {
            if (addedReference) handle.DangerousRelease();
            GC.KeepAlive(value);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ObsVideoInfoNative
    {
        public nint GraphicsModule;
        public uint FpsNumerator;
        public uint FpsDenominator;
        public uint BaseWidth;
        public uint BaseHeight;
        public uint OutputWidth;
        public uint OutputHeight;
        public ObsVideoFormat OutputFormat;
        public uint Adapter;
        public byte GpuConversion;
        public ObsVideoColorSpace ColorSpace;
        public ObsVideoRange Range;
        public ObsScaleType ScaleType;
    }

    [DllImport("obs", EntryPoint = "obs_output_set_mixers", CallingConvention = CallingConvention.Cdecl)]
    private static extern void ObsOutputSetMixers(nint output, nuint mixers);

    [DllImport("obs", EntryPoint = "obs_encoder_set_video", CallingConvention = CallingConvention.Cdecl)]
    private static extern void ObsEncoderSetVideo(nint encoder, nint video);

    [DllImport("obs", EntryPoint = "obs_encoder_set_audio", CallingConvention = CallingConvention.Cdecl)]
    private static extern void ObsEncoderSetAudio(nint encoder, nint audio);

    [DllImport("obs", EntryPoint = "obs_view_add2", CallingConvention = CallingConvention.Cdecl)]
    private static extern nint ObsViewAdd2(nint view, ref ObsVideoInfoNative videoInfo);

    [DllImport("obs", EntryPoint = "obs_view_remove", CallingConvention = CallingConvention.Cdecl)]
    private static extern void ObsViewRemove(nint view);

    [DllImport("obs", EntryPoint = "obs_get_audio", CallingConvention = CallingConvention.Cdecl)]
    private static extern nint ObsGetAudio();
}
