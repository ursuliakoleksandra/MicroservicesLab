using AutoMapper;
using MongoDB.Bson;
using Reviews.Application.Discussions;
using Reviews.Application.Reviews;
using Reviews.Domain.Entities;
using Reviews.Domain.Enums;

namespace Reviews.Application.Common.Mappings;

/// <summary>Профіль AutoMapper: ObjectId -> string, Value Objects -> прості поля, вкладені масиви -> списки DTO.</summary>
public class ReviewProfile : Profile
{
    public ReviewProfile()
    {
        CreateMap<ObjectId, string>().ConvertUsing(id => id.ToString());

        CreateMap<Comment, CommentDto>();

        CreateMap<Review, ReviewDto>()
            .ForMember(d => d.Id, o => o.MapFrom(s => s.Id.ToString()))
            .ForMember(d => d.AuthorId, o => o.MapFrom(s => s.Author.CustomerId))
            .ForMember(d => d.AuthorName, o => o.MapFrom(s => s.Author.DisplayName))
            .ForMember(d => d.Type, o => o.MapFrom(s => s.Type.ToCode()))
            .ForMember(d => d.Rating, o => o.MapFrom(s => s.Rating.Value))
            .ForMember(d => d.OrderId, o => o.MapFrom(s => s.VerifiedPurchase == null ? (long?)null : s.VerifiedPurchase.OrderId))
            .ForMember(d => d.DiscussionId, o => o.MapFrom(s => s.DiscussionId.HasValue ? s.DiscussionId.Value.ToString() : null))
            .ForMember(d => d.CommentsCount, o => o.MapFrom(s => s.Comments.Count))
            .ForMember(d => d.Comments, o => o.MapFrom(s => s.Comments));

        CreateMap<Discussion, DiscussionDto>()
            .ForMember(d => d.Id, o => o.MapFrom(s => s.Id.ToString()));
    }
}
