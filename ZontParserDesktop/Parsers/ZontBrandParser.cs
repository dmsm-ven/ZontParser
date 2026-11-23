using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ZontParserDesktop.Helpers;

namespace ZontParserDesktop.Parsers;

public class ZontBrandParser : ParserBase
{
    private readonly string HOST = "https://zont.online/";
    public static string KeyName => "zont";

    public static string DefaultBrand => "ZONT";

    public override async Task<List<Product>> ParseProducts(IProgress<double> indicator)
    {
        var skusToParse = await File.ReadAllLinesAsync("zont_skus.txt");
        var list = new List<Product>();
        int current = 0;
        int total = skusToParse.Length;

        foreach (var sku in skusToParse)
        {
            string uri = await SerachProductUriBySku(sku);
            if (!string.IsNullOrWhiteSpace(uri))
            {
                var product = new Product
                {
                    Uri = uri,
                    Manufacturer = DefaultBrand,
                    Sku = sku
                };

                await ParseProductDetails(product);
                list.Add(product);
            }

            indicator?.Report((double)++current / total);
        }

        return list;
    }


    private async Task<string> SerachProductUriBySku(string sku)
    {
        //example: https://zont.online/?s=ML00007554
        var doc = await GetDocument($"https://zont.online/?s={sku}");
        if (doc.DocumentNode.SelectSingleNode("//a[contains(@class, 'card ')]") != null)
        {
            var results = doc.DocumentNode.SelectNodes("//a[contains(@class, 'card ')]")
                .Select(a => a.GetAttributeValue("href", null))
                .ToArray();

            if (results.Length == 1)
            {
                return results[0];
            }
            Debug.WriteLine($"Найден более 1го результата для SKU: {sku}");
        }
        else
        {
            Debug.WriteLine($"Товар с SKU: {sku} не найден");
        }
        return "";
    }

    private async Task ParseProductDetails(Product product)
    {
        var doc = await GetDocument(product.Uri);

        string productPageSku = doc.DocumentNode.SelectSingleNode("//div[@class='product__info']//div[contains(text(), 'Артикул: ')]")?
            .InnerText
            .Replace("Артикул: ", "")
            .TrimHtml();


        if (productPageSku != product.Sku)
        {
            throw new Exception($"Несовпадение sku товара: {productPageSku} != {product.Sku}");
        }
    }
}
