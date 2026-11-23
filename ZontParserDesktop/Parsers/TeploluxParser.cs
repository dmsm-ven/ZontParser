using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ZontParserDesktop.Helpers;

namespace ZontParserDesktop.Parsers;

public class TeploluxParser : ParserBase
{
    private readonly string HOST = "https://teploluxe.ru";
    public static string KeyName => "teplolux";

    public static string DefaultBrand => "Teploluxe";

    public override async Task<List<Product>> ParseProducts(IProgress<double> indicator)
    {
        var skusToParse = await File.ReadAllLinesAsync("teplolux_skus.txt");
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

    private async Task ParseProductDetails(Product product)
    {
        var doc = await GetDocument(product.Uri);
        var root = doc.DocumentNode;

        if (root == null)
        {
            throw new Exception("cannot load html doc structure");
        }

        product.Name = root.SelectSingleNode("//h1").InnerText.Trim();
        product.DescriptionMarkup = root.SelectSingleNode("//div[@class='t_desc']")?.InnerHtml.TrimHtml();

        if (root.SelectSingleNode("//div[@class='pd_rd_thumbs']//img") != null)
        {
            var images = root.SelectNodes("//div[@class='pd_rd_thumbs']//img")
                .Select(image => HOST + image.GetAttributeValue("src", null))
                .ToArray();
            product.Images.AddRange(images);
        }
        else
        {
            throw new Exception("images not found");
        }

        if (root.SelectSingleNode("//div[@class='tech_chars_table']/div[contains(@class, 'tc_table_rows')]") != null)
        {
            var chars = root.SelectNodes("//div[@class='tech_chars_table']/div[contains(@class, 'tc_table_rows')]")
                .Select(div => new Characteristic()
                {
                    Group = "Технические характеристики",
                    Name = div.SelectSingleNode("./div[1]").InnerText.Trim(),
                    Value = div.SelectSingleNode("./div[2]").InnerText.Trim(),
                })
                .ToArray();
            product.Characteristics.AddRange(chars);
        }

        if (root.SelectSingleNode("//div[contains(@class, 'docs_rows')]//a[contains(@href, '.pdf')]") != null)
        {
            var pdfs = root.SelectNodes("//div[contains(@class, 'docs_rows')]//a[contains(@href, '.pdf')]")
                .Select(a => new PdfInstruction()
                {
                    Name = a.InnerText.TrimHtml(),
                    Uri = HOST + a.GetAttributeValue("href", null)
                })
                .ToArray();
            product.Instructions.AddRange(pdfs);
        }
    }

    private async Task<string> SerachProductUriBySku(string sku)
    {
        var directSkuToUriMap = new Dictionary<string, string>()
        {
            [""] = "",
        };
        if (directSkuToUriMap.ContainsKey(sku))
        {
            return directSkuToUriMap[sku];
        }

        //example: https://teploluxe.ru/search/?q=100035693300
        var doc = await GetDocument($"https://teploluxe.ru/search/?q={sku}");
        if (doc.DocumentNode.SelectSingleNode("//div[@class='catalog_item_card']/a") != null)
        {
            var results = doc.DocumentNode.SelectNodes("//div[@class='catalog_item_card']/a")
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
        throw new ArgumentOutOfRangeException();
    }
}
