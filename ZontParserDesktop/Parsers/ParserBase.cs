using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
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
        var html = await httpClient.GetStringAsync(uri));
        doc.LoadHtml(html);
        return doc;
    }

    public abstract Task<List<Product>> ParseProducts(IProgress<double> indicator);
}
