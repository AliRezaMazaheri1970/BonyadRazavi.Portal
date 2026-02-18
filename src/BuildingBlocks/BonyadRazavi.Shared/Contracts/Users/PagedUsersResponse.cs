namespace BonyadRazavi.Shared.Contracts.Users;

public sealed class PagedUsersResponse
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public List<UserDto> Items { get; set; } = [];
}
