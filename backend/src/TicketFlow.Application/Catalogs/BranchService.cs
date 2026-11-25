using FluentValidation;
using TicketFlow.Application.Abstractions;
using TicketFlow.Application.Common.Exceptions;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Application.Catalogs;

public interface IBranchService
{
    Task<IReadOnlyList<BranchDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default);

    Task<BranchDto> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<BranchDto> CreateAsync(CreateBranchRequest request, CancellationToken cancellationToken = default);

    Task<BranchDto> UpdateAsync(int id, UpdateBranchRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}

public sealed class BranchService(
    IBranchRepository branches,
    IValidator<CreateBranchRequest> createValidator,
    IValidator<UpdateBranchRequest> updateValidator) : IBranchService
{
    public Task<IReadOnlyList<BranchDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default) =>
        branches.ListAsync(includeInactive, cancellationToken);

    public async Task<BranchDto> GetAsync(int id, CancellationToken cancellationToken = default) =>
        ToDto(await FindAsync(id, cancellationToken));

    public async Task<BranchDto> CreateAsync(CreateBranchRequest request, CancellationToken cancellationToken = default)
    {
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);
        var name = request.Name.Trim();
        await EnsureNameIsFreeAsync(name, excludeId: null, cancellationToken);

        var branch = new Branch { Name = name, City = request.City.Trim(), IsActive = true };
        branches.Add(branch);
        await branches.SaveChangesAsync(cancellationToken);
        return ToDto(branch);
    }

    public async Task<BranchDto> UpdateAsync(int id, UpdateBranchRequest request, CancellationToken cancellationToken = default)
    {
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);
        var branch = await FindAsync(id, cancellationToken);
        var name = request.Name.Trim();
        await EnsureNameIsFreeAsync(name, excludeId: id, cancellationToken);

        branch.Name = name;
        branch.City = request.City.Trim();
        branch.IsActive = request.IsActive;
        await branches.SaveChangesAsync(cancellationToken);
        return ToDto(branch);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var branch = await FindAsync(id, cancellationToken);
        if (await branches.HasTicketsAsync(id, cancellationToken))
        {
            throw new ConflictException("The branch has tickets and cannot be deleted. Deactivate it instead.");
        }

        branches.Remove(branch);
        await branches.SaveChangesAsync(cancellationToken);
    }

    private async Task<Branch> FindAsync(int id, CancellationToken cancellationToken) =>
        await branches.GetForUpdateAsync(id, cancellationToken)
        ?? throw new NotFoundException($"Branch {id} was not found.");

    private async Task EnsureNameIsFreeAsync(string name, int? excludeId, CancellationToken cancellationToken)
    {
        if (await branches.NameExistsAsync(name, excludeId, cancellationToken))
        {
            throw new ConflictException($"A branch named '{name}' already exists.");
        }
    }

    private static BranchDto ToDto(Branch branch) => new(branch.Id, branch.Name, branch.City, branch.IsActive);
}
