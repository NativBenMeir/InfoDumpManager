namespace InfoDumpManager.WebAPI.Contracts.Categories;

public sealed class UpdateCategoryRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}
