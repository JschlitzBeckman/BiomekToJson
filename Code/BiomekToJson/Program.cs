using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using System.Xml;

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

using OthrosNet;
//using Othros;
using VariantList = OthrosNet.VariantList;
using IEEOR = OthrosNet.IEEOR;
using World;

namespace BiomekToJson
{
  internal class Program
  {
    private const string TYPE_KEY = "$type";
    private const string VALUE_KEY = "$value";
    private const string EEOR_NAME = "Eeor";
    private const string REFERENCE_NAME = "Reference";
    private const string VARIANT_LIST_NAME = "VariantList";
    private const string DATE_NAME = "Date";

    private static Dictionary<object, string> SeenObjects = new Dictionary<object, string>();

    private static readonly JsonSerializerOptions JSO = new JsonSerializerOptions
    {
      ReadCommentHandling = JsonCommentHandling.Skip,
      PropertyNameCaseInsensitive = true,
      WriteIndented = true,
      NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,

      MaxDepth = 512,
      
      ReferenceHandler = ReferenceHandler.Preserve,
      TypeInfoResolver = new DefaultJsonTypeInfoResolver(),

      Converters = { new DoubleConverter(), new FloatConverter(), new DecimalConverter()      }

    };

    private static readonly JsonNodeOptions JNO = new JsonNodeOptions() { PropertyNameCaseInsensitive = true };

    private static readonly JsonDocumentOptions JDO = new JsonDocumentOptions()
      { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

    static void Main(string[] args)
    {
      Eeor e;
      VariantList vl;


      //var anyDots = new[] { "gumby cat",".jenny", "a.n.y.", "dots.", "(\\ '`*+?|{[()^$.#"};
      //foreach (var s in anyDots)
      //  Console.Write($"'{s}', ");
      //Console.WriteLine();
      //var joined = string.Join(".", anyDots.Select(Regex.Escape));
      //Console.WriteLine(joined);
      //var splitter = new Regex(@"(?<!\\)\.",RegexOptions.Compiled);
      //var split = splitter.Split(joined);
      //foreach (var s in split)
      //  Console.Write($"'{s}', ");
      //Console.WriteLine();
      //foreach (var s in split.Select(Regex.Unescape))
      //  Console.Write($"'{s}', ");
      //Console.WriteLine();

      var vll = new VariantList();
      vll.Add(123);

      Console.WriteLine("----------------------------------------------");
      var test = new Eeor();
      

      test.Put("Hello", "World!");
      test.Put("int", 413);
      //test.Put("nan", Double.NaN);
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
      test.Put("vlArray", new[] { MakeList (999)});
      test.Put("object", new Eeor());

      var tmp = new Eeor();
      tmp.Put("Wibble", "Wobble");
      test.Put("List", MakeList( "Foo", 123, 45.6, tmp, null, DateTime.Today, new[]{"array", "in", "a", "list"}, new Eeor(), MakeList(8576309) ));
      test.Put(":", ":");

      //escaping path names...
      var dot = new Eeor();
      var dotdot = new Eeor();
      dot.Put(".dot']dot", dotdot);
      test.Put(".dot", dot);

//TODO      test.Put("Circular", test);

      //deleted some notes about things that seem like they ought to have worked. See c6b05a8970c3c90f61e9e0f7f3372ccb8c0eee76 and before

      //TODO: consider using a breadth-first search to serialize the information and prevent cycles
      //TODO: as I go through the graph, can the references to previous objects be the json path instead of some id?
      
      var root = GetEmptyEeorJson();

      SeenObjects.Add(test, "$");
      PopulateJsonFromEeor(test, "$", root);

      var asString = root.ToJsonString(JSO);
      Console.WriteLine(asString);
      //Can we round trip? ... Sorta. decimals get squished to doubles. all int types become ints.
      //TODO: circular references are a problem
      Eeor roundTrip = JsToEeor((JsonObject)JsonNode.Parse(asString, JNO));

      var  d = 42D;
      //This will round-trip with the special Converters...
      d = roundTrip.GetDouble("double");
      Console.WriteLine($"double == {d}");
      //And this as a string
      //d = roundTrip.GetDouble("nan");
      //Console.WriteLine($"nan == {d}");


      Console.WriteLine("-------- aggregate test --------");
      //IEEOR aggTest = (IEEOR)(new Eeor ());
      //aggTest.AggregateClassName = "Biomek5.Labware"; //Fails with: Class does not support aggregation (or class object is remote) (0x80040110 (CLASS_E_NOAGGREGATION))
      ILabware lw = new World.Labware();
      //lw.ConfigureWellAmount(1, 41.3);nope
      //Console.WriteLine($"{lw.GetWellAmount(1)}"); Yeah, it seems toucing anything on ILabware assumes a working biomek environment
      var asIEeor = (IEEOR)lw;
      //Eeor eee = (Eeor)asIEeor; No
      //Eeor eee = asIEeor as Eeor;  Also no
      eee.PutBool("Known", false);

      Console.WriteLine(asIEeor.AggregateClassName);


      
      Console.ReadKey(true);
    }

    private static VariantList MakeList(params object[] items)
    {
      var result = new VariantList();
      foreach (var item in items)
      {
        result.Add(item);
      }
      return result;
    }

    private static JsonObject GetEmptyEeorJson() => new JsonObject(JNO) { { TYPE_KEY, JsonValue.Create(EEOR_NAME, JNO) } };

    private static JsonObject GetEmptyVariantListJson()
    {
      return new JsonObject(JNO)
      {
        { TYPE_KEY, JsonValue.Create(VARIANT_LIST_NAME, JNO) },
        { VALUE_KEY, new JsonArray(JNO)}
      };
    }

    private static JsonObject MakeReference(string path)
    {
      return new JsonObject(JNO)
      {
        { TYPE_KEY, JsonValue.Create(REFERENCE_NAME, JNO) }, 
        {VALUE_KEY, JsonValue.Create(path)}
      };
    }

    private static Eeor JsToEeor(JsonObject theObject, Eeor root=null)
    {
      var result = new Eeor();
      if (root == null)
        root = result;
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
              result.Put(kvp.Key, JsToEeor(jo));
            else if (theType == VARIANT_LIST_NAME)
              result.Put(kvp.Key, JsToVariantList(jo));
            else if (theType == DATE_NAME)
              result.Put(kvp.Key, jo[VALUE_KEY].GetValue<DateTime>());
            //else if (theType == REFERENCE_NAME)
            //  result.Put(kvp.Key, ...);//TODO all of this.
            else
              throw new Exception($"Unable to interpret {kvp.Key} = {kvp.Value.ToJsonString()}.");
            break;
          case JsonValueKind.Array:
            result.Put(kvp.Key, JsToArray(kvp.Value.AsArray()));
            break;
          default:
            throw new Exception($"{kvp.Key} error in {theObject.ToJsonString()}");
        } 
      }
      return result;
     
    }

