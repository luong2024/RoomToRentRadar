using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Primitives;
using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Api.Binders;

/// <summary>
/// Binds <c>roomTypes</c> of <c>operationId: searchRooms</c> (US04, <c>docs/api/openapi.yaml</c>:
/// <c>style: form, explode: false</c>) to <c>IReadOnlyList&lt;RoomType&gt;?</c>.
/// Accepts both <c>?roomTypes=MiniApartment,WholeHouse</c> and <c>?roomTypes=A&amp;roomTypes=B</c>.
/// Values are trimmed, empty entries ignored and enum names parsed case-insensitively;
/// an unknown value adds a <see cref="ModelStateDictionary"/> error so <c>[ApiController]</c> answers 400.
/// A parameter without any usable value (<c>?roomTypes=</c>, <c>?roomTypes=A&amp;roomTypes=</c>) binds to <c>null</c>,
/// i.e. "no room-type filter", never to an empty list.
/// </summary>
public sealed class RoomTypesModelBinder : IModelBinder
{
    private static readonly char[] Separators = { ',' };

    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (valueProviderResult == ValueProviderResult.None)
        {
            // Không có tham số → null (use case coi như "không lọc theo loại phòng").
            bindingContext.Result = ModelBindingResult.Success(null);
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueProviderResult);

        var roomTypes = new List<RoomType>();
        var hasInvalidValue = false;

        foreach (var rawValue in valueProviderResult.Values)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                continue;
            }

            foreach (var part in rawValue.Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!Enum.TryParse<RoomType>(part, ignoreCase: true, out var roomType)
                    || !Enum.IsDefined(roomType))
                {
                    bindingContext.ModelState.TryAddModelError(
                        bindingContext.ModelName,
                        $"The value '{part}' is not a valid room type. Allowed values: {string.Join(", ", Enum.GetNames<RoomType>())}.");
                    hasInvalidValue = true;
                    continue;
                }

                roomTypes.Add(roomType);
            }
        }

        // Giá trị sai đã được ghi vào ModelState → [ApiController] tự trả 400 + problem+json.
        if (hasInvalidValue)
        {
            bindingContext.Result = ModelBindingResult.Failed();
            return Task.CompletedTask;
        }

        // Có tham số nhưng không còn giá trị hiệu lực (`?roomTypes=` hoặc `?roomTypes=A&roomTypes=`):
        // trả null như "không lọc theo loại phòng", không phải list rỗng (list rỗng = lọc ra 0 phòng).
        bindingContext.Result = ModelBindingResult.Success(
            roomTypes.Count == 0 ? null : roomTypes);

        return Task.CompletedTask;
    }
}

/// <summary>
/// Registers <see cref="RoomTypesModelBinder"/> for <c>IReadOnlyList&lt;RoomType&gt;</c>,
/// <c>IEnumerable&lt;RoomType&gt;</c> and <c>List&lt;RoomType&gt;</c>.
/// </summary>
public sealed class RoomTypesModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var modelType = context.Metadata.ModelType;
        if (modelType == typeof(IReadOnlyList<RoomType>)
            || modelType == typeof(IEnumerable<RoomType>)
            || modelType == typeof(IList<RoomType>)
            || modelType == typeof(List<RoomType>))
        {
            return new RoomTypesModelBinder();
        }

        return null;
    }
}
