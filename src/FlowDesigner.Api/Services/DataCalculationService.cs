using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Data;

namespace FlowDesigner.Api.Services;

public class DataCalculationService
{
    public async Task<object> CalculateAsync(CalculationType type, object input, Dictionary<string, object> parameters)
    {
        return await Task.Run(() =>
        {
            try
            {
                switch (type)
                {
                    case CalculationType.Add:
                    case CalculationType.Subtract:
                    case CalculationType.Multiply:
                    case CalculationType.Divide:
                    case CalculationType.Modulo:
                    case CalculationType.Power:
                    case CalculationType.Sqrt:
                    case CalculationType.Abs:
                    case CalculationType.Round:
                    case CalculationType.Floor:
                    case CalculationType.Ceiling:
                    case CalculationType.Log:
                    case CalculationType.Ln:
                    case CalculationType.Sin:
                    case CalculationType.Cos:
                    case CalculationType.Tan:
                    case CalculationType.Asin:
                    case CalculationType.Acos:
                    case CalculationType.Atan:
                        return PerformMathOperation(type, input, parameters);

                    case CalculationType.Min:
                    case CalculationType.Max:
                    case CalculationType.Average:
                    case CalculationType.Sum:
                    case CalculationType.Count:
                    case CalculationType.Median:
                    case CalculationType.StdDev:
                    case CalculationType.Variance:
                        return PerformAggregateOperation(type, input, parameters);

                    case CalculationType.Comparison:
                        return PerformComparison(input, parameters);

                    case CalculationType.Conditional:
                        return PerformConditional(input, parameters);

                    case CalculationType.AND:
                    case CalculationType.OR:
                    case CalculationType.NOT:
                    case CalculationType.XOR:
                        return PerformLogicalOperation(type, input, parameters);

                    case CalculationType.BitwiseAND:
                    case CalculationType.BitwiseOR:
                    case CalculationType.BitwiseNOT:
                    case CalculationType.BitwiseXOR:
                    case CalculationType.LeftShift:
                    case CalculationType.RightShift:
                        return PerformBitwiseOperation(type, input, parameters);

                    case CalculationType.FormatNumber:
                        return FormatNumber(input, parameters);

                    case CalculationType.ChangeNumber:
                        return ChangeNumber(input, parameters);

                    case CalculationType.Clone:
                        return CloneValue(input);

                    case CalculationType.Keys:
                        return GetObjectKeys(input);

                    case CalculationType.Values:
                        return GetObjectValues(input);

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

    private object PerformMathOperation(CalculationType type, object input, Dictionary<string, object> parameters)
    {
        var value = Convert.ToDouble(input);

        switch (type)
        {
            case CalculationType.Add:
                var addValue = Convert.ToDouble(parameters.GetValueOrDefault("value") ?? 0);
                return value + addValue;

            case CalculationType.Subtract:
                var subValue = Convert.ToDouble(parameters.GetValueOrDefault("value") ?? 0);
                return value - subValue;

            case CalculationType.Multiply:
                var mulValue = Convert.ToDouble(parameters.GetValueOrDefault("value") ?? 1);
                return value * mulValue;

            case CalculationType.Divide:
                var divValue = Convert.ToDouble(parameters.GetValueOrDefault("value") ?? 1);
                return divValue != 0 ? value / divValue : double.NaN;

            case CalculationType.Modulo:
                var modValue = Convert.ToDouble(parameters.GetValueOrDefault("value") ?? 1);
                return modValue != 0 ? value % modValue : double.NaN;

            case CalculationType.Power:
                var powValue = Convert.ToDouble(parameters.GetValueOrDefault("exponent") ?? 2);
                return Math.Pow(value, powValue);

            case CalculationType.Sqrt:
                return value >= 0 ? Math.Sqrt(value) : double.NaN;

            case CalculationType.Abs:
                return Math.Abs(value);

            case CalculationType.Round:
                var decimals = Convert.ToInt32(parameters.GetValueOrDefault("decimals") ?? 0);
                return Math.Round(value, decimals);

            case CalculationType.Floor:
                return Math.Floor(value);

            case CalculationType.Ceiling:
                return Math.Ceiling(value);

            case CalculationType.Log:
                var logBase = Convert.ToDouble(parameters.GetValueOrDefault("base") ?? 10);
                return Math.Log(value, logBase);

            case CalculationType.Ln:
                return Math.Log(value);

            case CalculationType.Sin:
                return Math.Sin(value);

            case CalculationType.Cos:
                return Math.Cos(value);

            case CalculationType.Tan:
                return Math.Tan(value);

            case CalculationType.Asin:
                return value >= -1 && value <= 1 ? Math.Asin(value) : double.NaN;

            case CalculationType.Acos:
                return value >= -1 && value <= 1 ? Math.Acos(value) : double.NaN;

            case CalculationType.Atan:
                return Math.Atan(value);

            default:
                return value;
        }
    }

    private object PerformAggregateOperation(CalculationType type, object input, Dictionary<string, object> parameters)
    {
        List<double> values;

        if (input is IEnumerable<object> array)
        {
            values = array
                .Select(x => Convert.ToDouble(x))
                .ToList();
        }
        else if (input is double single)
        {
            values = new List<double> { single };
        }
        else
        {
            values = new List<double> { Convert.ToDouble(input) };
        }

        switch (type)
        {
            case CalculationType.Min:
                return values.Count > 0 ? values.Min() : 0;

            case CalculationType.Max:
                return values.Count > 0 ? values.Max() : 0;

            case CalculationType.Average:
                return values.Count > 0 ? values.Average() : 0;

            case CalculationType.Sum:
                return values.Sum();

            case CalculationType.Count:
                return values.Count;

            case CalculationType.Median:
                if (values.Count == 0) return 0;
                var sorted = values.OrderBy(x => x).ToList();
                var mid = sorted.Count / 2;
                return sorted.Count % 2 != 0
                    ? sorted[mid]
                    : (sorted[mid - 1] + sorted[mid]) / 2;

            case CalculationType.StdDev:
                if (values.Count < 2) return 0;
                var avg = values.Average();
                var sumOfSquares = values.Sum(x => Math.Pow(x - avg, 2));
                return Math.Sqrt(sumOfSquares / (values.Count - 1));

            case CalculationType.Variance:
                if (values.Count < 2) return 0;
                var mean = values.Average();
                return values.Average(x => Math.Pow(x - mean, 2));

            default:
                return values;
        }
    }

    private object PerformComparison(object input, Dictionary<string, object> parameters)
    {
        var value = Convert.ToDouble(input);
        var compareValue = Convert.ToDouble(parameters.GetValueOrDefault("value") ?? 0);
        var operation = parameters.GetValueOrDefault("operation")?.ToString() ?? "==";

        switch (operation)
        {
            case "==":
            case "eq":
                return value == compareValue;
            case "!=":
            case "ne":
                return value != compareValue;
            case ">":
            case "gt":
                return value > compareValue;
            case ">=":
            case "gte":
                return value >= compareValue;
            case "<":
            case "lt":
                return value < compareValue;
            case "<=":
            case "lte":
                return value <= compareValue;
            case "isNaN":
                return double.IsNaN(value);
            case "isFinite":
                return double.IsFinite(value);
            case "isInteger":
                return value == Math.Floor(value);
            default:
                return false;
        }
    }

    private object PerformConditional(object input, Dictionary<string, object> parameters)
    {
        var condition = parameters.GetValueOrDefault("condition")?.ToString() ?? "";
        var trueValue = parameters.GetValueOrDefault("trueValue");
        var falseValue = parameters.GetValueOrDefault("falseValue");

        var isTrue = false;

        if (bool.TryParse(input?.ToString(), out var boolVal))
        {
            isTrue = boolVal;
        }
        else if (double.TryParse(input?.ToString(), out var numVal))
        {
            isTrue = numVal != 0;
        }
        else
        {
            isTrue = input != null && !string.IsNullOrEmpty(input.ToString());
        }

        switch (condition)
        {
            case "isNull":
                isTrue = input == null;
                break;
            case "isNotNull":
                isTrue = input != null;
                break;
            case "isEmpty":
                isTrue = input == null || string.IsNullOrEmpty(input?.ToString());
                break;
            case "isNotEmpty":
                isTrue = input != null && !string.IsNullOrEmpty(input?.ToString());
                break;
        }

        return isTrue ? trueValue ?? true : falseValue ?? false;
    }

    private object PerformLogicalOperation(CalculationType type, object input, Dictionary<string, object> parameters)
    {
        var value = Convert.ToBoolean(input);

        switch (type)
        {
            case CalculationType.AND:
                var andValue = Convert.ToBoolean(parameters.GetValueOrDefault("value") ?? false);
                return value && andValue;

            case CalculationType.OR:
                var orValue = Convert.ToBoolean(parameters.GetValueOrDefault("value") ?? false);
                return value || orValue;

            case CalculationType.NOT:
                return !value;

            case CalculationType.XOR:
                var xorValue = Convert.ToBoolean(parameters.GetValueOrDefault("value") ?? false);
                return value ^ xorValue;

            default:
                return value;
        }
    }

    private object PerformBitwiseOperation(CalculationType type, object input, Dictionary<string, object> parameters)
    {
        var value = Convert.ToInt64(input);

        switch (type)
        {
            case CalculationType.BitwiseAND:
                var andVal = Convert.ToInt64(parameters.GetValueOrDefault("value") ?? 0);
                return value & andVal;

            case CalculationType.BitwiseOR:
                var orVal = Convert.ToInt64(parameters.GetValueOrDefault("value") ?? 0);
                return value | orVal;

            case CalculationType.BitwiseNOT:
                return ~value;

            case CalculationType.BitwiseXOR:
                var xorVal = Convert.ToInt64(parameters.GetValueOrDefault("value") ?? 0);
                return value ^ xorVal;

            case CalculationType.LeftShift:
                var shiftLeft = Convert.ToInt32(parameters.GetValueOrDefault("bits") ?? 1);
                return value << shiftLeft;

            case CalculationType.RightShift:
                var shiftRight = Convert.ToInt32(parameters.GetValueOrDefault("bits") ?? 1);
                return value >> shiftRight;

            default:
                return value;
        }
    }

    private object FormatNumber(object input, Dictionary<string, object> parameters)
    {
        var value = Convert.ToDouble(input);
        var format = parameters.GetValueOrDefault("format")?.ToString() ?? "N2";
        var prefix = parameters.GetValueOrDefault("prefix")?.ToString() ?? "";
        var suffix = parameters.GetValueOrDefault("suffix")?.ToString() ?? "";

        string formatted;

        switch (format)
        {
            case "N0":
                formatted = value.ToString("N0");
                break;
            case "N1":
                formatted = value.ToString("N1");
                break;
            case "N2":
                formatted = value.ToString("N2");
                break;
            case "C0":
                formatted = value.ToString("C0");
                break;
            case "C2":
                formatted = value.ToString("C2");
                break;
            case "P0":
                formatted = value.ToString("P0");
                break;
            case "P2":
                formatted = value.ToString("P2");
                break;
            case "E":
                formatted = value.ToString("E");
                break;
            default:
                formatted = value.ToString(format);
                break;
        }

        return $"{prefix}{formatted}{suffix}";
    }

    private object ChangeNumber(object input, Dictionary<string, object> parameters)
    {
        var value = Convert.ToDouble(input);
        var changeType = parameters.GetValueOrDefault("type")?.ToString() ?? "percent";

        switch (changeType)
        {
            case "percent":
                var percent = Convert.ToDouble(parameters.GetValueOrDefault("value") ?? 0);
                return value * (1 + percent / 100);

            case "absolute":
                var absolute = Convert.ToDouble(parameters.GetValueOrDefault("value") ?? 0);
                return value + absolute;

            case "increment":
                return value + 1;

            case "decrement":
                return value - 1;

            case "double":
                return value * 2;

            case "half":
                return value / 2;

            case "square":
                return value * value;

            case "invert":
                return -value;

            default:
                return value;
        }
    }

    private object CloneValue(object input)
    {
        if (input is ICloneable cloneable)
        {
            return cloneable.Clone();
        }

        return input switch
        {
            null => null,
            string => string.Copy(input.ToString() ?? ""),
            IEnumerable<object> list => new List<object>(list),
            Dictionary<string, object> dict => new Dictionary<string, object>(dict),
            _ => input
        };
    }

    private object GetObjectKeys(object input)
    {
        if (input is Dictionary<string, object> dict)
        {
            return dict.Keys.ToList();
        }

        return new List<string>();
    }

    private object GetObjectValues(object input)
    {
        if (input is Dictionary<string, object> dict)
        {
            return dict.Values.ToList();
        }

        if (input is IEnumerable<object> array)
        {
            return array.ToList();
        }

        return new List<object> { input };
    }

    public async Task<object> EvaluateExpressionAsync(string expression, Dictionary<string, object> context)
    {
        return await Task.Run<object>(() =>
        {
            try
            {
                foreach (var key in context.Keys.ToList())
                {
                    expression = expression.Replace($"${key}", context[key]?.ToString() ?? "0");
                }

                expression = new DataTable().Compute(expression, "")?.ToString();
                return Convert.ToDouble(expression);
            }
            catch (Exception ex)
            {
                return (object)new Dictionary<string, string> { ["error"] = ex.Message };
            }
        });
    }

    public async Task<object> ProcessStatisticsAsync(IEnumerable<object> data, StatisticsType type)
    {
        return await Task.Run(() =>
        {
            var values = data.Select(x => Convert.ToDouble(x)).ToList();

            if (values.Count == 0)
            {
                return new Dictionary<string, object>
                {
                    ["error"] = "No data to process"
                };
            }

            var sorted = values.OrderBy(x => x).ToList();
            var avg = values.Average();
            var count = values.Count;
            var sum = values.Sum();
            var min = values.Min();
            var max = values.Max();

            double median;
            var mid = count / 2;
            median = count % 2 != 0 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;

            double variance = values.Sum(x => Math.Pow(x - avg, 2)) / count;
            double stdDev = Math.Sqrt(variance);

            var quartiles = new double[3];
            quartiles[0] = sorted[(int)(count * 0.25)];
            quartiles[1] = median;
            quartiles[2] = sorted[(int)(count * 0.75)];

            switch (type)
            {
                case StatisticsType.Full:
                    return new Dictionary<string, object>
                    {
                        ["count"] = count,
                        ["sum"] = sum,
                        ["avg"] = avg,
                        ["min"] = min,
                        ["max"] = max,
                        ["median"] = median,
                        ["stdDev"] = stdDev,
                        ["variance"] = variance,
                        ["range"] = max - min,
                        ["q1"] = quartiles[0],
                        ["q2"] = quartiles[1],
                        ["q3"] = quartiles[2]
                    };

                case StatisticsType.Basic:
                    return new Dictionary<string, object>
                    {
                        ["count"] = count,
                        ["sum"] = sum,
                        ["avg"] = avg,
                        ["min"] = min,
                        ["max"] = max
                    };

                case StatisticsType.Distribution:
                    var range = max - min;
                    var binCount = Math.Min(10, count);
                    var binSize = range / binCount;

                    var bins = new List<Dictionary<string, object>>();
                    for (int i = 0; i < binCount; i++)
                    {
                        var binStart = min + i * binSize;
                        var binEnd = binStart + binSize;
                        var binCount_val = values.Count(x => x >= binStart && (i == binCount - 1 ? x <= binEnd : x < binEnd));
                        bins.Add(new Dictionary<string, object>
                        {
                            ["range"] = $"{binStart:F2}-{binEnd:F2}",
                            ["count"] = binCount_val,
                            ["percentage"] = binCount_val * 100.0 / count
                        });
                    }

                    return new Dictionary<string, object>
                    {
                        ["bins"] = bins,
                        ["binCount"] = binCount
                    };

                default:
                    return new Dictionary<string, object>
                    {
                        ["avg"] = avg,
                        ["count"] = count
                    };
            }
        });
    }
}

public enum CalculationType
{
    Add, Subtract, Multiply, Divide, Modulo, Power, Sqrt, Abs, Round, Floor, Ceiling,
    Log, Ln, Sin, Cos, Tan, Asin, Acos, Atan,
    Min, Max, Average, Sum, Count, Median, StdDev, Variance,
    Comparison, Conditional,
    AND, OR, NOT, XOR,
    BitwiseAND, BitwiseOR, BitwiseNOT, BitwiseXOR, LeftShift, RightShift,
    FormatNumber, ChangeNumber, Clone, Keys, Values
}

public enum StatisticsType
{
    Full, Basic, Distribution
}
