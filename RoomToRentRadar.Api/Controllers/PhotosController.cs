using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Api.Models;
using RoomToRentRadar.Api.Services.Photos;
using RoomToRentRadar.Shared.Photos;

namespace RoomToRentRadar.Api.Controllers;

[ApiController]
[Route("api/v1/listings/{listingId}/photos")]
public class PhotosController(IPhotoService photoService) : ControllerBase
{
    private const long MaxRequestBytes = 110L * 1024 * 1024; // 10 tệp x 10 MB + overhead

    /// <summary>Upload ảnh (multipart/form-data): files[], is360[] cùng thứ tự (US03 T03.1).</summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    [ProducesResponseType(typeof(UploadPhotosResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UploadPhotosResponse>> Upload(
        string listingId, [FromForm] PhotoUploadForm form, CancellationToken ct)
    {
        var streams = new List<Stream>();
        try
        {
            var items = new List<PhotoUploadItem>(form.Files.Count);
            for (int i = 0; i < form.Files.Count; i++)
            {
                var file = form.Files[i];
                var stream = file.OpenReadStream();
                streams.Add(stream);
                bool is360 = i < form.Is360.Count && form.Is360[i];
                items.Add(new PhotoUploadItem(file.FileName, file.Length, stream, is360));
            }

            return Ok(await photoService.UploadAsync(listingId, items, ct));
        }
        catch (PhotoValidationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "Ảnh không hợp lệ");
        }
        catch (ListingNotFoundException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound, title: "Không tìm thấy phòng");
        }
        finally
        {
            foreach (var s in streams) await s.DisposeAsync();
        }
    }

    /// <summary>Lấy bộ sưu tập ảnh của phòng, sắp theo thứ tự hiển thị (US03 T03.1).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PhotoGalleryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PhotoGalleryResponse>> GetGallery(string listingId, CancellationToken ct)
    {
        try
        {
            return Ok(await photoService.GetGalleryAsync(listingId, ct));
        }
        catch (PhotoValidationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "Yêu cầu không hợp lệ");
        }
        catch (ListingNotFoundException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound, title: "Không tìm thấy phòng");
        }
    }
}
