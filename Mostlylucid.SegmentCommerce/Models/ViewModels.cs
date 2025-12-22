namespace Mostlylucid.SegmentCommerce.Models;

public class HomeViewModel
{
    public List<Product> PersonalisedProducts { get; set; } = [];
    public List<Product> TrendingProducts { get; set; } = [];
    public List<Product> OnSaleProducts { get; set; } = [];
    public List<string> Categories { get; set; } = [];
    public InterestSignature InterestSignature { get; set; } = new();
}
