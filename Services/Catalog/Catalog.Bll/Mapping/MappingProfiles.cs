using AutoMapper;
using Catalog.Bll.Dtos;
using Catalog.Domain.Entities;

namespace Catalog.Bll.Mapping;

public class ProductProfile : Profile
{
    public ProductProfile()
    {
        CreateMap<Product, ProductDto>();

        CreateMap<Product, ProductListItemDto>()
            .ForCtorParam(nameof(ProductListItemDto.BrandName), o => o.MapFrom(s => s.Brand.Name))
            .ForCtorParam(nameof(ProductListItemDto.Categories),
                o => o.MapFrom(s => s.ProductCategories.Select(pc => pc.Category.Name).ToList()));

        CreateMap<Product, ProductDetailsDto>()
            .ForCtorParam(nameof(ProductDetailsDto.Categories), o => o.MapFrom(s => s.ProductCategories));

        CreateMap<ProductDetail, ProductDetailDto>();
        CreateMap<ProductImage, ProductImageDto>();
        CreateMap<ProductCategory, ProductCategoryDto>()
            .ForCtorParam(nameof(ProductCategoryDto.CategoryName), o => o.MapFrom(s => s.Category.Name));
    }
}

public class CatalogReferenceProfile : Profile
{
    public CatalogReferenceProfile()
    {
        CreateMap<Brand, BrandDto>();
        CreateMap<Category, CategoryDto>();

        CreateMap<ProductCategory, CategoryProductDto>()
            .ForCtorParam(nameof(CategoryProductDto.Name), o => o.MapFrom(s => s.Product.Name))
            .ForCtorParam(nameof(CategoryProductDto.Price), o => o.MapFrom(s => s.Product.Price));
        CreateMap<Category, CategoryWithProductsDto>()
            .ForCtorParam(nameof(CategoryWithProductsDto.Products), o => o.MapFrom(s => s.ProductCategories));
    }
}
