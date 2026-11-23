using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using ZontParserDesktop.Helpers;

namespace ZontParserDesktop;

public class ResourceDownloader
{
    private HttpClient httpClient = new(new HttpClientHandler()
    {
        AllowAutoRedirect = true,
        CookieContainer = new System.Net.CookieContainer(),
        UseCookies = true,
        AutomaticDecompression = System.Net.DecompressionMethods.All
    });
    public async Task DownloadResource(IEnumerable<Product> products, string folder, IProgress<double> indicator)
    {
        int total = products.Count();
        int current = 0;

        foreach (var product in products)
        {

            try
            {
                await DownloadProductResources(folder, product);
                await Task.Delay(TimeSpan.FromSeconds(1.25));
            }
            catch
            {

            }

            indicator?.Report((double)++current / total);
        }
    }

    private async Task DownloadProductResources(string folder, Product product)
    {
        var images = product.Images.Distinct().ToArray();

        foreach (var image in images)
        {
            string localPath = Path.Combine(folder, product.ManufacturerFtpPath, "products", image.CreateMD5() + Path.GetExtension(image).ToLower());

            if (!File.Exists(localPath))
            {
                var dir = Path.GetDirectoryName(localPath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                try
                {
                    using (var fs = new FileStream(localPath, FileMode.Create))
                    {
                        var data = await httpClient.GetStreamAsync(image);
                        await data.CopyToAsync(fs);
                        await Task.Delay(TimeSpan.FromSeconds(1));
                    }

                }
                catch
                {
                    throw;
                }
            }
        }

        var instructions = product.Instructions;

        foreach (var pdf in instructions)
        {
            string localPath = Path.Combine(folder, product.ManufacturerFtpPath, "pdf", pdf.Uri.CreateMD5() + Path.GetExtension(pdf.Uri).ToLower());

            if (!File.Exists(localPath))
            {
                var dir = Path.GetDirectoryName(localPath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                try
                {
                    using (var fs = new FileStream(localPath, FileMode.Create))
                    {
                        var data = await httpClient.GetStreamAsync(pdf.Uri);
                        await data.CopyToAsync(fs);
                    }
                }
                catch
                {
                    throw;
                }
            }
        }
    }
}
