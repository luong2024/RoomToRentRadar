namespace RoomToRentRadar.Models
{
    /// <summary>
    /// Represents a registered user account (Student, Landlord, or ICTU Admin) for US01.
    /// </summary>
    public class UserAccount
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Role { get; set; } = "Student"; // Student, Landlord, Admin
        public string SelectedCampus { get; set; } = "ICTU";
        public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("O");
    }
}
