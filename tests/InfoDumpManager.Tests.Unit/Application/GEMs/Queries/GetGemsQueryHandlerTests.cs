using System;
using System.Collections.Generic;
using System.Linq;
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

public class GetGemsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsPaginatedResponse()
    {
        var first = GEM.Create(GEMSource.Create("https://example.com/first"), "First Title");
        first.AssignCategory(Guid.NewGuid());
        var second = GEM.Create(GEMSource.Create("https://example.com/second"), "Second Title");
        var items = new List<GEM> { first, second };
        const int page = 2;
        const int pageSize = 5;
        const int total = 42;

        var repository = new Mock<IGEMRepository>();
        repository
            .Setup(x => x.GetPagedAsync(page, pageSize, CancellationToken.None))
            .ReturnsAsync(((IReadOnlyList<GEM>)items, total));

        var handler = new GetGemsQueryHandler(repository.Object, CreateMapper());
        var result = await handler.Handle(new GetGemsQuery(page, pageSize), CancellationToken.None);

        result.Should().NotBeNull();
        result.Page.Should().Be(page);
        result.PageSize.Should().Be(pageSize);
        result.Total.Should().Be(total);
        result.Items.Should().HaveCount(items.Count);
        result.Items.Select(x => x.Id).Should().Equal(items.Select(i => i.Id));
        result.Items.Select(x => x.Title).Should().Equal(items.Select(i => i.Title));
        repository.Verify(x => x.GetPagedAsync(page, pageSize, CancellationToken.None), Times.Once);
    }

    private static IMapper CreateMapper()
        => new MapperConfiguration(cfg => cfg.AddProfile<ApplicationMappingProfile>()).CreateMapper();
}
