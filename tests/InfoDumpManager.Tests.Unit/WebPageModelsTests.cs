using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using FluentAssertions;
using InfoDumpManager.Application.Categories.Commands;
using InfoDumpManager.Application.Categories.DTOs;
using InfoDumpManager.Application.Common.Services;
using InfoDumpManager.Application.GEMs.Commands;
using InfoDumpManager.Application.GEMs.DTOs;
using InfoDumpManager.Application.GEMs.Queries;
using InfoDumpManager.Application.Mappings;
using InfoDumpManager.Domain.Entities;
using InfoDumpManager.Domain.Repositories;
using InfoDumpManager.Domain.ValueObjects;
using InfoDumpManager.Infrastructure.Services;
using InfoDumpManager.Web.Pages;
using InfoDumpManager.Web.Pages.Categories;
using InfoDumpManager.Web.Pages.GEMs;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace InfoDumpManager.Tests.Unit;

public sealed class IndexPageModelTests
{
    [Fact]
    public async Task OnPostAsync_WithValidInput_RedirectsToDetail()
    {
        var mediator = new Mock<IMediator>();
        var scraper = new Mock<IWebScrapingService>();
        var logger = new Mock<ILogger<IndexModel>>();
        var gemId = Guid.NewGuid();
        var scrapeResult = new WebScrapeResult(
            "https://example.com/article",
            "Article Title",
            "<html><body>content</body></html>",
            "text/html",
            DateTimeOffset.UtcNow);

        scraper.Setup(x => x.ScrapeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(scrapeResult);

        mediator.Setup(x => x.Send(It.IsAny<CreateGEMCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GEMDto { Id = gemId, Title = "Article Title", Url = scrapeResult.Url });

        var model = new IndexModel(logger.Object, mediator.Object, scraper.Object)
        {
            SourceUrl = scrapeResult.Url,
            PageContext = BuildPageContext()
        };

        var result = await model.OnPostAsync(CancellationToken.None);

        var redirect = result.Should().BeOfType<RedirectToPageResult>().Subject;
        redirect.PageName.Should().Be("/GEMs/Detail");
        redirect.RouteValues.Should().ContainKey("id");
    }

    [Fact]
    public async Task OnPostAsync_WithScrapeFailure_ReturnsPageWithError()
    {
        var mediator = new Mock<IMediator>();
        var scraper = new Mock<IWebScrapingService>();
        var logger = new Mock<ILogger<IndexModel>>();

        scraper.Setup(x => x.ScrapeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Failure"));

        var model = new IndexModel(logger.Object, mediator.Object, scraper.Object)
        {
            SourceUrl = "https://example.com",
            PageContext = BuildPageContext()
        };

        var result = await model.OnPostAsync(CancellationToken.None);

        result.Should().BeOfType<PageResult>();
        model.ModelState.Should().ContainKey(string.Empty);
    }

    [Fact]
    public async Task OnPostAsync_WithInvalidModel_ReturnsPage()
    {
        var mediator = new Mock<IMediator>();
        var scraper = new Mock<IWebScrapingService>();
        var logger = new Mock<ILogger<IndexModel>>();

        var model = new IndexModel(logger.Object, mediator.Object, scraper.Object)
        {
            SourceUrl = string.Empty,
            PageContext = BuildPageContext()
        };

        model.ModelState.AddModelError("SourceUrl", "Required");

        var result = await model.OnPostAsync(CancellationToken.None);

        result.Should().BeOfType<PageResult>();
        scraper.Verify(x => x.ScrapeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        mediator.Verify(x => x.Send(It.IsAny<CreateGEMCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static PageContext BuildPageContext()
    {
        return new PageContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }
}

public sealed class GemListPageModelTests
{
    [Fact]
    public async Task OnGetAsync_PaginatesGems()
    {
        var mapper = BuildMapper();
        var unitOfWork = new Mock<IUnitOfWork>();
        var currentUser = new Mock<ICurrentUserContext>();
        var tenantId = Guid.NewGuid();
        currentUser.Setup(x => x.TenantId).Returns(tenantId);

        var gemRepo = new Mock<IGEMRepository>();
        var categoryRepo = new Mock<ICategoryRepository>();

        var gems = new List<GEM>
        {
            CreateGem(tenantId, "Gem 1"),
            CreateGem(tenantId, "Gem 2"),
            CreateGem(tenantId, "Gem 3")
        };

        gemRepo.Setup(x => x.ListByTenantAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gems);
        categoryRepo.Setup(x => x.ListByTenantAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Category>());

        unitOfWork.Setup(x => x.GEMs).Returns(gemRepo.Object);
        unitOfWork.Setup(x => x.Categories).Returns(categoryRepo.Object);

        var model = new ListModel(unitOfWork.Object, mapper, currentUser.Object)
        {
            PageNumberQuery = 1,
            PageSizeQuery = 2
        };

        await model.OnGetAsync(CancellationToken.None);

        model.Gems.Should().HaveCount(2);
        model.Total.Should().Be(3);
        model.PageNumber.Should().Be(1);
    }

    [Fact]
    public async Task OnGetAsync_WithCategoryFilter_ReturnsFiltered()
    {
        var mapper = BuildMapper();
        var unitOfWork = new Mock<IUnitOfWork>();
        var currentUser = new Mock<ICurrentUserContext>();
        var tenantId = Guid.NewGuid();
        currentUser.Setup(x => x.TenantId).Returns(tenantId);

        var category = Category.Create(tenantId, "Filtered", Guid.NewGuid());
        var gems = new List<GEM> { CreateGem(tenantId, "Gem A") };

        var gemRepo = new Mock<IGEMRepository>();
        var categoryRepo = new Mock<ICategoryRepository>();

        categoryRepo.Setup(x => x.ListByTenantAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Category> { category });
        categoryRepo.Setup(x => x.GetByIdAsync(category.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);
        gemRepo.Setup(x => x.ListByCategoryAsync(category.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gems);

        unitOfWork.Setup(x => x.GEMs).Returns(gemRepo.Object);
        unitOfWork.Setup(x => x.Categories).Returns(categoryRepo.Object);

        var model = new ListModel(unitOfWork.Object, mapper, currentUser.Object)
        {
            CategoryId = category.Id
        };

        await model.OnGetAsync(CancellationToken.None);

        model.Gems.Should().HaveCount(1);
        model.Categories.Should().ContainSingle(c => c.Id == category.Id);
    }

    private static IMapper BuildMapper()
    {
        var config = new MapperConfiguration(cfg => cfg.AddProfile(new GEMMappingProfile()));
        return config.CreateMapper();
    }

    private static GEM CreateGem(Guid tenantId, string title)
    {
        var source = new GEMSource("https://example.com/source", "Source");
        var snapshot = new GEMSnapshot("<html></html>", "text/html", DateTimeOffset.UtcNow);
        return GEM.Create(tenantId, title, "https://example.com/gem", source, snapshot);
    }
}

public sealed class GemDetailPageModelTests
{
    [Fact]
    public async Task OnGetAsync_WhenGemMissing_ReturnsNotFound()
    {
        var mediator = new Mock<IMediator>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var mapper = BuildMapper();
        var currentUser = new Mock<ICurrentUserContext>();

        mediator.Setup(x => x.Send(It.IsAny<GetGEMByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GEMDto?)null);

        var model = new DetailModel(mediator.Object, unitOfWork.Object, mapper, currentUser.Object)
        {
            PageContext = BuildPageContext()
        };
        model.TempData = BuildTempData();

        var result = await model.OnGetAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task OnPostAssignCategoryAsync_WithMissingSelection_ReturnsPage()
    {
        var mediator = new Mock<IMediator>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var mapper = BuildMapper();
        var currentUser = new Mock<ICurrentUserContext>();
        var tenantId = Guid.NewGuid();
        currentUser.Setup(x => x.TenantId).Returns(tenantId);

        var categoryRepo = new Mock<ICategoryRepository>();
        categoryRepo.Setup(x => x.ListByTenantAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Category>());
        unitOfWork.Setup(x => x.Categories).Returns(categoryRepo.Object);

        mediator.Setup(x => x.Send(It.IsAny<GetGEMByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GEMDto { Id = Guid.NewGuid(), Title = "Gem" });

        var model = new DetailModel(mediator.Object, unitOfWork.Object, mapper, currentUser.Object)
        {
            PageContext = BuildPageContext()
        };
        model.TempData = BuildTempData();

        var result = await model.OnPostAssignCategoryAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<PageResult>();
        model.ModelState.Should().ContainKey(nameof(model.SelectedCategoryId));
    }

    [Fact]
    public async Task OnPostAssignCategoryAsync_WithValidSelection_Redirects()
    {
        var mediator = new Mock<IMediator>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var mapper = BuildMapper();
        var currentUser = new Mock<ICurrentUserContext>();
        var tenantId = Guid.NewGuid();
        currentUser.Setup(x => x.TenantId).Returns(tenantId);

        var categoryRepo = new Mock<ICategoryRepository>();
        categoryRepo.Setup(x => x.ListByTenantAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Category>());
        unitOfWork.Setup(x => x.Categories).Returns(categoryRepo.Object);

        mediator.Setup(x => x.Send(It.IsAny<AssignCategoryCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MediatR.Unit.Value);
        mediator.Setup(x => x.Send(It.IsAny<GetGEMByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GEMDto { Id = Guid.NewGuid(), Title = "Gem" });

        var model = new DetailModel(mediator.Object, unitOfWork.Object, mapper, currentUser.Object)
        {
            PageContext = BuildPageContext(),
            SelectedCategoryId = Guid.NewGuid()
        };
        model.TempData = BuildTempData();

        var result = await model.OnPostAssignCategoryAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<RedirectToPageResult>();
        mediator.Verify(x => x.Send(It.IsAny<AssignCategoryCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static IMapper BuildMapper()
    {
        var config = new MapperConfiguration(cfg => cfg.AddProfile(new GEMMappingProfile()));
        return config.CreateMapper();
    }

    private static PageContext BuildPageContext()
    {
        return new PageContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    private static ITempDataDictionary BuildTempData()
    {
        return new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>());
    }
}

public sealed class CategoryManagePageModelTests
{
    [Fact]
    public async Task OnPostCreateAsync_WithValidInput_Redirects()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var mapper = BuildMapper();
        var currentUser = new Mock<ICurrentUserContext>();
        var databasePolicy = new Mock<IDatabasePolicy>();
        var mediator = new Mock<IMediator>();

        mediator.Setup(x => x.Send(It.IsAny<CreateCategoryCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CategoryDto { Id = Guid.NewGuid(), Name = "New" });

        var model = new ManageModel(unitOfWork.Object, mapper, currentUser.Object, databasePolicy.Object, mediator.Object)
        {
            PageContext = BuildPageContextWithServices(),
            Create = new ManageModel.CreateCategoryInput { Name = "New" }
        };
        model.TempData = BuildTempData();

        var result = await model.OnPostCreateAsync(CancellationToken.None);

        result.Should().BeOfType<RedirectToPageResult>();
    }

    [Fact]
    public async Task OnPostUpdateAsync_WhenCategoryMissing_ReturnsNotFound()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var mapper = BuildMapper();
        var currentUser = new Mock<ICurrentUserContext>();
        var databasePolicy = new Mock<IDatabasePolicy>();
        var mediator = new Mock<IMediator>();

        var categoryRepo = new Mock<ICategoryRepository>();
        categoryRepo.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);

        unitOfWork.Setup(x => x.Categories).Returns(categoryRepo.Object);

        var model = new ManageModel(unitOfWork.Object, mapper, currentUser.Object, databasePolicy.Object, mediator.Object)
        {
            PageContext = BuildPageContextWithServices(),
            Edit = new ManageModel.EditCategoryInput { CategoryId = Guid.NewGuid(), Name = "Edit" }
        };
        model.TempData = BuildTempData();

        var result = await model.OnPostUpdateAsync(CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task OnPostDeleteAsync_WhenCategoryMissing_ReturnsNotFound()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var mapper = BuildMapper();
        var currentUser = new Mock<ICurrentUserContext>();
        var databasePolicy = new Mock<IDatabasePolicy>();
        var mediator = new Mock<IMediator>();

        var categoryRepo = new Mock<ICategoryRepository>();
        categoryRepo.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);
        unitOfWork.Setup(x => x.Categories).Returns(categoryRepo.Object);

        var model = new ManageModel(unitOfWork.Object, mapper, currentUser.Object, databasePolicy.Object, mediator.Object)
        {
            PageContext = BuildPageContextWithServices(),
            DeleteCategoryId = Guid.NewGuid()
        };
        model.TempData = BuildTempData();

        var result = await model.OnPostDeleteAsync(CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    private static IMapper BuildMapper()
    {
        var config = new MapperConfiguration(cfg => cfg.AddProfile(new GEMMappingProfile()));
        return config.CreateMapper();
    }

    private static PageContext BuildPageContext()
    {
        return new PageContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    private static ITempDataDictionary BuildTempData()
    {
        return new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>());
    }

    private static PageContext BuildPageContextWithServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMvc();
        var provider = services.BuildServiceProvider();

        return new PageContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = provider }
        };
    }
}
