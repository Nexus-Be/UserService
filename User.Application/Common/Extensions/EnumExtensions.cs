namespace User.Application.Common.Extensions;

using System;
using System.Collections.Generic;

/// <summary>
/// Reusable extension methods for converting between C# Enums (integers) and external representations (strings/integers).
/// </summary>
public static class EnumExtensions
{
    /// <summary>
    /// Serializes an Enum value to its PascalCase string name (e.g. UserStatus.Active -> "Active").
    /// </summary>
    public static string ToEnumString<TEnum>(this TEnum value) where TEnum : struct, Enum
    {
        return value.ToString();
    }

    /// <summary>
    /// Serializes a nullable Enum value to its string representation or fallback string when null.
    /// </summary>
    public static string ToEnumString<TEnum>(this TEnum? value, string fallbackValue = "") where TEnum : struct, Enum
    {
        return value.HasValue ? value.Value.ToString() : fallbackValue;
    }

    /// <summary>
    /// Converts an Enum value to its underlying 32-bit signed integer.
    /// </summary>
    public static int ToInt<TEnum>(this TEnum value) where TEnum : struct, Enum
    {
        return Convert.ToInt32(value);
    }

    /// <summary>
    /// Parses an external string into the strongly-typed Enum (case-insensitive).
    /// Supports PascalCase, camelCase, UPPERCASE, and SCREAMING_SNAKE_CASE.
    /// Throws ArgumentException with Sentence case English message if invalid.
    /// </summary>
    public static TEnum ToEnum<TEnum>(this string? value) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"Enum value for '{typeof(TEnum).Name}' cannot be empty. Valid values are: {string.Join(", ", Enum.GetNames<TEnum>())}.");
        }

        var trimmed = value.Trim();
        if (long.TryParse(trimmed, out _))
        {
            throw new ArgumentException($"Invalid value '{value}' for enum '{typeof(TEnum).Name}'. Valid values are: {string.Join(", ", Enum.GetNames<TEnum>())}.");
        }

        if (Enum.TryParse<TEnum>(trimmed, ignoreCase: true, out var result) && Enum.IsDefined(typeof(TEnum), result))
        {
            return result;
        }

        if (trimmed.Contains('_'))
        {
            var withoutUnderscore = trimmed.Replace("_", "");
            if (Enum.TryParse<TEnum>(withoutUnderscore, ignoreCase: true, out var resultNoUnderscore) && Enum.IsDefined(typeof(TEnum), resultNoUnderscore))
            {
                return resultNoUnderscore;
            }
        }

        throw new ArgumentException($"Invalid value '{value}' for enum '{typeof(TEnum).Name}'. Valid values are: {string.Join(", ", Enum.GetNames<TEnum>())}.");
    }

    /// <summary>
    /// Parses an external string into the strongly-typed Enum, falling back to defaultValue if invalid or empty.
    /// </summary>
    public static TEnum ToEnumOrDefault<TEnum>(this string? value, TEnum defaultValue = default) where TEnum : struct, Enum
    {
        return TryToEnum<TEnum>(value, out var result) ? result : defaultValue;
    }

    /// <summary>
    /// Tries to parse an external string into the strongly-typed Enum.
    /// Supports PascalCase, camelCase, UPPERCASE, and SCREAMING_SNAKE_CASE.
    /// </summary>
    public static bool TryToEnum<TEnum>(this string? value, out TEnum result) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = default;
            return false;
        }

        var trimmed = value.Trim();
        if (long.TryParse(trimmed, out _))
        {
            result = default;
            return false;
        }

        if (Enum.TryParse<TEnum>(trimmed, ignoreCase: true, out result) && Enum.IsDefined(typeof(TEnum), result))
        {
            return true;
        }

        if (trimmed.Contains('_'))
        {
            var withoutUnderscore = trimmed.Replace("_", "");
            if (Enum.TryParse<TEnum>(withoutUnderscore, ignoreCase: true, out result) && Enum.IsDefined(typeof(TEnum), result))
            {
                return true;
            }
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Safely casts an integer to an Enum if defined, otherwise returns defaultValue.
    /// </summary>
    public static TEnum ToEnum<TEnum>(this int value, TEnum defaultValue = default) where TEnum : struct, Enum
    {
        return Enum.IsDefined(typeof(TEnum), value) ? (TEnum)(object)value : defaultValue;
    }

    /// <summary>
    /// Checks whether an external string represents a valid value of TEnum.
    /// </summary>
    public static bool IsValidEnum<TEnum>(this string? value) where TEnum : struct, Enum
    {
        return TryToEnum<TEnum>(value, out _);
    }

    /// <summary>
    /// Gets all defined string names of TEnum.
    /// </summary>
    public static IReadOnlyList<string> GetNames<TEnum>() where TEnum : struct, Enum
    {
        return Enum.GetNames<TEnum>();
    }
}
