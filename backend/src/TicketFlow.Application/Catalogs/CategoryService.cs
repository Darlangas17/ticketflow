using FluentValidation;
using TicketFlow.Application.Abstractions;
using TicketFlow.Application.Common.Exceptions;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Application.Catalogs;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<CategoryDto> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<CategoryDto> CreateAsync(SaveCategoryRequest request, CancellationToken cancellationToken = default);

    Task<CategoryDto> UpdateAsync(int id, SaveCategoryRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}

public sealed class CategoryService(
    ICategoryRepository categories,
    IValidator<SaveCategoryRequest> validator) : ICategoryService
{
    public Task<IReadOnlyList<CategoryDto>> ListAsync(CancellationToken cancellationToken = default) =>
        categories.ListAsync(cancellationToken);

    public async Task<CategoryDto> GetAsync(int id, CancellationToken cancellationToken = default) =>
        ToDto(await FindAsync(id, cancellationToken));

    public async Task<CategoryDto> CreateAsync(SaveCategoryRequest request, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var name = request.Name.Trim();
        await EnsureNameIsFreeAsync(name, excludeId: null, cancellationToken);

        var category = new Category { Name = name, Description = NormalizeDescription(request.Description) };
        categories.Add(category);
        await categories.SaveChangesAsync(cancellationToken);
        return ToDto(category);
    }

    public async Task<CategoryDto> UpdateAsync(int id, SaveCategoryRequest request, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var category = await FindAsync(id, cancellationToken);
        var name = request.Name.Trim();
        await EnsureNameIsFreeAsync(name, excludeId: id, cancellationToken);

        category.Name = name;
        category.Description = NormalizeDescription(request.Description);
        await categories.SaveChangesAsync(cancellationToken);
        return ToDto(category);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var category = await FindAsync(id, cancellationToken);
        if (await categories.HasTicketsAsync(id, cancellationToken))
        {
            throw new ConflictException("The category has tickets and cannot be deleted.");
        }

        categories.Remove(category);
        await categories.SaveChangesAsync(cancellationToken);
    }

    private async Task<Category> FindAsync(int id, CancellationToken cancellationToken) =>
        await categories.GetForUpdateAsync(id, cancellationToken)
        ?? throw new NotFoundException($"Category {id} was not found.");

    private async Task EnsureNameIsFreeAsync(string name, int? excludeId, CancellationToken cancellationToken)
    {
        if (await categories.NameExistsAsync(name, excludeId, cancellationToken))
        {
            throw new ConflictException($"A category named '{name}' already exists.");
        }
    }

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    private static CategoryDto ToDto(Category category) => new(category.Id, category.Name, category.Description);
}
