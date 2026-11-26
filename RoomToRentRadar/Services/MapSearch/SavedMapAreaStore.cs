using System.Text.Json;
using RoomToRentRadar.Models.MapSearch;

namespace RoomToRentRadar.Services.MapSearch;

public sealed class SavedMapAreaStore(string path)
{
    private readonly SemaphoreSlim _gate = new(1);

    public async Task<IReadOnlyList<SavedMapArea>> LoadAsync(Guid accountId)
    {
        await _gate.WaitAsync();
        try
        {
            return await ReadAsync(GetAccountPath(accountId));
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task SaveAsync(Guid accountId, string name, GeoCoordinate[] boundary) =>
        UpdateAsync(accountId, areas => areas.Add(new SavedMapArea(Guid.NewGuid(), name.Trim(), boundary)));

    public Task RenameAsync(Guid accountId, Guid id, string name) => UpdateAsync(accountId, areas =>
    {
        int index = areas.FindIndex(area => area.Id == id);
        if (index >= 0)
            areas[index] = areas[index] with { Name = name.Trim() };
    });

    public Task DeleteAsync(Guid accountId, Guid id) =>
        UpdateAsync(accountId, areas => areas.RemoveAll(area => area.Id == id));

    private string GetAccountPath(Guid accountId)
    {
        if (accountId == Guid.Empty)
            throw new ArgumentException("A signed-in account is required.", nameof(accountId));

        return Path.ChangeExtension(path, $"{accountId:N}.json");
    }

    private static async Task<List<SavedMapArea>> ReadAsync(string accountPath)
    {
        try
        {
            using var stream = File.OpenRead(accountPath);
            return await JsonSerializer.DeserializeAsync<List<SavedMapArea>>(stream) ?? [];
        }
        catch (FileNotFoundException)
        {
            return [];
        }
        catch (DirectoryNotFoundException)
        {
            return [];
        }
    }

    private async Task UpdateAsync(Guid accountId, Action<List<SavedMapArea>> update)
    {
        string accountPath = GetAccountPath(accountId);
        await _gate.WaitAsync();
        try
        {
            var areas = await ReadAsync(accountPath);
            update(areas);

            var temporaryPath = accountPath + ".tmp";
            await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(areas));
            File.Move(temporaryPath, accountPath, overwrite: true);
        }
        finally
        {
            _gate.Release();
        }
    }
}
