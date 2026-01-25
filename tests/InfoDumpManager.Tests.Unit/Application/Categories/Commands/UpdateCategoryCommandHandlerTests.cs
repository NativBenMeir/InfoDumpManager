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

public class UpdateCategoryCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidCommand_UpdatesCategory()
    {
        var category = Category.Create("Original", "Old description");
        var repository = new Mock<ICategoryRepository>();
        repository
            .Setup(x => x.GetByIdAsync(category.Id, CancellationToken.None))
            .ReturnsAsync(category);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork
            .Setup(x => x.SaveChangesAsync(CancellationToken.None))
            .ReturnsAsync(1);

        var handler = new UpdateCategoryCommandHandler(repository.Object, unitOfWork.Object, CreateMapper());
        var result = await handler.Handle(new UpdateCategoryCommand(category.Id, "Updated", "Updated description"), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Updated");
        result.Description.Should().Be("Updated description");
        unitOfWork.Verify(x => x.SaveChangesAsync(CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Handle_CategoryNotFound_ReturnsNull()
    {
        var repository = new Mock<ICategoryRepository>();
        repository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), CancellationToken.None))
            .ReturnsAsync((Category?)null);

        var unitOfWork = new Mock<IUnitOfWork>();
        var handler = new UpdateCategoryCommandHandler(repository.Object, unitOfWork.Object, CreateMapper());

        var result = await handler.Handle(new UpdateCategoryCommand(Guid.NewGuid(), "Updated", "Desc"), CancellationToken.None);

        result.Should().BeNull();
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static IMapper CreateMapper()
        => new MapperConfiguration(cfg => cfg.CreateMap<Category, CategoryDto>()).CreateMapper();
}
