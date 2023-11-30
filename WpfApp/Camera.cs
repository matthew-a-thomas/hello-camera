using System.Diagnostics;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Vortice.MediaFoundation;

namespace WpfApp;

public struct CameraOptions
{
    public int DeviceIndex;
    public bool FlipX;
    public bool FlipY;
}

public static class Camera
{
    public static IObservable<FrameAvailableEvent> GetFrames(
        CameraOptions options
    ) => Observable.Create<FrameAvailableEvent>(observer =>
    {
        var disposeFlag = new DisposeFlag();
        var blobCache = new BlobCache<byte>();
        var (videoSubtype, fourCc, bitsPerPixel) = (VideoFormatGuids.Argb32, 0x00000015, 32);
        Task.Factory.StartNew(
            () =>
            {
                const int mfSourceReaderFirstVideoStream = unchecked((int)0xFFFFFFFC);
                MediaFactory.MFStartup(true).CheckError();
                try
                {
                    using var mediaSource = GetMediaSource(options.DeviceIndex);
                    if (mediaSource is null)
                        throw new Exception("Cannot find a device with that index");
                    using var sourceMediaType = GetBestMediaType(mediaSource, videoSubtype);
                    if (sourceMediaType is null)
                        throw new Exception("Cannot read from this video stream");
                    using var transform = CreateTransform(sourceMediaType, videoSubtype, options);
                    PrintInfo(sourceMediaType);
                    using var sourceReader = MediaFactory.MFCreateSourceReaderFromMediaSource(mediaSource, null);
                    sourceReader.SetStreamSelection(
                        mfSourceReaderFirstVideoStream,
                        true
                    );
                    sourceReader.SetCurrentMediaType(mfSourceReaderFirstVideoStream, sourceMediaType);
                    var frameSize = sourceMediaType.Get<ulong>(MediaTypeAttributeKeys.FrameSize);
                    var height = (int)(frameSize & ~0U);
                    var width = (int)(frameSize >> 32);
                    using var destSample = MediaFactory.MFCreateSample();
                    using var destBuffer = MediaFactory.MFCreate2DMediaBuffer(
                        width,
                        height,
                        fourCc,
                        false);
                    destSample.AddBuffer(destBuffer);
                    using var destBuffer2d = destBuffer.QueryInterface<IMF2DBuffer>();
                    transform.ProcessMessage(TMessageType.MessageNotifyBeginStreaming, default);
                    transform.ProcessMessage(TMessageType.MessageNotifyStartOfStream, default);
                    try
                    {
                        while (!disposeFlag.Disposed)
                        {
                            sourceReader.ReadSample(
                                mfSourceReaderFirstVideoStream,
                                default,
                                out _,
                                out _,
                                out var timestampTicks,
                                out var sample);
                            if (sample is null)
                                continue;
                            using var __ = sample;
                            transform.ProcessMessage(TMessageType.MessageCommandFlush, default);
                            transform.ProcessInput(0, sample, 0);
                            var outputSample = new OutputDataBuffer
                            {
                                Events = null,
                                Status = 0,
                                StreamID = 0,
                                Sample = destSample
                            };
                            transform.ProcessOutput(
                                0,
                                1,
                                ref outputSample,
                                out _).CheckError();

                            destBuffer2d.Lock2D(out _, out var pitch);
                            try
                            {
                                var length = destBuffer2d.ContiguousLength;
                                var array = blobCache.Get(length);
                                destBuffer2d.ContiguousCopyTo(array, length);
                                using var frameAvailableEvent = new FrameAvailableEvent(
                                    array,
                                    bitsPerPixel,
                                    pitch,
                                    new TimeSpan(timestampTicks),
                                    () => blobCache.Return(array)
                                );
                                observer.OnNext(frameAvailableEvent);
                            }
                            finally
                            {
                                destBuffer2d.Unlock2D();
                            }
                        }
                    }
                    finally
                    {
                        transform.ProcessMessage(TMessageType.MessageNotifyEndOfStream, 0);
                        transform.ProcessMessage(TMessageType.MessageNotifyEndStreaming, default);
                    }
                }
                catch (Exception e)
                {
                    observer.OnError(e);
                }
                finally
                {
                    MediaFactory.MFShutdown().CheckError();
                    observer.OnCompleted();
                }
            },
            TaskCreationOptions.LongRunning);
        return disposeFlag;
    });

