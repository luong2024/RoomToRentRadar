using System.ComponentModel.DataAnnotations;

namespace RoomToRentRadar.Api.Models;

/// <summary>
/// Request multipart/form-data cho upload ảnh.
/// Field "files" lặp lại cho từng ảnh; field "is360" lặp lại theo cùng thứ tự (thiếu = false).
/// </summary>
public class PhotoUploadForm
{
    [Required]
    public List<IFormFile> Files { get; set; } = [];

    public List<bool> Is360 { get; set; } = [];
}
