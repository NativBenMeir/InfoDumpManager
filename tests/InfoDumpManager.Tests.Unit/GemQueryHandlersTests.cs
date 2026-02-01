using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using FluentAssertions;
using InfoDumpManager.Application.Common.Services;
using InfoDumpManager.Application.GEMs.DTOs;
using InfoDumpManager.Application.GEMs.Queries;
using InfoDumpManager.Application.Mappings;
using InfoDumpManager.Domain.Entities;
using InfoDumpManager.Domain.Repositories;
using InfoDumpManager.Domain.ValueObjects;
using Moq;
using Xunit;

namespace InfoDumpManager.Tests.Unit;

public sealed class GemQueryHandlersTests
{
    private readonly IMapper _mapper;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ICurrentUserContext> _mockUserContext;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public GemQueryHandlersTests()
    {
        var config = new MapperConfiguration(cfg => cfg.AddProfile(new GEMMappingProfile()));
        _mapper = config.CreateMapper();

        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockUserContext = new Mock<ICurrentUserContext>();

        _mockUserContext.Setup(x => x.TenantId).Returns(_tenantId);
        _mockUserContext.Setup(x => x.UserId).Returns(_userId);
    }

    #region GetGEMByIdQueryHandler Tests

    [Fact]
    public async Task GetGEMByIdQueryHandler_WithValidGemId_ReturnsMappedGem()
    {
        // Arrange
        var gemId = Guid.NewGuid();
        var gem = CreateTestGem(gemId, _tenantId);

        var mockRepository = new Mock<IGEMRepository>();
        mockRepository.Setup(r => r.GetByIdAsync(gemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gem);

        _mockUnitOfWork.Setup(uow => uow.GEMs).Returns(mockRepository.Object);

        var handler = new GetGEMByIdQueryHandler(_mockUnitOfWork.Object, _mockUserContext.Object, _mapper);
        var query = new GetGEMByIdQuery { GemId = gemId };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(gemId);
        result.Title.Should().Be(gem.Title);
        result.Url.Should().Be(gem.Url);
        mockRepository.Verify(r => r.GetByIdAsync(gemId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetGEMByIdQueryHandler_WithNonExistentGemId_ReturnsNull()
    {
        // Arrange
        var gemId = Guid.NewGuid();

        var mockRepository = new Mock<IGEMRepository>();
        mockRepository.Setup(r => r.GetByIdAsync(gemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GEM?)null);

        _mockUnitOfWork.Setup(uow => uow.GEMs).Returns(mockRepository.Object);

        var handler = new GetGEMByIdQueryHandler(_mockUnitOfWork.Object, _mockUserContext.Object, _mapper);
        var query = new GetGEMByIdQuery { GemId = gemId };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        mockRepository.Verify(r => r.GetByIdAsync(gemId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetGEMByIdQueryHandler_WithDifferentTenantGem_ReturnsNull()
    {
        // Arrange
        var gemId = Guid.NewGuid();
        var differentTenantId = Guid.NewGuid();
        var gem = CreateTestGem(gemId, differentTenantId);

        var mockRepository = new Mock<IGEMRepository>();
        mockRepository.Setup(r => r.GetByIdAsync(gemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gem);

        _mockUnitOfWork.Setup(uow => uow.GEMs).Returns(mockRepository.Object);

        var handler = new GetGEMByIdQueryHandler(_mockUnitOfWork.Object, _mockUserContext.Object, _mapper);
        var query = new GetGEMByIdQuery { GemId = gemId };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull("Tenant isolation should prevent access to other tenant's GEMs");
        mockRepository.Verify(r => r.GetByIdAsync(gemId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetGEMByIdQueryHandler_WithEmptyGemId_ReturnsNull()
    {
        // Arrange
        var mockRepository = new Mock<IGEMRepository>();
        mockRepository.Setup(r => r.GetByIdAsync(Guid.Empty, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GEM?)null);

        _mockUnitOfWork.Setup(uow => uow.GEMs).Returns(mockRepository.Object);

        var handler = new GetGEMByIdQueryHandler(_mockUnitOfWork.Object, _mockUserContext.Object, _mapper);
        var query = new GetGEMByIdQuery { GemId = Guid.Empty };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region ListGEMsQueryHandler Tests

    [Fact]
    public async Task ListGEMsQueryHandler_WithValidPagination_ReturnsCorrectPage()
    {
        // Arrange
        var gems = new List<GEM>
        {
            CreateTestGem(Guid.NewGuid(), _tenantId, "First GEM"),
            CreateTestGem(Guid.NewGuid(), _tenantId, "Second GEM"),
            CreateTestGem(Guid.NewGuid(), _tenantId, "Third GEM")
        };

        var mockRepository = new Mock<IGEMRepository>();
        mockRepository.Setup(r => r.ListByTenantAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gems);

        _mockUnitOfWork.Setup(uow => uow.GEMs).Returns(mockRepository.Object);

        var handler = new ListGEMsQueryHandler(_mockUnitOfWork.Object, _mockUserContext.Object, _mapper);
        var query = new ListGEMsQuery { PageNumber = 1, PageSize = 2 };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.PageNumber.Should().Be(1);
        result.PageSize.Should().Be(2);
        result.Total.Should().Be(3);
        mockRepository.Verify(r => r.ListByTenantAsync(_tenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListGEMsQueryHandler_WithLastPage_ReturnsRemainingItems()
    {
        // Arrange
        var gems = new List<GEM>
        {
            CreateTestGem(Guid.NewGuid(), _tenantId, "First GEM"),
            CreateTestGem(Guid.NewGuid(), _tenantId, "Second GEM"),
            CreateTestGem(Guid.NewGuid(), _tenantId, "Third GEM")
        };

        var mockRepository = new Mock<IGEMRepository>();
        mockRepository.Setup(r => r.ListByTenantAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gems);

        _mockUnitOfWork.Setup(uow => uow.GEMs).Returns(mockRepository.Object);

        var handler = new ListGEMsQueryHandler(_mockUnitOfWork.Object, _mockUserContext.Object, _mapper);
        var query = new ListGEMsQuery { PageNumber = 2, PageSize = 2 };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.PageNumber.Should().Be(2);
        result.PageSize.Should().Be(2);
        result.Total.Should().Be(3);
    }

    [Fact]
    public async Task ListGEMsQueryHandler_WithEmptyResult_ReturnsEmptyCollection()
    {
        // Arrange
        var mockRepository = new Mock<IGEMRepository>();
        mockRepository.Setup(r => r.ListByTenantAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GEM>());

        _mockUnitOfWork.Setup(uow => uow.GEMs).Returns(mockRepository.Object);

        var handler = new ListGEMsQueryHandler(_mockUnitOfWork.Object, _mockUserContext.Object, _mapper);
        var query = new ListGEMsQuery { PageNumber = 1, PageSize = 20 };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
        result.PageNumber.Should().Be(1);
    }

    [Fact]
    public async Task ListGEMsQueryHandler_WithZeroPageNumber_CoercesToPageOne()
    {
        // Arrange
        var gems = new List<GEM> { CreateTestGem(Guid.NewGuid(), _tenantId) };

        var mockRepository = new Mock<IGEMRepository>();
        mockRepository.Setup(r => r.ListByTenantAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gems);

        _mockUnitOfWork.Setup(uow => uow.GEMs).Returns(mockRepository.Object);

        var handler = new ListGEMsQueryHandler(_mockUnitOfWork.Object, _mockUserContext.Object, _mapper);
        var query = new ListGEMsQuery { PageNumber = 0, PageSize = 20 };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.PageNumber.Should().Be(1, "PageNumber should coerce to minimum of 1");
    }

    [Fact]
    public async Task ListGEMsQueryHandler_WithNegativePageNumber_CoercesToPageOne()
    {
        // Arrange
        var gems = new List<GEM> { CreateTestGem(Guid.NewGuid(), _tenantId) };

        var mockRepository = new Mock<IGEMRepository>();
        mockRepository.Setup(r => r.ListByTenantAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gems);

        _mockUnitOfWork.Setup(uow => uow.GEMs).Returns(mockRepository.Object);

        var handler = new ListGEMsQueryHandler(_mockUnitOfWork.Object, _mockUserContext.Object, _mapper);
        var query = new ListGEMsQuery { PageNumber = -5, PageSize = 20 };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.PageNumber.Should().Be(1, "PageNumber should coerce to minimum of 1");
    }

    [Fact]
    public async Task ListGEMsQueryHandler_WithZeroPageSize_CoercesToPageSizeOne()
    {
        // Arrange
        var gems = new List<GEM> { CreateTestGem(Guid.NewGuid(), _tenantId) };

        var mockRepository = new Mock<IGEMRepository>();
        mockRepository.Setup(r => r.ListByTenantAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gems);

        _mockUnitOfWork.Setup(uow => uow.GEMs).Returns(mockRepository.Object);

        var handler = new ListGEMsQueryHandler(_mockUnitOfWork.Object, _mockUserContext.Object, _mapper);
        var query = new ListGEMsQuery { PageNumber = 1, PageSize = 0 };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(1);
        result.PageSize.Should().Be(1, "PageSize should coerce to minimum of 1");
    }

    [Fact]
    public async Task ListGEMsQueryHandler_WithMultiTenant_OnlyReturnsCurrentTenantGems()
    {
        // Arrange
        var currentTenantGems = new List<GEM>
        {
            CreateTestGem(Guid.NewGuid(), _tenantId, "Current Tenant Gem 1"),
            CreateTestGem(Guid.NewGuid(), _tenantId, "Current Tenant Gem 2")
        };

        var mockRepository = new Mock<IGEMRepository>();
        mockRepository.Setup(r => r.ListByTenantAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentTenantGems);

        _mockUnitOfWork.Setup(uow => uow.GEMs).Returns(mockRepository.Object);

        var handler = new ListGEMsQueryHandler(_mockUnitOfWork.Object, _mockUserContext.Object, _mapper);
        var query = new ListGEMsQuery { PageNumber = 1, PageSize = 20 };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(2);
        result.Total.Should().Be(2);
        mockRepository.Verify(r => r.ListByTenantAsync(_tenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListGEMsQueryHandler_SortsByCreatedAtDescending()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var gems = new List<GEM>
        {
            CreateTestGem(Guid.NewGuid(), _tenantId, "First", now.AddHours(-2)),
            CreateTestGem(Guid.NewGuid(), _tenantId, "Second", now.AddHours(-1)),
            CreateTestGem(Guid.NewGuid(), _tenantId, "Third", now)
        };

        var mockRepository = new Mock<IGEMRepository>();
        mockRepository.Setup(r => r.ListByTenantAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gems);

        _mockUnitOfWork.Setup(uow => uow.GEMs).Returns(mockRepository.Object);

        var handler = new ListGEMsQueryHandler(_mockUnitOfWork.Object, _mockUserContext.Object, _mapper);
        var query = new ListGEMsQuery { PageNumber = 1, PageSize = 20 };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(3);
        var items = result.Items.ToList();
        items[0].Title.Should().Be("Third", "Most recent should be first");
        items[1].Title.Should().Be("Second");
        items[2].Title.Should().Be("First", "Oldest should be last");
    }

    [Fact]
    public async Task ListGEMsQueryHandler_WithBeyondLastPageNumber_ReturnsEmptyItems()
    {
        // Arrange
        var gems = new List<GEM>
        {
            CreateTestGem(Guid.NewGuid(), _tenantId),
            CreateTestGem(Guid.NewGuid(), _tenantId)
        };

        var mockRepository = new Mock<IGEMRepository>();
        mockRepository.Setup(r => r.ListByTenantAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gems);

        _mockUnitOfWork.Setup(uow => uow.GEMs).Returns(mockRepository.Object);

        var handler = new ListGEMsQueryHandler(_mockUnitOfWork.Object, _mockUserContext.Object, _mapper);
        var query = new ListGEMsQuery { PageNumber = 100, PageSize = 20 };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Items.Should().BeEmpty();
        result.Total.Should().Be(2);
        result.PageNumber.Should().Be(100);
    }

    #endregion

    #region Helper Methods

    private static GEM CreateTestGem(Guid gemId, Guid tenantId, string title = "Test GEM", DateTimeOffset? createdAt = null)
    {
        var source = new GEMSource("https://source.example.com", "Source Title");
        var snapshot = new GEMSnapshot("<html><body>Test</body></html>", "text/html", DateTimeOffset.UtcNow);
        
        var gem = GEM.Create(tenantId, title, "https://example.com/test", source, snapshot);
        
        // Use reflection to set the Id for testing if needed
        var idProperty = typeof(GEM).GetProperty("Id");
        idProperty?.SetValue(gem, gemId);

        return gem;
    }

    #endregion
}