    static void PrintInfo(IMFMediaType videoMediaType)
    {
        const string category = nameof(Camera) + "." + nameof(PrintInfo);
        if (videoMediaType.MajorType == MediaTypeGuids.Video)
        {
            var subtype = videoMediaType.Get<Guid>(MediaTypeAttributeKeys.Subtype);
            var subtypeName = ImfHelpers.VideoSubtypeNames.GetValueOrDefault(subtype) ?? subtype.ToString();
            Trace.WriteLine($"Video subtype: {subtypeName}", category);
        }
        foreach (var (key, value) in GetAllAttributes(videoMediaType))
        {
            Trace.WriteLine($"{key}: {value}", category);
        }
    }

    static IReadOnlyDictionary<string, object> GetAllAttributes(IMFAttributes attributes)
    {
        var values = new Dictionary<string, object>();
        foreach (var (key, value) in attributes.GetAllAttributes())
        {
            var name = ImfHelpers.MediaTypeAttributeNames.GetValueOrDefault(key) ?? key.ToString();
            values[name] = value;
            if (value is ulong joinedUlong)
            {
                values[name + " (split)"] = ImfHelpers.Tear(joinedUlong);
            }
            else if (value is long joinedLong)
            {
                values[name + " (split)"] = ImfHelpers.Tear((ulong)joinedLong);
            }
        }
        return values;
    }

    static IMFMediaType? GetBestMediaType(
        IMFMediaSource mediaSource,
        Guid toVideoSubtype)
    {
        var chosenMediaTypeIndex = -1;
        var allMediaTypes = ImfHelpers
            .GetMediaTypes(mediaSource)
            .Select(ImfHelpers.Clone)
            .ToList();
        try
        {
            var annotatedMediaTypes = allMediaTypes.Select((x, i) => (
                MediaType: x,
                MediaTypeIndex: i,
                Attributes: x.GetAllAttributes().ToDictionary(tuple => tuple.Key, tuple => tuple.Value)
            ));
            var preferred =
                from tuple in annotatedMediaTypes
                from _ in new TransformsToSubtype(toVideoSubtype).Get(tuple.Attributes).Take(1) // Any supported transforms?
                from frameRate in FrameRate.Get(tuple.Attributes)
                from frameSize in FrameSize.Get(tuple.Attributes)
                // from sampleSize in SampleSize.Get(tuple.Attributes)
                // from bitrate in Bitrate.Get(tuple.Attributes)
                orderby
                    frameSize descending,
                    // bitrate descending,
                    frameRate descending
                select tuple;
            if (preferred.FirstOrNull() is not var (mediaType, mediaTypeIndex, _))
                return null;
            chosenMediaTypeIndex = mediaTypeIndex;
            return mediaType;
        }
        finally
        {
            for (var i = 0; i < allMediaTypes.Count; i++)
            {
                if (i != chosenMediaTypeIndex)
                    allMediaTypes[i].Dispose();
            }
            allMediaTypes.Clear();
        }
    }

    static IMFMediaSource? GetMediaSource(int deviceIndex)
    {
        using var devices = MediaFactory.MFEnumVideoDeviceSources();
        var activateDevice = devices.Skip(deviceIndex).FirstOrDefault();
        return activateDevice?.ActivateObject<IMFMediaSource>();
    }

    static IMFTransform CreateTransform(
        IMFMediaType fromType,
        Guid toVideoSubtype,
        CameraOptions options)
    {
        var fromVideoSubtype = fromType.Get<Guid>(MediaTypeAttributeKeys.Subtype);
        using var collection = ImfHelpers.GetTransforms(fromVideoSubtype, toVideoSubtype);
        var transform = collection.Create(0);
        ConfigureTransform(transform, fromType, toVideoSubtype, options);
        return transform;
    }

