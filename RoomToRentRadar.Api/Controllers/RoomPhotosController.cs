using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Api.DTOs;
using RoomToRentRadar.Api.Services;
using RoomToRentRadar.Api.Services.Photos;

namespace RoomToRentRadar.Api.Controllers;

[ApiController]
[Route("api/rooms/{roomId}/photos")]
[Produces("application/json")]
public class RoomPhotosController(IPhotoService photoService) : ControllerBase
{
    private string BaseUrl => $"{Request.Scheme}://{Request.Host}";

    /// <summary>Tải lên một hoặc nhiều ảnh (JPEG/PNG, kể cả ảnh 360°) cho phòng trọ.</summary>
    /// <remarks>
    /// multipart/form-data: <c>files</c> (nhiều tệp), <c>is360</c> (true/false).
    /// Ảnh được kiểm tra, xoay đúng chiều, resize, nén JPEG rồi mới lưu.
    /// </remarks>
    /// <response code="200">Upload thành công, trả về thông tin các ảnh.</response>
    /// <response code="400">Tệp không hợp lệ (sai định dạng, quá lớn, quá nhiều tệp, ...).</response>
    /// <response code="404">Phòng trọ không tồn tại.</response>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(110 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 110 * 1024 * 1024)]
    [ProducesResponseType(typeof(UploadPhotosResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UploadPhotosResponse>> Upload(
        string roomId,
        [FromForm] UploadPhotosRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await photoService.UploadAsync(roomId, request, BaseUrl, cancellationToken));
        }
        catch (PhotoValidationException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "Ảnh không hợp lệ");
        }
        catch (RoomNotFoundException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status404NotFound, title: "Không tìm thấy phòng");
        }
    }

    /// <summary>Lấy danh sách ảnh (gallery) của một phòng trọ.</summary>
    /// <response code="200">Danh sách ảnh (có thể rỗng).</response>
    /// <response code="404">Phòng trọ không tồn tại.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PhotoGalleryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PhotoGalleryResponse>> GetGallery(string roomId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await photoService.GetGalleryAsync(roomId, BaseUrl, cancellationToken));
        }
        catch (PhotoValidationException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "RoomId không hợp lệ");
        }
        catch (RoomNotFoundException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status404NotFound, title: "Không tìm thấy phòng");
        }
    }
}
