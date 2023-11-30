using System.Reactive.Linq;
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
                    using var sourceReader = MediaFactory.MFCreateSourceReaderFromMediaSource(mediaSource, null);
                    sourceReader.SetStreamSelection(
                        mfSourceReaderFirstVideoStream,
                        true
                    );
                    using var mediaType = sourceReader.GetCurrentMediaType(mfSourceReaderFirstVideoStream);
                    var frameSize = mediaType.Get<ulong>(MediaTypeAttributeKeys.FrameSize);
                    var height = (int)(frameSize & ~0U);
                    var width = (int)(frameSize >> 32);
                    var (videoSubtype, fourCc, bitsPerPixel) = (VideoFormatGuids.Argb32, 0x00000015, 32);
                    using var destSample = MediaFactory.MFCreateSample();
                    using var destBuffer = MediaFactory.MFCreate2DMediaBuffer(
                        width,
                        height,
                        fourCc,
                        false);
                    destSample.AddBuffer(destBuffer);
                    using var destBuffer2d = destBuffer.QueryInterface<IMF2DBuffer>();
                    using var transform = GetTransform(
                        mediaType,
                        videoSubtype,
                        options
                    ) ?? throw new Exception("Unable to transform the video stream");
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

    static IMFMediaSource? GetMediaSource(int deviceIndex)
    {
        using var devices = MediaFactory.MFEnumVideoDeviceSources();
        var activateDevice = devices.Skip(deviceIndex).FirstOrDefault();
        return activateDevice?.ActivateObject<IMFMediaSource>();
    }

    static unsafe IMFTransform? GetTransform(
        IMFMediaType fromType,
        Guid toVideoSubtype,
        CameraOptions options)
    {
        MediaFactory.MFTEnumEx(
            TransformCategoryGuids.VideoProcessor,
            default,
            new RegisterTypeInfo
            {
                GuidMajorType = MediaTypeGuids.Video,
                GuidSubtype = fromType.Get<Guid>(MediaTypeAttributeKeys.Subtype)
            },
            new RegisterTypeInfo
            {
                GuidMajorType = MediaTypeGuids.Video,
                GuidSubtype = toVideoSubtype
            },
            out var pointerToActivateArray,
            out var numElementsInActivateArray);
        try
        {
            if (numElementsInActivateArray == 0)
                return null;
            var span = new ReadOnlySpan<IntPtr>((IntPtr*)pointerToActivateArray, (int)numElementsInActivateArray);
            for (var i = 1; i < span.Length; i++)
            {
                new IMFActivate(span[i]).Dispose();
            }

            using var activate = new IMFActivate(span[0]);
            var transform = activate.ActivateObject<IMFTransform>();
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
            return transform;
        }
        finally
        {
            Marshal.FreeCoTaskMem(pointerToActivateArray);
        }
    }
}