    static void ConfigureTransform(
        IMFTransform transform,
        IMFMediaType fromType,
        Guid toVideoSubtype,
        CameraOptions options)
    {
        transform.SetInputType(
            0,
            fromType,
            0);
        using var outputType = MediaFactory.MFCreateMediaType();
        fromType.CopyAllItems(outputType);
        outputType.Set(MediaTypeAttributeKeys.Subtype, toVideoSubtype);
        transform.SetOutputType(
            0,
            outputType,
            0);
        using var control = transform.QueryInterface<IMFVideoProcessorControl>();
        // ReSharper disable BitwiseOperatorOnEnumWithoutFlags
        var mirror = VideoProcessorMirror.MirrorNone;
        if (options.FlipX)
            mirror |= VideoProcessorMirror.MirrorHorizontal;
        if (options.FlipY)
            mirror |= VideoProcessorMirror.MirrorVertical;
        // ReSharper restore BitwiseOperatorOnEnumWithoutFlags
        control.SetMirror(mirror);
    }
}

public static class ImfAttributesExtensions
{
    public static IEnumerable<(Guid Key, object Value)> GetAllAttributes(this IMFAttributes attributes)
    {
        attributes.LockStore();
        try
        {
            for (var i = 0U; i < attributes.Count; i++)
            {
                var value = attributes.GetByIndex(i, out var key);
                yield return (key, value);
            }
        }
        finally
        {
            attributes.UnlockStore();
        }
    }
}

public interface IStaticAttributeFilter<out TValue>
{
    public static abstract IEnumerable<TValue> Get(IReadOnlyDictionary<Guid, object> attributes);
}

public interface IAttributeFilter<out TValue>
{
    IEnumerable<TValue> Get(IReadOnlyDictionary<Guid, object> attributes);
}

public sealed class CommonVideoSubtypeName : IStaticAttributeFilter<string>
{
    public static IEnumerable<string> Get(IReadOnlyDictionary<Guid, object> attributes)
    {
        if (!attributes.TryGetValue(MediaTypeAttributeKeys.Subtype, out var rawSubtype) || rawSubtype is not Guid subtype)
            yield break;
        if (ImfHelpers.VideoSubtypeNames.TryGetValue(subtype, out var name))
            yield return name;
    }
}

public sealed class FrameRate : IStaticAttributeFilter<double>
{
    public static IEnumerable<double> Get(IReadOnlyDictionary<Guid, object> attributes)
    {
        if (!attributes.TryGetValue(MediaTypeAttributeKeys.FrameRate, out var framerateRaw) || framerateRaw is not long framerateLong)
        {
            yield return 0;
        }
        else
        {
            var (numerator, denominator) = ImfHelpers.Tear((ulong)framerateLong);
            yield return (double)numerator / denominator;
        }
    }
}

public sealed class FrameSize : IStaticAttributeFilter<(uint Width, uint Height)>
{
    public static IEnumerable<(uint Width, uint Height)> Get(IReadOnlyDictionary<Guid, object> attributes)
    {
        if (!attributes.TryGetValue(MediaTypeAttributeKeys.FrameSize, out var frameSizeRaw) || frameSizeRaw is not long frameSizeLong)
            yield return default;
        else
            yield return ImfHelpers.Tear((ulong)frameSizeLong);
    }
}

public sealed class Bitrate : IStaticAttributeFilter<uint>
{
    public static IEnumerable<uint> Get(IReadOnlyDictionary<Guid, object> attributes)
    {
        if (!attributes.TryGetValue(MediaTypeAttributeKeys.AvgBitrate, out var bitrateRaw) || bitrateRaw is not int bitrateInt)
            yield return default;
        else
            yield return (uint)bitrateInt;
    }
}

public sealed class TransformsToSubtype(Guid subtype) : IAttributeFilter<Func<IMFTransform>>
{
    public IEnumerable<Func<IMFTransform>> Get(IReadOnlyDictionary<Guid, object> attributes)
    {
        if (!attributes.TryGetValue(MediaTypeAttributeKeys.Subtype, out var fromSubtypeRaw) || fromSubtypeRaw is not Guid fromSubtype)
            yield break;
        using var collection = ImfHelpers.GetTransforms(fromSubtype, subtype);
        for (var i = 0; i < collection.Count; i++)
        {
            var capturedIndex = i;
            yield return () => collection.Create(capturedIndex);
        }
    }
}

public sealed class SampleSize : IStaticAttributeFilter<uint>
{
    public static IEnumerable<uint> Get(IReadOnlyDictionary<Guid, object> attributes)
    {
        if (!attributes.TryGetValue(MediaTypeAttributeKeys.SampleSize, out var sampleSizeRaw) || sampleSizeRaw is not int sampleSizeInt)
            yield return default;
        else
            yield return (uint)sampleSizeInt;
    }
}