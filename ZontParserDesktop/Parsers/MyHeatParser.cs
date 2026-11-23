using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
            var normalizedSku = sku.Trim();
            if (string.IsNullOrWhiteSpace(normalizedSku))
            {
                indicator?.Report((double)++current / total);
                continue;
            }

            var productNode = doc.DocumentNode.SelectSingleNode(
                $"//span[normalize-space(.) = 'Код товара {normalizedSku}']/../../a");

            if (productNode != null)
            {
                var product = new Product
                {
                    Uri = ResolveUrl(HOST, productNode.GetAttributeValue("href", null)),
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

        // Product name
        product.Name = GetNodeText(
            root.SelectSingleNode("//h1[contains(@class, 'text-dark')]"));

        // SKU
        var skuNode = root.SelectSingleNode("//span[contains(normalize-space(.), 'Код товара')]");
        if (skuNode != null)
        {
            var match = Regex.Match(skuNode.InnerText, @"\d+");
            if ((match.Success && product.Sku != match.Value) || !match.Success)
            {
                throw new FormatException("Ошибка формата sku товара");
            }
        }

        // Product images
        var imageNodes = root.SelectNodes(
            "//a[contains(concat(' ', normalize-space(@class), ' '), ' thumbnails-list ')]");

        if (imageNodes != null)
        {
            foreach (var node in imageNodes)
            {
                var imageUrl = node.GetAttributeValue("href", null);

                if (string.IsNullOrWhiteSpace(imageUrl))
                {
                    imageUrl = node.SelectSingleNode(".//img")
                        ?.GetAttributeValue("src", null);
                }

                var absoluteUrl = ResolveUrl(product.Uri, imageUrl);

                if (!string.IsNullOrWhiteSpace(absoluteUrl) &&
                    !product.Images.Contains(absoluteUrl, StringComparer.OrdinalIgnoreCase))
                {
                    product.Images.Add(absoluteUrl);
                }
            }
        }

        // Description markup
        var descriptionNode = root.SelectSingleNode(
            "//h2[normalize-space()='О товаре']/ancestor::section[1]//p[1]");

        if (descriptionNode != null)
            product.DescriptionMarkup = descriptionNode.InnerHtml.Trim();

        // Functional features
        // Resolve the tab by its ID directly. It is not guaranteed to remain inside
        // the same wrapper as the "Характеристики" heading.
        var featuresListNode = root.SelectSingleNode("//div[@id='pills-home']/ul");

        if (featuresListNode != null)
        {
            var featuresMarkup = featuresListNode.OuterHtml.Trim();

            product.DescriptionMarkup = string.IsNullOrWhiteSpace(product.DescriptionMarkup)
                ? featuresMarkup
                : product.DescriptionMarkup + Environment.NewLine + featuresMarkup;
        }

        // Technical characteristics are parsed separately from the #pills-profile tab.
        var technicalTab = root.SelectSingleNode("//div[@id='pills-profile']");

        if (technicalTab != null)
        {
            var groupName = GetNodeText(
                root.SelectSingleNode(
                    "//ul[contains(@class, 'nav-pills')]//button[@data-bs-target='#pills-profile']"));

            var characteristicNodes = technicalTab.SelectNodes(
                ".//li[contains(@class, 'list-group-item')]");

            if (characteristicNodes != null)
            {
                foreach (var node in characteristicNodes)
                {
                    var name = GetNodeText(
                        node.SelectSingleNode(
                            ".//p[contains(@class, 'text-caption')]"));

                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    var valueNodes = node.SelectNodes(
                        ".//div[contains(@class, 'me-auto')]/p[not(contains(@class, 'text-caption'))]");

                    if (valueNodes == null)
                        continue;

                    var values = valueNodes
                        .Select(GetNodeText)
                        .Where(value => !string.IsNullOrWhiteSpace(value));

                    var value = string.Join("\n", values);
                    if (string.IsNullOrWhiteSpace(value))
                        continue;

                    product.Characteristics.Add(new Characteristic
                    {
                        Group = groupName,
                        Name = name,
                        Value = value
                    });

                    // Product.Dimensions accepts exactly three values.
                    // Keep 2D dimensions in Characteristics instead of inventing a third value.
                    if (name.Contains("габарит", StringComparison.OrdinalIgnoreCase))
                    {
                        var dimensions = ParseNumbers(value);
                        if (dimensions.Count == 3)
                            product.Dimensions = dimensions.ToArray();
                    }

                    // Weight is stored in kilograms.
                    if (name.Contains("масса", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("вес", StringComparison.OrdinalIgnoreCase))
                    {
                        var numbers = ParseNumbers(value);
                        if (numbers.Count > 0)
                        {
                            product.Weight = value.Contains("гр", StringComparison.OrdinalIgnoreCase)
                                ? numbers[0] / 1000m
                                : numbers[0];
                        }
                    }
                }
            }
        }

        // Remove presentation-only attributes from the final description markup.
        product.DescriptionMarkup = SanitizeDescriptionMarkup(product.DescriptionMarkup);

        // PDF documents
        var pdfNodes = root.SelectNodes(
            "//a[contains(translate(@href, 'PDF', 'pdf'), '.pdf')]");

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
}
