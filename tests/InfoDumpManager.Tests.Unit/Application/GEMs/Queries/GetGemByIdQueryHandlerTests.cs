using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using FluentAssertions;
using InfoDumpManager.Application.Common;
using InfoDumpManager.Application.GEMs.Dtos;
using InfoDumpManager.Application.GEMs.Queries;
using InfoDumpManager.Application.GEMs.Queries.Handlers;
using InfoDumpManager.Domain.Entities;
using InfoDumpManager.Domain.Repositories;
using InfoDumpManager.Domain.ValueObjects;
using Moq;
using Xunit;

namespace InfoDumpManager.Tests.Unit.Application.GEMs.Queries;

public class GetGemByIdQueryHandlerTests
{
    [Fact]
    public async Task Handle_ExistingId_ReturnsGemDto()
    {
        var gem = GEM.Create(GEMSource.Create("https://example.com"), "Sample Title");
        var categoryId = Guid.NewGuid();
        gem.AssignCategory(categoryId);

        var repository = new Mock<IGEMRepository>();
        repository
            .Setup(x => x.GetByIdAsync(gem.Id, CancellationToken.None))
            .ReturnsAsync(gem);

        var handler = new GetGemByIdQueryHandler(repository.Object, CreateMapper());
        var result = await handler.Handle(new GetGemByIdQuery(gem.Id), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(gem.Id);
        result.Title.Should().Be("Sample Title");
        result.SourceUrl.Should().Be(gem.Source.Url);
        result.CategoryIds.Should().BeEquivalentTo(new[] { categoryId });
        repository.Verify(x => x.GetByIdAsync(gem.Id, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Handle_NonExistingId_ReturnsNull()
    {
        var repository = new Mock<IGEMRepository>();
        repository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), CancellationToken.None))
            .ReturnsAsync((GEM?)null);

        var handler = new GetGemByIdQueryHandler(repository.Object, CreateMapper());
        var result = await handler.Handle(new GetGemByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.Should().BeNull();
        repository.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), CancellationToken.None), Times.Once);
    }

    private static IMapper CreateMapper()
        => new MapperConfiguration(cfg => cfg.AddProfile<ApplicationMappingProfile>()).CreateMapper();
}
