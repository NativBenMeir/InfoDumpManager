using System.Collections.Generic;
using System.Linq;
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

public class GetCategoriesQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsAllCategories()
    {
        var categories = new List<Category>
        {
            Category.Create("First", "Description"),
            Category.Create("Second")
        };

        var repository = new Mock<ICategoryRepository>();
        repository
            .Setup(x => x.GetAllAsync(CancellationToken.None))
            .ReturnsAsync(categories);

        var handler = new GetCategoriesQueryHandler(repository.Object, CreateMapper());
        var result = await handler.Handle(new GetCategoriesQuery(), CancellationToken.None);

        result.Should().HaveCount(categories.Count);
        result.Select(x => x.Name).Should().BeEquivalentTo(categories.Select(x => x.Name));
        repository.Verify(x => x.GetAllAsync(CancellationToken.None), Times.Once);
    }

    private static IMapper CreateMapper()
        => new MapperConfiguration(cfg => cfg.AddProfile<ApplicationMappingProfile>()).CreateMapper();
}
