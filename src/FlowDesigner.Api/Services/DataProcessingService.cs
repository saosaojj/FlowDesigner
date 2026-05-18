using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FlowDesigner.Api.Services;

public class DataProcessingService
{
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ISerializer _yamlSerializer;
    private readonly IDeserializer _yamlDeserializer;

    public DataProcessingService()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };

        _yamlSerializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
    }

    public async Task<object> ProcessJsonAsync(string input, JsonOperation operation)
    {
        return await Task.Run<object>(() =>
        {
            try
            {
                switch (operation)
                {
                    case JsonOperation.Parse:
                        return JsonSerializer.Deserialize<object>(input, _jsonOptions) ?? input;

                    case JsonOperation.Stringify:
                        return JsonSerializer.Serialize(input, _jsonOptions);

                    case JsonOperation.Format:
                        return JsonSerializer.Serialize(
                            JsonSerializer.Deserialize<object>(input, _jsonOptions),
                            new JsonSerializerOptions { WriteIndented = true });

                    case JsonOperation.Minify:
                        return JsonSerializer.Serialize(
                            JsonSerializer.Deserialize<object>(input, _jsonOptions));

                    case JsonOperation.GetProperty:
                        return input;

                    default:
                        return input;
                }
            }
            catch (Exception ex)
            {
                return new { error = ex.Message };
            }
        });
    }

    public async Task<object> ProcessCsvAsync(string input, CsvOperation operation)
    {
        return await Task.Run<object>(() =>
        {
            try
            {
                switch (operation)
                {
                    case CsvOperation.Parse:
                        return ParseCsvToArray(input);

                    case CsvOperation.Stringify:
                        return ConvertToCsv(input);

                    case CsvOperation.ToObjects:
                        return ParseCsvToObjects(input);

                    default:
                        return input;
                }
            }
            catch (Exception ex)
            {
                return new { error = ex.Message };
            }
        });
    }

    private List<Dictionary<string, string>> ParseCsvToArray(string input)
    {
        var lines = input.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) return new List<Dictionary<string, string>>();

        var headers = ParseCsvLine(lines[0]);
        var result = new List<Dictionary<string, string>>();

        for (int i = 1; i < lines.Length; i++)
        {
            var values = ParseCsvLine(lines[i]);
            var row = new Dictionary<string, string>();
            for (int j = 0; j < headers.Length && j < values.Length; j++)
            {
                row[headers[j]] = values[j];
            }
            result.Add(row);
        }

        return result;
    }

    private string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        foreach (var c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        result.Add(current.ToString().Trim());

        return result.ToArray();
    }

    private string ConvertToCsv(object input)
    {
        if (input is IEnumerable<object> objects)
        {
            var list = objects.Take(100).ToList();
            if (list.Count == 0) return "";

            var headers = list[0] is Dictionary<string, object> dict
                ? dict.Keys.ToArray()
                : new[] { "value" };

            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", headers));

            foreach (var item in list)
            {
                var values = item is Dictionary<string, object> d
                    ? headers.Select(h => d.GetValueOrDefault(h)?.ToString() ?? "").ToArray()
                    : new[] { item?.ToString() ?? "" };

                sb.AppendLine(string.Join(",", values.Select(v => $"\"{v}\"")));
            }

            return sb.ToString();
        }

        return input?.ToString() ?? "";
    }

    private List<Dictionary<string, string>> ParseCsvToObjects(string input)
    {
        return ParseCsvToArray(input);
    }

    public async Task<object> ProcessXmlAsync(string input, XmlOperation operation)
    {
        return await Task.Run<object>(() =>
        {
            try
            {
                switch (operation)
                {
                    case XmlOperation.Parse:
                        var doc = XDocument.Parse(input);
                        return ConvertXmlToDict(doc.Root);

                    case XmlOperation.ToJson:
                        var xmlDoc = XDocument.Parse(input);
                        return JsonSerializer.Serialize(
                            ConvertXmlToDict(xmlDoc.Root), _jsonOptions);

                    case XmlOperation.FromJson:
                        var json = JsonSerializer.Deserialize<Dictionary<string, object>>(input, _jsonOptions);
                        return ConvertDictToXml(json);

                    case XmlOperation.Stringify:
                        return input;

                    default:
                        return input;
                }
            }
            catch (Exception ex)
            {
                return new { error = ex.Message };
            }
        });
    }

    private object ConvertXmlToDict(XElement? element)
    {
        if (element == null) return new { };

        var dict = new Dictionary<string, object>();

        if (!element.HasElements)
        {
            return element.Value;
        }

        foreach (var child in element.Elements())
        {
            var name = child.Name.ToString();
            var value = ConvertXmlToDict(child);

            if (dict.ContainsKey(name))
            {
                if (dict[name] is List<object> list)
                {
                    list.Add(value);
                }
                else
                {
                    dict[name] = new List<object> { dict[name], value };
                }
            }
            else
            {
                dict[name] = value;
            }
        }

        return dict;
    }

    private string ConvertDictToXml(object? input)
    {
        if (input is Dictionary<string, object> dict)
        {
            var doc = new XDocument(
                new XElement("root",
                    dict.Select(kvp => new XElement(kvp.Key, kvp.Value))
                )
            );
            return doc.ToString();
        }
        return "<root/>";
    }

    public async Task<object> ProcessYamlAsync(string input, YamlOperation operation)
    {
        return await Task.Run<object>(() =>
        {
            try
            {
                switch (operation)
                {
                    case YamlOperation.Parse:
                        return _yamlDeserializer.Deserialize<object>(input);

                    case YamlOperation.Stringify:
                        return _yamlSerializer.Serialize(input);

                    case YamlOperation.ToJson:
                        var obj = _yamlDeserializer.Deserialize<object>(input);
                        return JsonSerializer.Serialize(obj, _jsonOptions);

                    case YamlOperation.FromJson:
                        var json = JsonSerializer.Deserialize<object>(input, _jsonOptions);
                        return _yamlSerializer.Serialize(json);

                    default:
                        return input;
                }
            }
            catch (Exception ex)
            {
                return new { error = ex.Message };
            }
        });
    }

    public async Task<object> ProcessArrayAsync(ArrayOperation operation, object input, Dictionary<string, object> options)
    {
        return await Task.Run<object>(() =>
        {
            try
            {
                if (input is not IEnumerable<object> array)
                {
                    return new { error = "输入必须是数组" };
                }

                var list = array.ToList();

                switch (operation)
                {
                    case ArrayOperation.Sort:
                        var key = options.GetValueOrDefault("key")?.ToString();
                        var descending = options.GetValueOrDefault("descending")?.ToString() == "true";
                        return SortArray(list, key, descending);

                    case ArrayOperation.Filter:
                        var filterKey = options.GetValueOrDefault("key")?.ToString() ?? "";
                        var filterValue = options.GetValueOrDefault("value")?.ToString() ?? "";
                        return FilterArray(list, filterKey, filterValue);

                    case ArrayOperation.Group:
                        var groupKey = options.GetValueOrDefault("key")?.ToString() ?? "";
                        return GroupArray(list, groupKey);

                    case ArrayOperation.Unique:
                        return list.Distinct().ToList();

                    case ArrayOperation.Reverse:
                        return list.AsEnumerable().Reverse().ToList();

                    case ArrayOperation.Slice:
                        var start = Convert.ToInt32(options.GetValueOrDefault("start") ?? 0);
                        var end = Convert.ToInt32(options.GetValueOrDefault("end") ?? list.Count);
                        return list.Skip(start).Take(end - start).ToList();

                    case ArrayOperation.First:
                        return list.FirstOrDefault() ?? new { };

                    case ArrayOperation.Last:
                        return list.LastOrDefault() ?? new { };

                    default:
                        return list;
                }
            }
            catch (Exception ex)
            {
                return new { error = ex.Message };
            }
        });
    }

    private List<object> SortArray(List<object> list, string? key, bool descending)
    {
        if (string.IsNullOrEmpty(key))
        {
            return descending
                ? list.OrderByDescending(x => x?.ToString()).ToList<object>()
                : list.OrderBy(x => x?.ToString()).ToList<object>();
        }

        return descending
            ? list.OrderByDescending(x => GetPropertyValue(x, key)).ToList<object>()
            : list.OrderBy(x => GetPropertyValue(x, key)).ToList<object>();
    }

    private object? GetPropertyValue(object obj, string property)
    {
        if (obj is Dictionary<string, object> dict)
        {
            return dict.GetValueOrDefault(property);
        }
        return null;
    }

    private List<object> FilterArray(List<object> list, string key, string value)
    {
        if (string.IsNullOrEmpty(key))
        {
            return list.Where(x => x?.ToString()?.Contains(value) == true).ToList<object>();
        }

        return list.Where(x =>
        {
            var propValue = GetPropertyValue(x, key)?.ToString();
            return propValue?.Contains(value) == true;
        }).ToList<object>();
    }

    private Dictionary<string, List<object>> GroupArray(List<object> list, string key)
    {
        var result = new Dictionary<string, List<object>>();

        foreach (var item in list)
        {
            var groupKey = GetPropertyValue(item, key)?.ToString() ?? "other";
            if (!result.ContainsKey(groupKey))
            {
                result[groupKey] = new List<object>();
            }
            result[groupKey].Add(item);
        }

        return result;
    }

    public async Task<object> ProcessSplitAsync(string input, SplitOperation operation)
    {
        return await Task.Run<object>(() =>
        {
            try
            {
                switch (operation)
                {
                    case SplitOperation.ByChar:
                        return input.Split(',').Select(s => s.Trim()).ToList();

                    case SplitOperation.ByLine:
                        return input.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => s.Trim()).ToList();

                    case SplitOperation.ByRegex:
                        return input.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => s.Trim()).ToList();

                    case SplitOperation.Chunk:
                        var size = 10;
                        var chunks = new List<object>();
                        for (int i = 0; i < input.Length; i += size)
                        {
                            chunks.Add(input.Substring(i, Math.Min(size, input.Length - i)));
                        }
                        return chunks;

                    default:
                        return new List<string> { input };
                }
            }
            catch (Exception ex)
            {
                return new { error = ex.Message };
            }
        });
    }

    public async Task<object> ProcessJoinAsync(IEnumerable<object> input, JoinOperation operation, string separator = ",")
    {
        return await Task.Run<object>(() =>
        {
            try
            {
                switch (operation)
                {
                    case JoinOperation.WithSeparator:
                        return string.Join(separator, input.Select(x => x?.ToString() ?? ""));

                    case JoinOperation.ArrayToString:
                        return string.Join("", input.Select(x => x?.ToString() ?? ""));

                    case JoinOperation.Object:
                        return input.ToList();

                    default:
                        return input;
                }
            }
            catch (Exception ex)
            {
                return new { error = ex.Message };
            }
        });
    }

    public async Task<object> ProcessStringAsync(string input, StringOperation operation, Dictionary<string, object> options)
    {
        return await Task.Run<object>(() =>
        {
            try
            {
                switch (operation)
                {
                    case StringOperation.Uppercase:
                        return input.ToUpper();

                    case StringOperation.Lowercase:
                        return input.ToLower();

                    case StringOperation.Trim:
                        return input.Trim();

                    case StringOperation.Replace:
                        var search = options.GetValueOrDefault("search")?.ToString() ?? "";
                        var replacement = options.GetValueOrDefault("replacement")?.ToString() ?? "";
                        return input.Replace(search, replacement);

                    case StringOperation.Substring:
                        var start = Convert.ToInt32(options.GetValueOrDefault("start") ?? 0);
                        var length = Convert.ToInt32(options.GetValueOrDefault("length") ?? input.Length);
                        return input.Substring(start, Math.Min(length, input.Length - start));

                    case StringOperation.PadLeft:
                        var padLength = Convert.ToInt32(options.GetValueOrDefault("length") ?? 0);
                        var padChar = options.GetValueOrDefault("char")?.ToString() ?? " ";
                        return input.PadLeft(padLength, padChar[0]);

                    case StringOperation.PadRight:
                        var padLengthR = Convert.ToInt32(options.GetValueOrDefault("length") ?? 0);
                        var padCharR = options.GetValueOrDefault("char")?.ToString() ?? " ";
                        return input.PadRight(padLengthR, padCharR[0]);

                    case StringOperation.Reverse:
                        return new string(input.Reverse().ToArray());

                    case StringOperation.Length:
                        return input.Length;

                    default:
                        return input;
                }
            }
            catch (Exception ex)
            {
                return new { error = ex.Message };
            }
        });
    }

    public async Task<object> ProcessBatchAsync(object input, BatchOperation operation, Dictionary<string, object> options)
    {
        return await Task.Run<object>(() =>
        {
            try
            {
                switch (operation)
                {
                    case BatchOperation.GroupCount:
                        return GroupByCount(input);

                    case BatchOperation.GroupTime:
                        return GroupByTime(input);

                    case BatchOperation.Split:
                        return SplitIntoBatches(input);

                    default:
                        return input;
                }
            }
            catch (Exception ex)
            {
                return new { error = ex.Message };
            }
        });
    }

    private List<object> GroupByCount(object input)
    {
        if (input is IEnumerable<object> array)
        {
            var list = array.ToList();
            return new List<object> { new { count = list.Count, items = list } };
        }
        return new List<object> { input };
    }

    private object GroupByTime(object input)
    {
        return new { timestamp = DateTime.UtcNow, data = input };
    }

    private List<object> SplitIntoBatches(object input)
    {
        if (input is IEnumerable<object> array)
        {
            var list = array.ToList();
            var batchSize = 10;
            var batches = new List<object>();

            for (int i = 0; i < list.Count; i += batchSize)
            {
                batches.Add(list.Skip(i).Take(batchSize).ToList());
            }

            return batches;
        }
        return new List<object> { input };
    }
}

public enum JsonOperation { Parse, Stringify, Format, Minify, GetProperty }
public enum CsvOperation { Parse, Stringify, ToObjects }
public enum XmlOperation { Parse, ToJson, FromJson, Stringify }
public enum YamlOperation { Parse, Stringify, ToJson, FromJson }
public enum ArrayOperation { Sort, Filter, Group, Unique, Reverse, Slice, First, Last }
public enum SplitOperation { ByChar, ByLine, ByRegex, Chunk }
public enum JoinOperation { WithSeparator, ArrayToString, Object }
public enum StringOperation { Uppercase, Lowercase, Trim, Replace, Substring, PadLeft, PadRight, Reverse, Length }
public enum BatchOperation { GroupCount, GroupTime, Split }
