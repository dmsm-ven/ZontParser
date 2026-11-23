using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ZontParserDesktop.Parsers;

public class TeploluxNeptuneParser : ParserBase
{
    public static string KeyName => "teplolux";

    public static string DefaultBrand => "Теплолюкс";

    public override Task<List<Product>> ParseProducts(IProgress<double> indicator)
    {
        throw new NotImplementedException();
    }
}
