using System.Text.RegularExpressions;

namespace Cmf.Custom.Common.SD.Utilities.Extensions;

public static class GenericExtensions
{
    public static bool IgnoreCaseEquals(this string? str1, string? str2)
    {
        return str1 is not null && str1.Equals(str2, StringComparison.CurrentCultureIgnoreCase);
    }

    public static string? TrimStart(this string? target, string? trimString, bool trimFirstOccurrence = false)
    {
        if (string.IsNullOrEmpty(trimString))
        {
            return target;
        }

        string? result = target;
        while (result!.StartsWith(trimString))
        {
            result = result[trimString.Length..];
            if (trimFirstOccurrence)
            {
                break;
            }
        }

        return result;
    }

    public static bool IsNullOrWhiteSpace(this string? text)
    {
        return string.IsNullOrWhiteSpace(text);
    }

    public static string AddSpacesToSentenceBeforeCapitalLetters(this string text)
    {
        return Regex.Replace(text, "([a-z])([A-Z])", "$1 $2");
    }

    public static bool IsNullOrEmpty<T>(this IEnumerable<T>? enumerable)
    {
        if (enumerable is null)
        {
            return true;
        }

        if (enumerable is ICollection<T> collection)
        {
            return collection.Count < 1;
        }

        return !enumerable.Any();
    }

    public static bool In<T>(this T obj, params T[] args)
    {
        return args.Contains(obj);
    }

    public static string GetNextBase36Value(this string value, int maxNumberOfDigits, int valueToAdd = 1,
        string charList = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ", bool startWithFirstValue = true,
        bool repeatWithFirstValue = false)
    {
        string nextValue = value;

        if (string.IsNullOrWhiteSpace(nextValue))
        {
            if (startWithFirstValue)
            {
                nextValue = string.Concat(new string(charList[0], maxNumberOfDigits));
            }
            else
            {
                nextValue = string.Concat(new string(charList[0], maxNumberOfDigits - 1), charList[1]);
            }
        }
        else
        {
            if (nextValue.Length > maxNumberOfDigits)
            {
                nextValue = nextValue.Remove(maxNumberOfDigits, nextValue.Length - maxNumberOfDigits);
            }
            else if (nextValue.Length < maxNumberOfDigits)
            {
                nextValue = string.Concat(nextValue, new string(charList[0], maxNumberOfDigits - nextValue.Length));
            }

            for (int j = 0; j < valueToAdd; j++)
            {
                bool addValue = true;

                for (int i = maxNumberOfDigits - 1; i >= 0 && addValue; i--)
                {
                    int position = charList.IndexOf(nextValue[i]);

                    if (position != charList.Length - 1)
                    {
                        nextValue = nextValue.Remove(i, 1).Insert(i, charList[position + 1].ToString());
                        addValue = false;
                    }
                    else if (repeatWithFirstValue)
                    {
                        nextValue = nextValue.Remove(i, 1).Insert(i, charList[0].ToString());
                    }
                    else
                    {
                        nextValue = nextValue.Remove(i, 1).Insert(i, charList[i == maxNumberOfDigits - 1 ? 1 : 0].ToString());
                    }
                }
            }
        }

        return nextValue;
    }

    public static bool IsNumericType(this Type type)
    {
        return GetNumericTypeKind(type) != 0;
    }

    public static int GetNumericTypeKind(this Type type)
    {
        type = GetNonNullableType(type);
        if (type.IsEnum)
        {
            return 0;
        }

        return Type.GetTypeCode(type) switch
        {
            TypeCode.Char or TypeCode.Single or TypeCode.Double or TypeCode.Decimal => 1,
            TypeCode.SByte or TypeCode.Int16 or TypeCode.Int32 or TypeCode.Int64 => 2,
            TypeCode.Byte or TypeCode.UInt16 or TypeCode.UInt32 or TypeCode.UInt64 => 3,
            _ => 0
        };
    }

    public static Type GetNonNullableType(this Type type)
    {
        return IsNullableType(type) ? type.GetGenericArguments()[0] : type;
    }

    public static bool IsNullableType(this Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
    }

#pragma warning disable CS8714
    public static void AddRange<T, S>(this Dictionary<T, S> source, Dictionary<T, S> collection)
    {
        if (collection is null)
        {
            throw new ArgumentNullException("Empty collection");
        }

        foreach (KeyValuePair<T, S> item in collection)
        {
            if (!source.ContainsKey(item.Key))
            {
                source.Add(item.Key, item.Value);
            }
        }
    #pragma warning restore CS8714
    }
}
