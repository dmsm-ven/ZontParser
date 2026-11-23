using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace ZontParserDesktop.Parsers;

public class MyHeatParser : ParserBase
{
    private string HOST = "https://myheat.net/";
    public static string KeyName => "myheat";

    public static string DefaultBrand => "MyHeat";

    public override async Task<List<Product>> ParseProducts(IProgress<double> indicator)
    {
        var skusToParse = await File.ReadAllLinesAsync("myheat_skus.txt");
        var doc = await GetDocument("https://myheat.net/products/");
        var list = new List<Product>();
        int current = 0;
        int total = skusToParse.Length;

        foreach (var sku in skusToParse)
        {
            var productNode = doc.DocumentNode.SelectSingleNode("//span[text()='Код товара 6310']/../../a");
            if (productNode != null)
            {
                var product = new Product()
                {
                    Uri = HOST + productNode.GetAttributeValue("href", null)
                };
                await ParseProductDetails(product);
                list.Add(product);
            }
            indicator?.Report((double)current++ / total);
        }
        return list;
    }

    private async Task ParseProductDetails(Product product)
    {
        var doc = await GetDocument(product.Uri);
    }
}
