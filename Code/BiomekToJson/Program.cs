using OthrosNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Xml;

namespace BiomekToJson
{
  internal class Program
  {
    private const string TYPE_KEY = "$type";
    private const string VALUE_KEY = "$value";
    private const string EEOR_NAME = "Eeor";
    private const string VARIANT_LIST_NAME = "VariantList";
    private const string DATE_NAME = "Date";

    private static readonly JsonSerializerOptions JSO = new JsonSerializerOptions
    {
      ReadCommentHandling = JsonCommentHandling.Skip,
      PropertyNameCaseInsensitive = true,
      WriteIndented = true,
      ReferenceHandler = ReferenceHandler.Preserve,
      TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
      Converters = { new DoubleConverter(), new FloatConverter(), new DecimalConverter() }
    };

    private static readonly JsonNodeOptions JNO = new JsonNodeOptions() { PropertyNameCaseInsensitive = true };

    private static readonly JsonDocumentOptions JDO = new JsonDocumentOptions()
      { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

    static void Main(string[] args)
    {

      var test = new Eeor() ;
      test.Put("Hello", "World!");
      test.Put("int", 413);
      test.Put("DecimalAkaCurrency", 1111m);
      test.PutDouble("double", 612.0); //TODO... looks like this would be an int on a round-trip...
      test.Put("NullValue", null);
      test.Put("now", DateTime.Now);
      //test.Put("weird", new Exception("weird stuff")); This breaks, and I think that's OK
      test.Put("arrayInt", new[] { 1, 2, 3, 4, 5 });
      test.Put("arrayDouble", new[] { 1.0, 2.0, 3.3, 4.13, 5.0 });
      test.Put("emptyArray", Array.Empty<object>());
      var eo = new Eeor();
      eo.Put("foo", "bar");
      test.Put("eeorArray", new[] { eo, new Eeor()});
      test.Put("arrayArray", new[] { new[]{2,3,5,7}, new []{4,6,8,9}});
      test.Put("vlArray", new[] { new VariantList(){999}});
      test.Put("object", new Eeor());

      var tmp = new Eeor();
      tmp.Put("Wibble", "Wobble");
      test.Put("List", new VariantList { "Foo", 123, 45.6, tmp, null });
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

      //TODO: consider using a breadth-first search to serialize the information and prevent cycles
      //TODO: as I go through the graph, can the references to previous objects be the json path instead of some id?
      var root = ProcessEeor(test);

      var asString = root.ToJsonString(JSO);
      Console.WriteLine(asString);
      //Can we round trip? ... Sorta. decimals get squished to doubles. all int types become ints.
      Eeor roundTrip = ProcessJsEeor((JsonObject)JsonNode.Parse(asString, JNO));


      Console.ReadKey(true);
    }

    private static Eeor ProcessJsEeor(JsonObject theObject)
    {
      var result = new Eeor();
      foreach (var kvp in theObject)
      {
        if (kvp.Key == TYPE_KEY) continue;


        if (kvp.Value == null)
        {
          result.Put(kvp.Key, null);
          continue;
        }

        var vk = kvp.Value.GetValueKind();
        JsonValue jv;
        switch (vk)
        {
          case JsonValueKind.String:
            jv = kvp.Value.AsValue();
            result.Put(kvp.Key, jv.GetValue<string>());
            break;
          case JsonValueKind.Number:
            jv = kvp.Value.AsValue();
            if (jv.TryGetValue(out int i))
              result.PutInt(kvp.Key, i);
            else
              result.Put(kvp.Key, jv.GetValue<double>());
            break;
          case JsonValueKind.Null:
            result.Put(kvp.Key, null);
            break;
          case JsonValueKind.True:
            result.PutBool(kvp.Key, true);
            break;
          case JsonValueKind.False:
            result.PutBool(kvp.Key, false);
            break;
          case JsonValueKind.Object:
            var jo = kvp.Value.AsObject();
            var theType = jo[TYPE_KEY].GetValue<string>();
            if (theType == EEOR_NAME)
              result.Put(kvp.Key, ProcessJsEeor(jo));
            else if (theType == VARIANT_LIST_NAME)
              result.Put(kvp.Key, ProcessJsVariantList(jo));
            else if (theType == DATE_NAME)
              result.Put(kvp.Key, jo[VALUE_KEY].GetValue<DateTime>());
            else
              throw new Exception($"Unable to interpret {kvp.Key} = {kvp.Value.ToJsonString()}.");
            break;
          case JsonValueKind.Array:
            result.Put(kvp.Key, ProcessJsArray(kvp.Value.AsArray()));
            break;
          default:
            throw new Exception($"{kvp.Key} error in {theObject.ToJsonString()}");
        } 
      }
      return result;
     
    }

    private static Array ProcessJsArray(JsonArray asArray)
    {
      //TODO: should I just give this up and turn everything into an object[]?
      if (!asArray.Any())
        return Array.Empty<object>();

      var arr = Array.CreateInstance(GetArrType(asArray), asArray.Count);

      for (var i = 0; i < asArray.Count; i++)
      {
        switch (asArray[i].GetValueKind())
        {
          case JsonValueKind.String:
            arr.SetValue(asArray[i].AsValue().GetValue<string>(), i);
            break;
          case JsonValueKind.Number:
            if (asArray[i].AsValue().TryGetValue<int>(out var x))
              arr.SetValue(x, i);
            else
              arr.SetValue(asArray[i].AsValue().GetValue<double>(), i);
            break;
          case JsonValueKind.True:
            arr.SetValue(true, i);
            break;
          case JsonValueKind.False:
            arr.SetValue(true, i);
            break;
          case JsonValueKind.Null:
            arr.SetValue(null, i);
            break;
          case JsonValueKind.Object:
            var jo = asArray[i].AsObject();
            var theType = jo[TYPE_KEY].GetValue<string>();
            switch (theType)
            {
              case EEOR_NAME:
                arr.SetValue(ProcessJsEeor(jo), i);
                break;
              case VARIANT_LIST_NAME:
                arr.SetValue(ProcessJsVariantList(jo), i);
                break;
              case DATE_NAME:
                arr.SetValue(jo[VALUE_KEY].GetValue<DateTime>(), i);
                break;
              default:
                throw new Exception($"Unable to interpret {jo.ToJsonString()} from {asArray.ToJsonString()}.");
            }
            break;
          case JsonValueKind.Array:
            arr.SetValue(ProcessJsArray(asArray[i].AsArray()), i);
            break;
          default:
            throw new ArgumentOutOfRangeException();
        }
      }

      return arr;
    }

    private static Type GetArrType(JsonArray asArray)
    {
      if (!asArray.Any()) return typeof(object);

      var theVk = asArray.First().GetValueKind();
      if (theVk == JsonValueKind.Undefined)
        return typeof(object);

      if (theVk == JsonValueKind.Object)
      {
        var theType = asArray.First().AsObject()?[TYPE_KEY]?.GetValue<string>() ?? "";
        return theType switch
        {
          EEOR_NAME => typeof(Eeor),
          VARIANT_LIST_NAME => typeof(VariantList),
          DATE_NAME => typeof(DateTime),
          _ => typeof(object)
        };
      } 

      var isInt = false;
      if (theVk == JsonValueKind.Number)
        isInt = asArray.First().AsValue().TryGetValue<int>(out _);

      //If we somehow have a mix, just be an object
      for (var i = 1; i < asArray.Count(); i++)
      {
        var vk = asArray[i].GetValueKind();
        if (vk != theVk)
          return typeof(object);
        if (isInt && vk == JsonValueKind.Number)
          isInt = asArray[i].AsValue().TryGetValue<int>(out _);
      }

      switch (theVk)
      {
        case JsonValueKind.Undefined:
        case JsonValueKind.Object:
        case JsonValueKind.Null:
          return typeof(object);
        case JsonValueKind.Array:
          //Well this is convoluted.
          var x = Array.CreateInstance(GetArrType(asArray.First().AsArray()), 0);
          return x.GetType();
        case JsonValueKind.String:
          return typeof(string);
        case JsonValueKind.Number:
          return isInt ? typeof(int) : typeof(double);
        case JsonValueKind.True:
        case JsonValueKind.False:
          return typeof(bool);
        default:
          throw new ArgumentOutOfRangeException();
      }
    }

    private static VariantList ProcessJsVariantList(JsonObject jo)
    {
      //TODO
      return new VariantList();
    }

    private static JsonObject ProcessEeor(Eeor eeor)
    {
      var result = new JsonObject(JNO) { { TYPE_KEY, JsonValue.Create(EEOR_NAME, JNO) } };

      //Getting it to write a double like 612.0 is a pain. It will write 612.0 as 612, which will be read as an int.
      foreach (var k in eeor.GetAllDoubleKeys())
        result.Add(k, JsonValue.Create<double>(eeor.GetDouble(k), JNO));
      foreach (var k in eeor.GetAllCurrencyKeys())
        result.Add(k, JsonValue.Create<decimal>(eeor.GetCurrency(k), JNO));
      foreach (var k in eeor.GetAllFloatKeys())
        result.Add(k, JsonValue.Create<float>(eeor.GetFloat(k), JNO));
      foreach (var k in eeor.GetAllIntegerKeys())
        result.Add(k, JsonValue.Create(eeor.GetInt(k), JNO));
      foreach (var k in eeor.GetAllShortKeys())
        result.Add(k, JsonValue.Create(eeor.GetShort(k), JNO));
      foreach (var k in eeor.GetAllBooleanKeys())
        result.Add(k, JsonValue.Create(eeor.GetBool(k), JNO));
      foreach (var k in eeor.GetAllByteKeys())
        result.Add(k, JsonValue.Create(eeor.GetByte(k), JNO));
      foreach (var k in eeor.GetAllArrayKeys())
        result.Add(k, ProcessArray(eeor.GetArray(k)));
      foreach (var k in eeor.GetAllComObjectKeys())
        result.Add(k, JsonValue.Create(eeor.GetComObject(k), JNO));
      foreach (var k in eeor.GetAllErrorKeys())
        result.Add(k, JsonValue.Create(eeor.GetError(k), JNO));
      foreach (var k in eeor.GetAllStringKeys())
        result.Add(k, JsonValue.Create(eeor.GetString(k), JNO));

      foreach (var k in eeor.GetAllNullKeys())
        result.Add(k, null);
      foreach (var k in eeor.GetAllEmptyKeys())
        result.Add(k, null);

      foreach (var k in eeor.GetAllDateKeys())
      {
        var dateObject = new JsonObject(JNO)
        {
          { TYPE_KEY, JsonValue.Create(DATE_NAME, JNO) },
          { VALUE_KEY, JsonValue.Create(eeor.GetDate(k), JNO) }
        };
        result.Add(k, dateObject);
      }

      foreach (var k in eeor.GetAllDictionaryKeys())
        result.Add(k, ProcessEeor(eeor.GetDictionary(k)));

      foreach (var k in eeor.GetAllListKeys())
      {
        result.Add(k, ProcessList(eeor.GetList(k)));
      }

      return result;
    }

    private static JsonArray ProcessArray(Array arr)
    {
      var result = new JsonArray(JNO);
      foreach (var item in arr)
      {
        switch (item)
        {
          case Eeor dictionary:
            result.Add(ProcessEeor(dictionary));
            break;
          case VariantList subList:
            result.Add(ProcessList(subList));
            break;
          case Array subArray:
            result.Add(ProcessArray(subArray));
            break;
          default:
            result.Add(JsonValue.Create(item, JNO));
            break;
        }
      }

      return result;
    } 

    private static JsonNode ProcessList(VariantList vl)
    {
      var innerArray = new JsonArray(JNO);
      var result = new JsonObject(JNO)
      {
        { TYPE_KEY, JsonValue.Create(VARIANT_LIST_NAME, JNO) },
        { VALUE_KEY, innerArray }
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

      return result;
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

  /// <summary>
  /// By default, it will serialize 413.0 as 413, which won't round trip if we don't know the type ahead of time.
  /// </summary>
  public class DoubleConverter : JsonConverter<double>
  {
    public override double Read(ref Utf8JsonReader reader, Type typeToConvert,
      JsonSerializerOptions options)
    {
      reader.TryGetDouble(out double result);
      return result;
    }

    public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
    {
      writer.WriteRawValue($"{value:F1}");
    }
  }

  public class FloatConverter : JsonConverter<float>
  {
    public override float Read(ref Utf8JsonReader reader, Type typeToConvert,
      JsonSerializerOptions options)
    {
      reader.TryGetSingle(out float result);
      return result;
    }

    public override void Write(Utf8JsonWriter writer, float value, JsonSerializerOptions options)
    {
      writer.WriteRawValue($"{value:F1}");
    }
  }
  public class DecimalConverter : JsonConverter<decimal>
  {
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert,
      JsonSerializerOptions options)
    {
      reader.TryGetDecimal(out decimal result);
      return result;
    }

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
    {
      writer.WriteRawValue($"{value:F1}");
    }
  }

}
