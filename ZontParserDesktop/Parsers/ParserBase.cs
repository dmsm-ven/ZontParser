using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ZontParserDesktop.Parsers;

public abstract class ParserBase
{
    protected HttpClient httpClient = new HttpClient(new HttpClientHandler()
    {
        AllowAutoRedirect = true,
        UseCookies = true,
        CookieContainer = new System.Net.CookieContainer()
    });

    protected async Task<HtmlDocument> GetDocument(string uri)
    {
        var doc = new HtmlDocument();
        var html = await httpClient.GetStringAsync(uri);
        doc.LoadHtml(html);
        return doc;
    }

    public abstract Task<List<Product>> ParseProducts(IProgress<double> indicator);

    protected static string GetNodeText(HtmlNode node)
    {
        if (node == null)
            return string.Empty;

        var text = HtmlEntity.DeEntitize(node.InnerText);
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    protected static string ResolveUrl(string baseUrl, string relativeUrl)
    {
        if (string.IsNullOrWhiteSpace(relativeUrl))
            return null;

        if (Uri.TryCreate(relativeUrl, UriKind.Absolute, out var absoluteUri))
            return absoluteUri.ToString();

        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) &&
            Uri.TryCreate(baseUri, relativeUrl, out absoluteUri))
        {
            return absoluteUri.ToString();
        }

        return null;
    }

    protected static List<decimal> ParseNumbers(string value)
    {
        var result = new List<decimal>();
        if (string.IsNullOrWhiteSpace(value))
            return result;

        var matches = Regex.Matches(value, @"\d+(?:[.,]\d+)?");
        foreach (Match match in matches)
        {
            if (decimal.TryParse(
                match.Value.Replace(',', '.'),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var number))
            {
                result.Add(number);
            }
        }

        return result;
    }

    protected static string SanitizeDescriptionMarkup(string markup)
    {
        if (string.IsNullOrWhiteSpace(markup))
            return markup;

        var document = new HtmlDocument();
        document.LoadHtml(markup);

        foreach (var node in document.DocumentNode.Descendants().ToList())
        {
            node.Attributes.Remove("class");
            node.Attributes.Remove("style");
            node.Attributes.Remove("width");
            node.Attributes.Remove("height");
        }

        return document.DocumentNode.InnerHtml.Trim();
    }

    protected static void PrepareDescriptionImages(Product product)
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

                // Prevent the browser from selecting supplier images through responsive/lazy-load attributes.
                foreach (var attributeName in new[]
                {
                    "srcset",
                    "sizes",
                    "data-src",
                    "data-srcset",
                    "data-lazy-src",
                    "data-lazy-srcset",
                    "data-original",
                    "data-original-src"
                })
                {
                    imageNode.Attributes.Remove(attributeName);
                }
            }
        }

        // Product descriptions must not contain links to the supplier website.
        var linkedNodes = description.DocumentNode.SelectNodes("//*[@href]");
        if (linkedNodes != null)
        {
            foreach (var node in linkedNodes)
                node.Attributes.Remove("href");
        }

        product.DescriptionMarkup = description.DocumentNode.InnerHtml.Trim();
    }
}
