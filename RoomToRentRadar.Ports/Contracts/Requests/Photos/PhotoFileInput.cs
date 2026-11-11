namespace RoomToRentRadar.Ports.Contracts.Requests.Photos;

public sealed record PhotoFileInput(string FileName, Stream Content, long Length);
