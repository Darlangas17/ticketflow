namespace TicketFlow.Application.Catalogs;

public sealed record BranchDto(int Id, string Name, string City, bool IsActive);

public sealed record CreateBranchRequest(string Name, string City);

/// <param name="IsActive">Inactive branches cannot receive new tickets but keep their history.</param>
public sealed record UpdateBranchRequest(string Name, string City, bool IsActive);

public sealed record CategoryDto(int Id, string Name, string? Description);

/// <summary>Body of both POST and PUT /api/categories.</summary>
public sealed record SaveCategoryRequest(string Name, string? Description);
