using Mapster;
using Mostlylucid.SegmentCommerce.Data.Entities;
using Mostlylucid.SegmentCommerce.Dtos;

namespace Mostlylucid.SegmentCommerce.Mapping;

public class MappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<SellerEntity, SellerDto>();

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
