using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HtmlAgilityPack;

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
        var directSkuToUriMap = new Dictionary<string, string>()
        {
            ["ML12218"] = "https://zont.online/product/akkumulyator-rezervnyj-li-ion-1ah/",
            ["ML8569"] = "https://zont.online/product/datchik-temperatury-komnatnyj-ds18s20/",
            ["ML00003614"] = "https://zont.online/product/termodatchik-teplonositelya-ulichnyj-zont-ds18b20/",
            ["ML13866-tvp"] = "https://zont.online/product/radiotermodatchik-ulichnyj-zont-ml-711/",
            ["ML00000291"] = "https://zont.online/product/rele-promezhutochnoe-na-din-rejku-v-sbore/",
            ["ML12294"] = "https://zont.online/product/termodatchik-ulichnyj-teplonositelya-zont-700/",

        };
        if (directSkuToUriMap.ContainsKey(sku))
        {
            return directSkuToUriMap[sku];
        }

        //example: https://zont.online/?s=ML00007554
        var doc = await GetDocument($"https://zont.online/?s={sku}");
        if (doc.DocumentNode.SelectSingleNode("//a[contains(@class, 'card ')]") != null)
        {
            var results = doc.DocumentNode.SelectNodes("//a[contains(@class, 'card ')]")
                .Select(a => a.GetAttributeValue("href", null))
                .Where(href => href.StartsWith("https://zont.online/product/"))
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

    private async Task ParseProductDetails(Product product)
    {
        var doc = await GetDocument(product.Uri);
        var root = doc.DocumentNode;

        // Verify SKU
        string productPageSku = GetNodeText(
            root.SelectSingleNode("//div[contains(@class, 'product__info')]//div[contains(text(), 'Артикул: ')]"))
            .Replace("Артикул: ", "")
            .Trim();

        if (productPageSku != product.Sku)
        {
            Debug.WriteLine($"Несовпадение sku товара: {productPageSku} != {product.Sku}");
        }

        // Product name
        product.Name = GetNodeText(
            root.SelectSingleNode("//div[contains(@class, 'product__title')]//h1"));

        // Product images: use links to original images, not thumbnail previews.
        var imageNodes = root.SelectNodes(
            "//div[contains(@class, 'product__image-big')]//a[@rel='gallery']");

        if (imageNodes != null)
        {
            foreach (var node in imageNodes)
            {
                var imageUrl = ResolveUrl(
                    product.Uri,
                    node.GetAttributeValue("href", null));

                if (!string.IsNullOrWhiteSpace(imageUrl) &&
                    !product.Images.Contains(imageUrl, StringComparer.OrdinalIgnoreCase))
                {
                    product.Images.Add(imageUrl);
                }
            }
        }

        // Combine short and full product descriptions while preserving their HTML.
        var shortDescriptionNode = root.SelectSingleNode(
            "//div[contains(@class, 'product__content')]//div[contains(@class, 'product__text')]");

        var fullDescriptionNode = root.SelectSingleNode("//div[@id='description']");

        var descriptions = new[]
        {
            shortDescriptionNode?.InnerHtml.Trim(),
            fullDescriptionNode?.InnerHtml.Trim()
        }
        .Where(markup => !string.IsNullOrWhiteSpace(markup));

        product.DescriptionMarkup = string.Join(
            Environment.NewLine,
            descriptions);

        // Technical characteristics
        const string characteristicsGroup = "Технические характеристики";
        var characteristicRows = root.SelectNodes(
            "//div[@id='specifications']//table//tr");

        if (characteristicRows != null)
        {
            foreach (var row in characteristicRows)
            {
                var cells = row.SelectNodes("./td");
                if (cells == null || cells.Count < 2)
                    continue;

                var name = GetNodeText(cells[0]);
                var value = GetNodeText(cells[1]);

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value))
                    continue;

                product.Characteristics.Add(new Characteristic
                {
                    Group = characteristicsGroup,
                    Name = name,
                    Value = value
                });

                if (name.Contains("габарит", StringComparison.OrdinalIgnoreCase))
                {
                    var dimensions = ParseNumbers(value);
                    if (dimensions.Count == 3)
                        product.Dimensions = dimensions.ToArray();
                }

                if (name.Contains("масса", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("вес", StringComparison.OrdinalIgnoreCase))
                {
                    var weight = ParseNumbers(value);
                    if (weight.Count > 0)
                    {
                        product.Weight = value.Contains("гр", StringComparison.OrdinalIgnoreCase)
                            ? weight[0] / 1000m
                            : weight[0];
                    }
                }
            }
        }

        // Remove presentation-only attributes from the final description markup.
        product.DescriptionMarkup = SanitizeDescriptionMarkup(product.DescriptionMarkup);
        PrepareDescriptionImages(product);

        // PDF documents listed in the product documentation tab.
        var pdfNodes = root.SelectNodes(
            "//div[@id='documentation']//a[contains(translate(@href, 'PDF', 'pdf'), '.pdf')]");

        if (pdfNodes != null)
        {
            var addedUrls = new HashSet<string>(
                product.Instructions.Select(instruction => instruction.Uri),
                StringComparer.OrdinalIgnoreCase);

            foreach (var node in pdfNodes)
            {
                var pdfUrl = ResolveUrl(
                    product.Uri,
                    node.GetAttributeValue("href", null));

                if (string.IsNullOrWhiteSpace(pdfUrl) || !addedUrls.Add(pdfUrl))
                    continue;

                product.Instructions.Add(new PdfInstruction
                {
                    Name = GetNodeText(node),
                    Uri = pdfUrl
                });
            }
        }
    }

    private static void PrepareDescriptionImages(Product product)
    {
        if (string.IsNullOrWhiteSpace(product.DescriptionMarkup))
            return;

        var description = new HtmlDocument();
        description.LoadHtml(product.DescriptionMarkup);

        var imageNodes = description.DocumentNode.SelectNodes("//img");
        if (imageNodes != null)
        {
            foreach (var imageNode in imageNodes)
            {
                var imageSource = imageNode.GetAttributeValue("src", null);

                if (string.IsNullOrWhiteSpace(imageSource))
                    imageSource = imageNode.GetAttributeValue("data-src", null);

                var imageUrl = ResolveUrl(product.Uri, imageSource);
                if (string.IsNullOrWhiteSpace(imageUrl))
                    continue;

                if (!product.DescriptionImages.Contains(imageUrl, StringComparer.OrdinalIgnoreCase))
                    product.DescriptionImages.Add(imageUrl);

                // Keep the original URL in markup; Formatter replaces it with the local website path.
                imageNode.SetAttributeValue("src", imageUrl);
                imageNode.Attributes.Remove("data-src");
            }
        }

        // Product descriptions must not contain links to the supplier website.
        foreach (var node in description.DocumentNode.SelectNodes("//*[@href]") ?? Enumerable.Empty<HtmlNode>())
            node.Attributes.Remove("href");

        product.DescriptionMarkup = description.DocumentNode.InnerHtml.Trim();
    }
}
