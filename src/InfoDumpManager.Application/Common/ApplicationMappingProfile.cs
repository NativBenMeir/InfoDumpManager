using AutoMapper;
using InfoDumpManager.Application.Categories.Dtos;
using InfoDumpManager.Application.GEMs.Dtos;
using InfoDumpManager.Domain.Entities;

namespace InfoDumpManager.Application.Common;

public sealed class ApplicationMappingProfile : Profile
{
    public ApplicationMappingProfile()
    {
        CreateMap<GEM, GemDto>()
            .ForMember(dest => dest.CategoryIds, opt => opt.MapFrom(src => src.CategoryIds));

        CreateMap<Category, CategoryDto>()
            .ForMember(dest => dest.GemIds, opt => opt.MapFrom(src => src.GemIds));
    }
}
