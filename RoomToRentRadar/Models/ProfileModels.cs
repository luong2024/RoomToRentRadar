using System.Windows.Input;

namespace RoomToRentRadar.Models;

public enum ProfileVerificationStatus
{
    Unverified,
    Pending,
    Verified
}

public sealed class ProfileInfoRow(string label, string value)
{
    public string Label { get; } = label;

    public string Value { get; } = value;
}

public sealed class ProfileVerificationItem(string title, ProfileVerificationStatus status)
{
    public string Title { get; } = title;

    public ProfileVerificationStatus Status { get; } = status;

    public bool IsVerified => Status == ProfileVerificationStatus.Verified;

    public bool IsPending => Status == ProfileVerificationStatus.Pending;

    public bool IsUnverified => Status == ProfileVerificationStatus.Unverified;
}

public sealed class ProfileRoomStats
{
    public int Total { get; set; }

    public int Available { get; set; }

    public int Occupied { get; set; }

    public int Pending { get; set; }

    public ProfileRoomStats Clone() => new()
    {
        Total = Total,
        Available = Available,
        Occupied = Occupied,
        Pending = Pending
    };
}

public sealed class ProfileReputation
{
    public double AverageRating { get; set; }

    public int TotalReviews { get; set; }

    public int ResponseRatePercent { get; set; }

    public ProfileReputation Clone() => new()
    {
        AverageRating = AverageRating,
        TotalReviews = TotalReviews,
        ResponseRatePercent = ResponseRatePercent
    };
}

public sealed class StudentProfileDetails
{
    public string FullName { get; set; } = string.Empty;

    public string AvatarPath { get; set; } = string.Empty;

    public DateTime? DateOfBirth { get; set; }

    public string Gender { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string University { get; set; } = string.Empty;

    public string Major { get; set; } = string.Empty;

    public string AcademicYear { get; set; } = string.Empty;

    public string Budget { get; set; } = string.Empty;

    public string PreferredLocation { get; set; } = string.Empty;

    public string RoomType { get; set; } = string.Empty;

    public DateTime? MoveInDate { get; set; }

    public string RentalDuration { get; set; } = string.Empty;

    public string Occupants { get; set; } = string.Empty;

    public string GenderPreference { get; set; } = string.Empty;

    public bool HasPets { get; set; }

    public bool IsSmoker { get; set; }

    public string Introduction { get; set; } = string.Empty;
}

public sealed class LandlordProfileDetails
{
    public string FullName { get; set; } = string.Empty;

    public string AvatarPath { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string AccountType { get; set; } = string.Empty;

    public string BusinessName { get; set; } = string.Empty;

    public string ContactAddress { get; set; } = string.Empty;

    public string Introduction { get; set; } = string.Empty;

    public ProfileVerificationStatus PhoneVerification { get; set; }

    public ProfileVerificationStatus EmailVerification { get; set; }

    public ProfileVerificationStatus IdentityVerification { get; set; }

    public ProfileVerificationStatus PropertyOwnershipVerification { get; set; }

    public ProfileRoomStats RoomStats { get; set; } = new();

    public ProfileReputation Reputation { get; set; } = new();
}

public sealed class UserSettings
{
    public bool NotificationsEnabled { get; set; } = true;

    public bool NewListingsEnabled { get; set; } = true;

    public bool ShowPhoneEnabled { get; set; }

    public bool AllowMessagesEnabled { get; set; } = true;

    public UserSettings Clone() => new()
    {
        NotificationsEnabled = NotificationsEnabled,
        NewListingsEnabled = NewListingsEnabled,
        ShowPhoneEnabled = ShowPhoneEnabled,
        AllowMessagesEnabled = AllowMessagesEnabled
    };
}

public sealed class ProfileMenuItem(
    string title,
    string description,
    string icon,
    string automationId,
    ICommand command,
    bool isComingSoon = false)
{
    public string Title { get; } = title;

    public string Description { get; } = description;

    public string Icon { get; } = icon;

    public string AutomationId { get; } = automationId;

    public ICommand Command { get; } = command;

    public bool IsComingSoon { get; } = isComingSoon;
}
