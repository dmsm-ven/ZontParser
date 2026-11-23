using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ZontParserDesktop.Parsers;

public class ZontBrandParser : ParserBase
{
    public static string KeyName => "zont";

    public static string DefaultBrand => "ZONT";

    public override Task<List<Product>> ParseProducts(IProgress<double> indicator)
    {
        throw new NotImplementedException();
    }
}
