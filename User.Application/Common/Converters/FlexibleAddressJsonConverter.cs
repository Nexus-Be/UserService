namespace User.Application.Common.Converters;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Flexible converter for Address that accepts either:
/// 1. A JSON string (e.g. "123 Le Loi, Ben Nghe, Quan 1, TP.HCM")
/// 2. A JSON object (e.g. {"streetAddress": "123 Le Loi", "wardCode": "001", "districtId": 2, "provinceId": 1} or snake_case)
/// 3. null
/// And always converts it to a standard formatted string for storage in User.Address.
/// </summary>
public sealed class FlexibleAddressJsonConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString();
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;

            string? fullAddress = null;
            string? street = null;
            string? ward = null;
            string? district = null;
            string? province = null;

            foreach (var prop in root.EnumerateObject())
            {
                var normalized = prop.Name.ToLowerInvariant().Replace("_", "");
                if (normalized == "fulladdress" || normalized == "formattedaddress" || normalized == "addressline")
                {
                    fullAddress = prop.Value.GetString();
                }
                else if (normalized == "streetaddress" || normalized == "street")
                {
                    street = prop.Value.GetString();
                }
                else if (normalized == "wardcode" || normalized == "ward")
                {
                    ward = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : prop.Value.ToString();
                }
                else if (normalized == "districtid" || normalized == "district")
                {
                    district = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : prop.Value.ToString();
                }
                else if (normalized == "provinceid" || normalized == "province")
                {
                    province = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : prop.Value.ToString();
                }
            }

            if (!string.IsNullOrWhiteSpace(fullAddress))
            {
                return fullAddress.Trim();
            }

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(street)) parts.Add(street.Trim());
            if (!string.IsNullOrWhiteSpace(ward)) parts.Add(ward.Trim());
            if (!string.IsNullOrWhiteSpace(district)) parts.Add(district.Trim());
            if (!string.IsNullOrWhiteSpace(province)) parts.Add(province.Trim());

            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }

        return reader.GetString();
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(value);
        }
    }
}

