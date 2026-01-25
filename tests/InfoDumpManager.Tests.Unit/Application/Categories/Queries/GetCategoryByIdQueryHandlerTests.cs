using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using FluentAssertions;
using InfoDumpManager.Application.Categories.Queries;
using InfoDumpManager.Application.Categories.Queries.Handlers;
using InfoDumpManager.Application.Common;
using InfoDumpManager.Domain.Entities;
using InfoDumpManager.Domain.Repositories;
using Moq;
using Xunit;

namespace InfoDumpManager.Tests.Unit.Application.Categories.Queries;

public class GetCategoryByIdQueryHandlerTests
{
    [Fact]
    public async Task Handle_ExistingId_ReturnsCategoryDto()
    {
        var category = Category.Create("Reader", "Packed details");

        var repository = new Mock<ICategoryRepository>();
        repository
            .Setup(x => x.GetByIdAsync(category.Id, CancellationToken.None))
            .ReturnsAsync(category);

        var handler = new GetCategoryByIdQueryHandler(repository.Object, CreateMapper());
        var result = await handler.Handle(new GetCategoryByIdQuery(category.Id), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(category.Id);
        result.Name.Should().Be("Reader");
        result.Description.Should().Be("Packed details");
        repository.Verify(x => x.GetByIdAsync(category.Id, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Handle_NonExistingId_ReturnsNull()
    {
        var repository = new Mock<ICategoryRepository>();
        repository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), CancellationToken.None))
            .ReturnsAsync((Category?)null);

        var handler = new GetCategoryByIdQueryHandler(repository.Object, CreateMapper());
        var result = await handler.Handle(new GetCategoryByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.Should().BeNull();
        repository.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), CancellationToken.None), Times.Once);
    }

    private static IMapper CreateMapper()
        => new MapperConfiguration(cfg => cfg.AddProfile<ApplicationMappingProfile>()).CreateMapper();
}
