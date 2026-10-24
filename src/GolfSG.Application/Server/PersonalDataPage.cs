namespace GolfSG.Application.Server;

public sealed record PersonalDataItem(string Id, string Title, string State, DateTimeOffset UpdatedAt, string Detail, double? Score = null);
public sealed record PersonalDataPage(string Section, IReadOnlyList<PersonalDataItem> Items, int Offset, bool HasMore);
public interface IPersonalData
{
    Task<PersonalDataPage> ReadAsync(string accountId, string section, int offset, CancellationToken cancellationToken);
}
