using System.Collections.Generic;
using System.Threading.Tasks;

namespace RoomToRentRadar.Services;

/// <summary>
/// Trừu tượng điều hướng để ViewModel thuần C# giữ khả năng test độc lập trên net10.0.
/// </summary>
public interface INavigationService
{
    Task GoToAsync(string route, IDictionary<string, object>? parameters = null);

    Task GoBackAsync();
}