    private static Array JsToArray(JsonArray asArray)
    {
      if (!asArray.Any())
        return Array.Empty<object>();

      var arr = Array.CreateInstance(GetArrType(asArray), asArray.Count);

      for (var i = 0; i < asArray.Count; i++)
      {
        if (asArray[i] == null)
        {
          arr.SetValue(null, i);
          continue;
        }
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
                arr.SetValue(JsToEeor(jo), i);
                break;
              case VARIANT_LIST_NAME:
                arr.SetValue(JsToVariantList(jo), i);
                break;
              case DATE_NAME:
                arr.SetValue(jo[VALUE_KEY].GetValue<DateTime>(), i);
                break;
              default:
                throw new Exception($"Unable to interpret {jo.ToJsonString()} from {asArray.ToJsonString()}.");
            }
            break;
          case JsonValueKind.Array:
            arr.SetValue(JsToArray(asArray[i].AsArray()), i);
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

    private static VariantList JsToVariantList(JsonObject jObjVariantList)
    {
      var result = new VariantList();
      foreach (var jn in jObjVariantList[VALUE_KEY].AsArray())
      {
        if (jn == null)
        {
          result.Add(null);
          continue;
        }
        switch (jn.GetValueKind())
        {
          case JsonValueKind.String:
            result.Add(jn.AsValue().GetValue<string>());
            break;
          case JsonValueKind.Number:
            var jv = jn.AsValue();
            if (jv.TryGetValue<int>(out var i))
              result.Add(i);
            else
              result.Add(jv.GetValue<double>());
            break;
          case JsonValueKind.Null: //Can't really get here
            result.Add(null);
            break;
          case JsonValueKind.Object:
            var jo = jn.AsObject();
            var theType = jo[TYPE_KEY].GetValue<string>();
            switch (theType)
            {
              case EEOR_NAME:
                result.Add(JsToEeor(jo));
                break;
              case VARIANT_LIST_NAME:
                result.Add(JsToVariantList(jo));
                break;
              case DATE_NAME:
                result.Add(jo[VALUE_KEY].GetValue<DateTime>());
                break;
              default:
                throw new Exception($"Unable to interpret {jn.ToJsonString()} from {jObjVariantList.ToJsonString()}.");
            }
            break;
          case JsonValueKind.Array:
            result.Add(JsToArray(jn.AsArray()));
            break;
          default:
            throw new ArgumentOutOfRangeException();
        }
      } 

      return result;
    }

    private static Action<string, JsonNode> JaAdder(JsonArray ja) => (_, value) => ja.Add(value);
    private static Action<string, JsonNode> JoAdder(JsonObject jo) => jo.Add;
    private static void PopulateJsonFromEeor(Eeor source, string sourcePath, JsonObject target)
    {
      Console.WriteLine(sourcePath);

      //Getting it to write a double like 612.0 is a pain. It will write 612.0 as 612, which will be read as an int.
      foreach (var k in source.GetAllDoubleKeys())
        target.Add(k, JsonValue.Create<double>(source.GetDouble(k), JNO));
      foreach (var k in source.GetAllCurrencyKeys())
        target.Add(k, JsonValue.Create<decimal>(source.GetCurrency(k), JNO));
      foreach (var k in source.GetAllFloatKeys())
        target.Add(k, JsonValue.Create<float>(source.GetFloat(k), JNO));
      foreach (var k in source.GetAllIntegerKeys())
        target.Add(k, JsonValue.Create(source.GetInt(k), JNO));
      foreach (var k in source.GetAllShortKeys())
        target.Add(k, JsonValue.Create(source.GetShort(k), JNO));
      foreach (var k in source.GetAllBooleanKeys())
        target.Add(k, JsonValue.Create(source.GetBool(k), JNO));
      foreach (var k in source.GetAllByteKeys())
        target.Add(k, JsonValue.Create(source.GetByte(k), JNO));
      foreach (var k in source.GetAllComObjectKeys())
        target.Add(k, JsonValue.Create(source.GetComObject(k), JNO));
      foreach (var k in source.GetAllErrorKeys())
        target.Add(k, JsonValue.Create(source.GetError(k), JNO));
      foreach (var k in source.GetAllStringKeys())
        target.Add(k, JsonValue.Create(source.GetString(k), JNO));
      foreach (var k in source.GetAllNullKeys())
        target.Add(k, null);
      foreach (var k in source.GetAllEmptyKeys())
        target.Add(k, null);
      foreach (var k in source.GetAllDateKeys())
      {
        var dateObject = new JsonObject(JNO)
        {
          { TYPE_KEY, JsonValue.Create(DATE_NAME, JNO) },
          { VALUE_KEY, JsonValue.Create(source.GetDate(k), JNO) }
        };
        target.Add(k, dateObject);
      }

      foreach (var k in source.GetAllDictionaryKeys())
      {
        var item = source.GetDictionary(k);
        if (SeenObjects.ContainsKey(item))
        {
          target.Add(k, MakeReference(SeenObjects[item]));
        }
        else
        {
          var path = PathAdd(sourcePath, k);
          var subItem = GetEmptyEeorJson();
          target.Add(k, subItem);
          PopulateJsonFromEeor(item, path, subItem);
        }
      }

      foreach (var k in source.GetAllListKeys())
      {
        var path = PathAdd(sourcePath, k);
        var subItem = GetEmptyVariantListJson();
        PopulateJsonFromVariantList(source.GetList(k), path, subItem);
      }

      foreach (var k in source.GetAllArrayKeys())
      {
        var path = PathAdd(sourcePath , k);
        var subItem = new JsonArray(JNO);
        target.Add(k, subItem);
        PopulateJsonFromArray(source.GetArray(k), path, subItem);
      }

      //target.Add("$path", path);
    }


    private static string PathAdd(string sourcePath, string key) => sourcePath + "." + Regex.Escape(key);

    private static void PopulateJsonFromArray(Array source, string sourcePath, JsonArray target)
    {
      Console.WriteLine(sourcePath);

      var index = 0;
      foreach (var item in source)
      {
        var path = PathAdd(sourcePath, index.ToString());
        switch (item)
        {
          case Eeor dictionary:
            var targetObj = GetEmptyEeorJson();
            target.Add(targetObj);
            PopulateJsonFromEeor(dictionary, path, targetObj);
            break;
          case VariantList subList:
            var targetList = GetEmptyVariantListJson();
            target.Add(targetList);
            PopulateJsonFromVariantList(subList, path, targetList);
            break;
          case Array subArray:
            var targetArr = new JsonArray(JNO);
            target.Add(targetArr);
            PopulateJsonFromArray(subArray, path, targetArr);
            break;
          case null:
            target.Add(null);
            break;
          default:
            target.Add(JsonValue.Create(item, JNO));
            break;
        }

        index++;
      }
    } 

    private static void PopulateJsonFromVariantList(VariantList source, string sourcePath, JsonObject target)
    {
      Console.WriteLine(sourcePath);

      var innerArray = target[VALUE_KEY]!.AsArray();
      for (var index = 0; index < source.Count; index++)
      {
        var item = source[index];
        var path = PathAdd(sourcePath, index.ToString());
        switch (item)
        {
          case Eeor dictionary:
            var subDict = GetEmptyEeorJson();
            innerArray.Add(subDict);
            PopulateJsonFromEeor(dictionary, path, subDict);
            break;
          case VariantList subList:
            var subItem = GetEmptyVariantListJson();
            innerArray.Add(subItem);
            PopulateJsonFromVariantList(subList, path, subItem);
            break;
          case Array subArray:
            var targetArr = new JsonArray(JNO);
            innerArray.Add(targetArr);
            PopulateJsonFromArray(subArray, path, targetArr);
            break;
          case null:
            innerArray.Add(null);
            break;
          default:
            innerArray.Add<JsonValue>(JsonValue.Create(item, JNO));
            break;
        }
      }
    }

    private static void AddThing(VariantList vParent, JsonObject jParent, string itemName, JsonNode item)
    {}
    private static void AddThing(Eeor vParent, JsonObject jParent, string itemName, JsonNode item)
    {}
    private static void AddThing(Array vParent, JsonArray jParent, string itemName, JsonNode item)
    {}
  }

 
  #region hoop-jumping to make sure doubles that happen to be integers don't get serialized as integers
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
  #endregion

}
