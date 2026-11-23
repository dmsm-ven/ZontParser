using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ZontParserDesktop.Parsers;

public class MyHeatParser : ParserBase
{
    public static string KeyName => "myheat";

    public static string DefaultBrand => "MyHeat";

    public override Task<List<Product>> ParseProducts()
    {
        throw new NotImplementedException();
    }
}
