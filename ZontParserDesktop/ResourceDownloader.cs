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

        /*foreach (var image in images)
        {
            string localPath = Path.Combine(folder, product.ManufacturerFtpPath, "products", image.CreateMD5() + Path.GetExtension(image).ToLower());

            if (!File.Exists(localPath))
            {
                var dir = Path.GetDirectoryName(localPath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var data = await httpClient.GetByteArrayAsync(pdf.Uri);
                await File.WriteAllBytesAsync(localPath, image);
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }*/

        // Description images are always stored as JPEG, regardless of the supplier format.
        foreach (var imageUrl in product.DescriptionImages.Distinct())
        {
            string localPath = Path.Combine(
                folder,
                product.ManufacturerFtpPath,
                "description_images",
                imageUrl.CreateMD5() + Path.GetExtension(imageUrl));

            if (File.Exists(localPath))
                continue;

            var dir = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var imageBytes = await httpClient.GetByteArrayAsync(imageUrl);
            await File.WriteAllBytesAsync(localPath, imageBytes);
            await Task.Delay(TimeSpan.FromSeconds(1));
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

                var data = await httpClient.GetByteArrayAsync(pdf.Uri);
                await File.WriteAllBytesAsync(localPath, data);
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }
    }
}
