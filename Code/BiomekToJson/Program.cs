using OthrosNet;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Xml;

namespace BiomekToJson
{
  internal class Program
  {

    private static readonly JsonSerializerOptions JSO = new JsonSerializerOptions
    {
      ReadCommentHandling = JsonCommentHandling.Skip, 
      PropertyNameCaseInsensitive = true,
      WriteIndented = true,
      ReferenceHandler = ReferenceHandler.Preserve,
      TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };
    private static readonly JsonNodeOptions JNO = new JsonNodeOptions() { PropertyNameCaseInsensitive = true };
    private static readonly JsonDocumentOptions JDO = new JsonDocumentOptions() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

    static void Main(string[] args)
    {
      var test = new Eeor();
      test.Put("Hello", "World!");
      test.Put("int", 413);
      test.Put("double", (double)612.0);//TODO... looks like this would be an int on a round-trip...
      test.Put("NullValue", null);
      test.Put("now", DateTime.Now);
      test.Put("array", new[] { 1, 2, 3, 4, 5 });
      test.Put("object", new Eeor ());

      var tmp = new Eeor();
      tmp.Put("Wibble", "Wobble");
      test.Put("List", new VariantList { "Foo", 123, 45.6, tmp, null});
      test.Put(":", ":");

      
      
      #region Things that don't work
      //Creates a nested Array, no key names
      //var simple = JsonConvert.SerializeObject(eeor, Formatting.Indented);
      //Console.WriteLine(simple);

      //As above, but no indentation.
      //JsonSerializer serializer = new JsonSerializer { NullValueHandling = NullValueHandling.Ignore };
      //var stringStream = new StringWriter();
      //serializer.Serialize(stringStream, eeor);
      //Console.WriteLine(stringStream.ToString());

      //https://signavio.github.io/tech-blog/2017/json-type-information
      //https://www.codeproject.com/Articles/5284591/Adding-type-to-System-Text-Json-Serialization-like

      
      //I don't think I can writte sensible desrializers, since I don't know what I've got until I start examining it, 
      //and it seems that it is strictly step-forward with each token. 
      //but for writing...? I'd still need to use JsonConverterFactory since I don't know the type of the value until I look more closely 
      //options.Converters.Add(new EeorConverter());
      //options.Converters.Add(new VlConverter());
      //var str = JsonSerializer.Serialize(eeor, options);
      //Console.WriteLine(str);

      /* I could serialize the type information into the JSON, get it, and then deserialize the object with that type info.
       * This is a 2x cost, and I don't know if it jibes with the JsonConverter approach.
       *
       * There are callbacks for stages of serialization/deserialization. Ah, but you put them on the object you're serializing/desrializing
       *
       * Do we wrap VD & VL in custom types? ew.
       *
       * system.text.json is case sensitive. With eeors, when you add "Barcode" you can't also have "barCode" or whatever.
       *
       * Desrialize w/out .net class:
       *   Utf8JsonReader
       *   JSON DOM
       * {
       *   $type: "Eeor",
       *   foo: {
       *     $type: "VariantList",
       *     data: ["Foo", "Bar", "Baz"]
       *   }
       * }
       *
       *
       *
       */
      #endregion

      var root = ProcessEeor(test);

      Console.WriteLine(root.ToJsonString(JSO));

      Console.ReadKey(true);
    }

