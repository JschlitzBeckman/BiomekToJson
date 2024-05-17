using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using OthrosNet;

namespace BiomekToJson
{
  internal class Program
  {
    static void Main(string[] args)
    {
      var test = new Eeor();
      test.Put("Hello", "World!");
      test.Put("int", 413);
      test.Put("double", 612.0);
      test.Put("array", new[] { 1, 2, 3, 4, 5 });
      test.Put("object", new Eeor ());
      var vl = new VariantList { "Foo", "Bar", "Baz" };
      test.Put("List", vl);

      var simple = JsonConvert.SerializeObject(test, Formatting.Indented);

      Console.WriteLine(simple);

      Console.ReadKey(true);
    }
  }
}
