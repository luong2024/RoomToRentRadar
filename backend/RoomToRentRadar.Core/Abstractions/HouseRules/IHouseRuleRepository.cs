using RoomToRentRadar.Models;

namespace RoomToRentRadar.Abstractions.HouseRules
{
    /// <summary>
    /// Contract for US12 Versioned House Rules. Implemented in T12.1. Consumed in T12.2.
    /// </summary>
    public interface IHouseRuleRepository
    {
        Task<List<HouseRuleVersion>> GetHistoryByRoomAsync(int roomId);
        Task<int> SaveNewVersionAsync(HouseRuleVersion ruleVersion);
    }
}
