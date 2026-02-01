using AutoMapper;
using InfoDumpManager.Application.Categories.DTOs;
using InfoDumpManager.Application.GEMs.DTOs;
using InfoDumpManager.Domain.Entities;

namespace InfoDumpManager.Application.Mappings;

public sealed class GEMMappingProfile : Profile
{
    public GEMMappingProfile()
    {
        CreateMap<GEM, GEMDto>()
            .ForMember(dest => dest.SnapshotHtml, opt => opt.MapFrom(src => src.Snapshot.HtmlContent))
            .ForMember(dest => dest.SnapshotMimeType, opt => opt.MapFrom(src => src.Snapshot.MimeType))
            .ForMember(dest => dest.SnapshotCapturedAt, opt => opt.MapFrom(src => src.Snapshot.CapturedAt))
            .ForMember(dest => dest.SourceUrl, opt => opt.MapFrom(src => src.Source.Url))
            .ForMember(dest => dest.SourceTitle, opt => opt.MapFrom(src => src.Source.Title))
            .ForMember(dest => dest.SummaryText, opt => opt.MapFrom(src => src.Summary.Text))
            .ForMember(dest => dest.SummaryModel, opt => opt.MapFrom(src => src.Summary.Model))
            .ForMember(dest => dest.SummaryTokenCount, opt => opt.MapFrom(src => src.Summary.TokenCount))
            .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category != null ? src.Category.Name : null));

        CreateMap<Category, CategoryDto>()
            .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description))
            .ForMember(dest => dest.GemCount, opt => opt.MapFrom(src => src.Gems.Count));
    }
}
