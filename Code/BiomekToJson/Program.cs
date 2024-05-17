using System;
using System.Collections.Generic;
using System.IO;
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

      //Creates a nested Array, no key names
      //var simple = JsonConvert.SerializeObject(test, Formatting.Indented);
      //Console.WriteLine(simple);

      //As above, but no indentation.
      //JsonSerializer serializer = new JsonSerializer { NullValueHandling = NullValueHandling.Ignore };
      //var stringStream = new StringWriter();
      //serializer.Serialize(stringStream, test);
      //Console.WriteLine(stringStream.ToString());

      var serializer = new JsonSerializer
      {
        NullValueHandling = NullValueHandling.Ignore,
        Formatting = Formatting.Indented
      };
      serializer.Converters.Add(new EeorConverter());
      var stringStream = new StringWriter();
      serializer.Serialize(stringStream, test);
      Console.WriteLine(stringStream.ToString());

      Console.ReadKey(true);
    }
  }

  public class VlConverter : JsonConverter<VariantList>
  {
    public override void WriteJson(JsonWriter writer, VariantList value, JsonSerializer serializer)
    {
      if (value == null)
      {
        writer.WriteNull();
        return;
      }
        
      writer.WriteStartArray();
      foreach (var x in value)
      {
        serializer.Serialize(writer, x);
      }
      writer.WriteEndArray();

    }

    public override VariantList ReadJson(JsonReader reader, Type objectType, VariantList existingValue, bool hasExistingValue,
      JsonSerializer serializer)
    {
      throw new NotImplementedException();
    }
  }


  public class EeorConverter : JsonConverter<Eeor>
  {
    public override void WriteJson(JsonWriter writer, Eeor value, JsonSerializer serializer)
    {
      if (value == null)
      {
        writer.WriteNull();
        return;
      }
        
      writer.WriteStartObject();
      foreach (var k in value.Keys)
      {
        writer.WritePropertyName(k);
        serializer.Serialize(writer, value.Get(k));
      }
      writer.WriteEndObject();
    }

    public override Eeor ReadJson(JsonReader reader, Type objectType, Eeor existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
      throw new NotImplementedException();
   }

  }
}
