using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using FluentAssertions;
using InfoDumpManager.Application.Categories.Commands;
using InfoDumpManager.Application.Categories.Commands.Handlers;
using InfoDumpManager.Application.Categories.Dtos;
using InfoDumpManager.Domain.Common;
using InfoDumpManager.Domain.Entities;
using InfoDumpManager.Domain.Repositories;
using Moq;
using Xunit;

namespace InfoDumpManager.Tests.Unit.Application.Categories.Commands;

public class CreateCategoryCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidCommand_CreatesCategory()
    {
        var repository = new Mock<ICategoryRepository>();
        Category? capturedCategory = null;
        repository
            .Setup(x => x.AddAsync(It.IsAny<Category>(), CancellationToken.None))
            .Callback<Category, CancellationToken>((category, _) => capturedCategory = category)
            .Returns(Task.CompletedTask);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork
            .Setup(x => x.SaveChangesAsync(CancellationToken.None))
            .ReturnsAsync(1);

        var handler = new CreateCategoryCommandHandler(repository.Object, unitOfWork.Object, CreateMapper());
        await handler.Handle(new CreateCategoryCommand(" Knowledge ", " Details "), CancellationToken.None);

        capturedCategory.Should().NotBeNull();
        capturedCategory!.Name.Should().Be("Knowledge");
        capturedCategory.Description.Should().Be("Details");
        repository.Verify(x => x.AddAsync(capturedCategory, CancellationToken.None), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsCategoryDto()
    {
        var repository = new Mock<ICategoryRepository>();
        repository
            .Setup(x => x.AddAsync(It.IsAny<Category>(), CancellationToken.None))
            .Returns(Task.CompletedTask);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork
            .Setup(x => x.SaveChangesAsync(CancellationToken.None))
            .ReturnsAsync(1);

        var handler = new CreateCategoryCommandHandler(repository.Object, unitOfWork.Object, CreateMapper());

        var result = await handler.Handle(new CreateCategoryCommand("Knowledge", "Details"), CancellationToken.None);

        result.Name.Should().Be("Knowledge");
        result.Description.Should().Be("Details");
        result.Id.Should().NotBe(Guid.Empty);
        result.GemIds.Should().BeEmpty();
    }

    private static IMapper CreateMapper()
        => new MapperConfiguration(cfg => cfg.CreateMap<Category, CategoryDto>()).CreateMapper();
}
