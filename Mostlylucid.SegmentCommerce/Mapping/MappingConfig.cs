using Mapster;
using Mostlylucid.SegmentCommerce.Data.Entities;
using Mostlylucid.SegmentCommerce.Dtos;

namespace Mostlylucid.SegmentCommerce.Mapping;

public class MappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        // Map UserEntity (with SellerProfile) to SellerDto
        config.NewConfig<UserEntity, SellerDto>()
            .Map(dest => dest.Id, src => src.Id)
            .Map(dest => dest.Name, src => src.SellerProfile != null ? src.SellerProfile.BusinessName : src.DisplayName)
            .Map(dest => dest.Rating, src => src.SellerProfile != null ? src.SellerProfile.Rating : 0)
            .Map(dest => dest.ReviewCount, src => src.SellerProfile != null ? src.SellerProfile.ReviewCount : 0)
            .Map(dest => dest.IsVerified, src => src.SellerProfile != null && src.SellerProfile.IsVerified)
            .Map(dest => dest.LogoUrl, src => src.SellerProfile != null ? src.SellerProfile.LogoUrl : src.AvatarUrl);

        config.NewConfig<ProductVariationEntity, ProductVariationDto>();

        config.NewConfig<TaxonomyNodeEntity, TaxonomyNodeDto>()
            .Map(dest => dest.Path, src => src.Path);

        config.NewConfig<StoreEntity, StoreDto>();

        config.NewConfig<ProductEntity, ProductDto>()
            .Map(dest => dest.Variations, src => src.Variations)
            .Map(dest => dest.Seller, src => src.Seller)
            .Map(dest => dest.Taxonomy, src => src.ProductTaxonomy.Select(pt => pt.TaxonomyNode))
            .Map(dest => dest.Stores, src => src.StoreProducts.Select(sp => sp.Store));
    }
}
