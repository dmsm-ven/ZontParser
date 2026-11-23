using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ZontParserDesktop.Parsers;

namespace ZontParserDesktop;

public class Manager
{
    public string CurrentBrandKey { get; set; }
    private readonly ResourceDownloader resourceDownloader;
    private string CurrentStorageFileName
    {
        get
        {
            if (string.IsNullOrWhiteSpace(CurrentBrandKey))
            {
                throw new ArgumentNullException(nameof(CurrentBrandKey));
            }
            return $"{CurrentBrandKey}_products.json"; ;
        }
    }
    private List<Product> products = new();
    public Manager()
    {
        resourceDownloader = new ResourceDownloader();
    }

    public async Task ParseBrand(IProgress<double> progress)
    {
        var parser = CreateParser();
        var parsedProducts = await parser.ParseProducts(progress);
        products = parsedProducts;
        await Save(parsedProducts);
    }

    public string GetDescriptionSql(int startId)
    {
        var formatter = new Formatter(products, startId);
        return formatter.GetDescriptionSql();
    }

    public string GetPdfSql(int startId)
    {
        var formatter = new Formatter(products, startId);
        return formatter.GetPdfSql();
    }

    public string GetImagesSql()
    {
        var formatter = new Formatter(products, -1);
        return formatter.GetImagesSql();
    }

    public string GetAdditionalImagesExport(int pid)
    {
        var formatter = new Formatter(products, pid);
        return formatter.GetAdditionalImagesExport();
    }

    public string GetGeneralExport(int pid)
    {
        var formatter = new Formatter(products, pid);
        return formatter.GetGeneralExport();
    }

    private async Task Save(List<Product> productsToSave)
    {
        if (productsToSave != null)
        {
            var str = JsonSerializer.Serialize(productsToSave);
            File.WriteAllText(CurrentStorageFileName, str);
        }
    }

    public async Task Load()
    {
        if (File.Exists(CurrentStorageFileName))
        {
            await using FileStream stream = File.OpenRead(CurrentStorageFileName);
            products = await JsonSerializer.DeserializeAsync<List<Product>>(stream);
        }
    }

    internal async Task DownloadResources(string resourceFolder, IProgress<double> progress)
    {
        await resourceDownloader.DownloadResource(products, resourceFolder, progress);
    }

    internal string GetDimensionsSql(int startId)
    {
        var formatter = new Formatter(products, startId);
        return formatter.GetDimensionsSql();
    }

    private ParserBase CreateParser()
    {
        if (CurrentBrandKey == MyHeatParser.KeyName)
        {
            return new MyHeatParser();
        }
        if (CurrentBrandKey == TeploluxNeptuneParser.KeyName)
        {
            return new TeploluxNeptuneParser();
        }
        if (CurrentBrandKey == ZontBrandParser.KeyName)
        {
            return new ZontBrandParser();
        }
        throw new NotImplementedException();
    }
}