    private static JsonObject ProcessEeor(Eeor eeor)
    {
      var root = new JsonObject(JNO) { { "$type", JsonValue.Create("Eeor", JNO) } };

      foreach (var k in eeor.GetAllDoubleKeys())
        root.Add(k, JsonValue.Create(eeor.GetDouble(k), JNO));
      foreach (var k in eeor.GetAllCurrencyKeys())
        root.Add(k, JsonValue.Create(eeor.GetCurrency(k), JNO));
      foreach (var k in eeor.GetAllFloatKeys())
        root.Add(k, JsonValue.Create(eeor.GetFloat(k), JNO));
      foreach (var k in eeor.GetAllIntegerKeys())
        root.Add(k, JsonValue.Create(eeor.GetInt(k), JNO));
      foreach (var k in eeor.GetAllShortKeys())
        root.Add(k, JsonValue.Create(eeor.GetShort(k), JNO));
      foreach (var k in eeor.GetAllBooleanKeys())
        root.Add(k, JsonValue.Create(eeor.GetBool(k), JNO));
      foreach (var k in eeor.GetAllDateKeys())
        root.Add(k, JsonValue.Create(eeor.GetDate(k), JNO));
      foreach (var k in eeor.GetAllByteKeys())
        root.Add(k, JsonValue.Create(eeor.GetByte(k), JNO));
      foreach (var k in eeor.GetAllArrayKeys())
        root.Add(k, JsonValue.Create(eeor.GetArray(k), JNO));
      foreach (var k in eeor.GetAllComObjectKeys())
        root.Add(k, JsonValue.Create(eeor.GetComObject(k), JNO));
      foreach (var k in eeor.GetAllErrorKeys())
        root.Add(k, JsonValue.Create(eeor.GetError(k), JNO));
      foreach (var k in eeor.GetAllStringKeys())
        root.Add(k, JsonValue.Create(eeor.GetString(k), JNO));

      //TODO ???
      foreach (var k in eeor.GetAllNullKeys())
        root.Add(k, null);
      foreach (var k in eeor.GetAllEmptyKeys())
        root.Add(k, null);

      foreach (var k in eeor.GetAllDictionaryKeys())
        root.Add(k, ProcessEeor(eeor.GetDictionary(k)));

      foreach (var k in eeor.GetAllListKeys())
      {
        root.Add(k, ProcessList(eeor.GetList(k)));
      }

      return root;
    }

    private static JsonNode ProcessList(VariantList vl)
    {
      var innerArray = new JsonArray(JNO);
      var root = new JsonObject(JNO)
      {
        { "$type", JsonValue.Create("VariantList", JNO) },
        { "$value", innerArray }
      };
      foreach (var item in vl)
      {
        switch (item)
        {
          case Eeor dictionary:
            innerArray.Add(ProcessEeor(dictionary));
            break;
          case VariantList subList:
            innerArray.Add(ProcessList(subList));
            break;
          default:
            innerArray.Add(JsonValue.Create(item, JNO));
            break;
        }
      }

      return root;
    }
  }

  //public class VlConverter : JsonConverter<VariantList>
  //{
  //  public override void WriteJson(JsonWriter writer, VariantList value, JsonSerializer serializer)
  //  {
  //    if (value == null)
  //    {
  //      writer.WriteNull();
  //      return;
  //    }
        
  //    writer.WriteStartArray();
  //    foreach (var x in value)
  //    {
  //      serializer.Serialize(writer, x);
  //    }
  //    writer.WriteEndArray();

  //  }

  //  public override VariantList ReadJson(JsonReader reader, Type objectType, VariantList existingValue, bool hasExistingValue,
  //    JsonSerializer serializer)
  //  {
  //    throw new NotImplementedException();
  //  }
  //}


  //public class EeorConverter : JsonConverter<Eeor>
  //{
  //  public override Eeor Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
  //  {
  //    throw new NotImplementedException();
  //  }

  //  public override void Write(Utf8JsonWriter writer, Eeor value, JsonSerializerOptions options)
  //  {
  //    //if (value == null)
  //    //{
  //    //  writer.WriteNull()
  //    //  return;
  //    //}
        
  //    writer.WriteStartObject();
  //    foreach (var k in value.Keys)
  //    {
  //      //null is weird for eeors.
  //      var v = value.Get(k);
  //      if (v == null) //?
  //      {
  //        writer.WriteNull(k);
  //      }
  //      else
  //      {
  //        writer.WritePropertyName(k);
  //        writer.
  //        serializer.Serialize(writer, value.Get(k));
  //      }
  //    }
  //    writer.WriteEndObject();
  //  }
  //}
}
