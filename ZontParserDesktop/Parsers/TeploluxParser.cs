using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
            var product = new Product
            {
                Uri = uri,
                Manufacturer = DefaultBrand,
                Sku = sku
            };
            if (!string.IsNullOrWhiteSpace(product.Uri))
            {
                await ParseProductDetails(product);
            }
            list.Add(product);

            indicator?.Report((double)++current / total);
            if (sku != skusToParse.Last())
            {
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
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
        PrepareDescriptionImages(product);

        if (root.SelectSingleNode("//div[@class='pd_rd_thumbs']//img") != null)
        {
            var images = root.SelectNodes("//div[@class='pd_rd_thumbs']//img")
                .Select(image => HOST + image.GetAttributeValue("src", null))
                .ToArray();
            product.Images.AddRange(images);
        }
        else if (root.SelectSingleNode("//div[@class='product_images_slider_wrap']//img") != null)
        {
            var singleImage = HOST + root.SelectSingleNode("//div[@class='product_images_slider_wrap']//img").GetAttributeValue("src", null);
            product.Images.Add(singleImage);
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

        var pageSkuNode = doc.DocumentNode.SelectSingleNode("//div[@class='top_chars']/p[contains(text(), 'Артикул: ')]");
        if (pageSkuNode != null)
        {
            var pageSKu = pageSkuNode
                .InnerText
                .Replace("Артикул: ", "")
                .Trim();

            if (pageSKu != product.Sku)
            {
                Debug.WriteLine($"Несовпадение SKU товара: {pageSKu ?? "<EMPTY>"} != {product.Sku}");
            }
        }

        var productSkuCh = product.Characteristics.FirstOrDefault(ch => ch.Name == "Артикул 1С");
        if (productSkuCh != null)
        {
            product.Characteristics.Remove(productSkuCh);
        }

        var productDimensionsCh = product.Characteristics.FirstOrDefault(ch => ch.Name == "Размеры");
        if (productDimensionsCh != null)
        {
            //example:  38 х 88 х 66 мм
            var dimRegexp = Regex.Match(productDimensionsCh.Value, @"(\d+) ?х ?(\d+) ?х ?(\d+) ?мм");
            if (dimRegexp.Success)
            {
                product.Dimensions = [
                    int.Parse(dimRegexp.Groups[1].Value),
                    int.Parse(dimRegexp.Groups[2].Value),
                    int.Parse(dimRegexp.Groups[2].Value)];
            }
        }
        var productWeightCh = product.Characteristics.FirstOrDefault(ch => ch.Name == "Вес");
        if (productWeightCh != null)
        {
            var weightRegexp = Regex.Match(productWeightCh.Value, @"(\d+) ?г");
            if (weightRegexp.Success)
            {
                product.Weight = int.Parse(weightRegexp.Groups[1].Value);
            }
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
                return HOST + results[0];
            }
            Debug.WriteLine($"Найден более 1го результата для SKU: {sku}");
        }
        else
        {
            Debug.WriteLine($"Товар с SKU: {sku} не найден");
        }
        return "";
    }
}
