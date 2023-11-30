using System.Reflection;
using System.Runtime.InteropServices;
using SharpGen.Runtime;
using Vortice.MediaFoundation;

namespace WpfApp;

public sealed class ImfActivateCollection2<T> : IDisposable
    where T : ComObject
{
    IMFActivate[]? _array;

    public unsafe ImfActivateCollection2(IntPtr pointerToActivateArray, uint count)
    {
        var arrayPointer = (IntPtr*)pointerToActivateArray;
        _array = new IMFActivate[count];
        for (var i = 0U; i < count; i++)
        {
            _array[i] = new IMFActivate(arrayPointer[i]);
        }
        Marshal.FreeCoTaskMem(pointerToActivateArray);
    }

    public int Count => _array?.Length ?? 0;

    public T Create(int index)
    {
        if (_array is not { } array)
            throw new Exception("The collection has already been disposed of");
        if (index < 0 || index >= array.Length)
            throw new IndexOutOfRangeException();
        return array[index].ActivateObject<T>();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _array, null) is not { } array)
            return;
        foreach (var item in array)
        {
            item.Dispose();
        }
        Array.Clear(array);
    }
}

public static class ImfHelpers
{
    public static readonly IReadOnlyDictionary<Guid, string> VideoSubtypeNames = typeof(VideoFormatGuids)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.IsInitOnly && field.FieldType == typeof(Guid))
        .ToDictionary(
            field => (Guid)field.GetValue(null)!,
            field => field.Name
        );

    public static readonly IReadOnlyDictionary<Guid, string> MediaTypeAttributeNames = typeof(MediaTypeAttributeKeys)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.IsInitOnly && field.FieldType == typeof(Guid))
        .ToDictionary(
            field => (Guid)field.GetValue(null)!,
            field => field.Name
        );

    public static IMFMediaType Clone(IMFMediaType mediaType)
    {
        var clone = MediaFactory.MFCreateMediaType();
        mediaType.CopyAllItems(clone);
        return clone;
    }

    public static IEnumerable<IMFMediaType> GetMediaTypes(IMFMediaSource source)
    {
        using var presentationDescriptor = source.CreatePresentationDescriptor();
        for (var streamDescriptorIndex = 0; streamDescriptorIndex < presentationDescriptor.StreamDescriptorCount; streamDescriptorIndex++)
        {
            presentationDescriptor.GetStreamDescriptorByIndex(streamDescriptorIndex, out var selected, out var streamDescriptor);
            using var _ = streamDescriptor;
            if (!selected)
                continue;
            using var mediaTypeHandler = streamDescriptor.MediaTypeHandler;
            for (var mediaTypeIndex = 0; mediaTypeIndex < mediaTypeHandler.MediaTypeCount; mediaTypeIndex++)
            {
                using var mediaType = mediaTypeHandler.GetMediaTypeByIndex(mediaTypeIndex);
                yield return mediaType;
            }
        }
    }

    public static ImfActivateCollection2<IMFTransform> GetTransforms(
        Guid fromVideoSubtype,
        Guid toVideoSubtype)
    {
        MediaFactory.MFTEnumEx(
            TransformCategoryGuids.VideoProcessor,
            default,
            new RegisterTypeInfo
            {
                GuidMajorType = MediaTypeGuids.Video,
                GuidSubtype = fromVideoSubtype
            },
            new RegisterTypeInfo
            {
                GuidMajorType = MediaTypeGuids.Video,
                GuidSubtype = toVideoSubtype
            },
            out var pointerToActivateArray,
            out var numElementsInActivateArray);
        return new ImfActivateCollection2<IMFTransform>(pointerToActivateArray, numElementsInActivateArray);
    }

    public static (uint Upper, uint Lower) Tear(ulong joined)
    {
        return ((uint)(joined >> 32), (uint)joined);
    }
}