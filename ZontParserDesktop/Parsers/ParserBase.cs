using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Globalization;
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
}
