namespace User.Tests;

using System;
using System.Text.Json;
using User.Api.Common.Converters;
using User.Application.Common.Extensions;
using User.Domain.Enums;
using Xunit;

public sealed class EnumConversionTests
{
    private readonly JsonSerializerOptions _jsonOptions;

    public EnumConversionTests()
    {
        _jsonOptions = new JsonSerializerOptions();
        _jsonOptions.Converters.Add(new PascalCaseJsonStringEnumConverter(allowIntegerValues: false));
    }

    private sealed record DummyModel(UserStatus Status, Gender? Gender = null);

    // ---------------------------------------------------------
    // 1. Domain Enum Underlying Type Invariant
    // ---------------------------------------------------------
    [Theory]
    [InlineData(typeof(UserStatus))]
    [InlineData(typeof(RoleType))]
    [InlineData(typeof(SellerProfileStatus))]
    [InlineData(typeof(Gender))]
    [InlineData(typeof(TrustLevel))]
    [InlineData(typeof(PenaltyStatus))]
    [InlineData(typeof(PenaltyType))]
    [InlineData(typeof(PenaltyViolationType))]
    [InlineData(typeof(ReportStatus))]
    [InlineData(typeof(ReportViolationType))]
    [InlineData(typeof(ReviewerRole))]
    [InlineData(typeof(ConfigDataType))]
    [InlineData(typeof(AuditAction))]
    [InlineData(typeof(ReputationChangeReason))]
    [InlineData(typeof(ReferenceType))]
    [InlineData(typeof(OtpType))]
    public void AllDomainEnums_HaveUnderlyingType_Int32(Type enumType)
    {
        var underlyingType = Enum.GetUnderlyingType(enumType);
        Assert.Equal(typeof(int), underlyingType);
    }

    // ---------------------------------------------------------
    // 2. Outbound JSON Serialization (Enum -> String)
    // ---------------------------------------------------------
    [Fact]
    public void Outbound_EnumProperty_SerializesToString_PascalCase()
    {
        var model = new DummyModel(UserStatus.Active, Gender.Female);
        var json = JsonSerializer.Serialize(model, _jsonOptions);

        Assert.Contains("\"Status\":\"Active\"", json);
        Assert.Contains("\"Gender\":\"Female\"", json);
        Assert.DoesNotContain("\"Status\":1", json);
        Assert.DoesNotContain("\"Gender\":2", json);
    }

    [Fact]
    public void Outbound_MultiWordEnum_SerializesToPascalCase()
    {
        var model = new DummyModel(UserStatus.PendingVerification);
        var json = JsonSerializer.Serialize(model, _jsonOptions);

        Assert.Contains("\"Status\":\"PendingVerification\"", json);
    }

    [Fact]
    public void Outbound_NullableEnum_NullSerializesToNullLiteral()
    {
        var model = new DummyModel(UserStatus.Active, null);
        var json = JsonSerializer.Serialize(model, _jsonOptions);

        Assert.Contains("\"Gender\":null", json);
    }

    // ---------------------------------------------------------
    // 3. Inbound JSON Deserialization (String -> Enum)
    // ---------------------------------------------------------
    [Theory]
    [InlineData("{\"Status\":\"Active\"}", UserStatus.Active)]
    [InlineData("{\"Status\":\"active\"}", UserStatus.Active)]
    [InlineData("{\"Status\":\"ACTIVE\"}", UserStatus.Active)]
    [InlineData("{\"Status\":\"PendingVerification\"}", UserStatus.PendingVerification)]
    [InlineData("{\"Status\":\"pendingverification\"}", UserStatus.PendingVerification)]
    [InlineData("{\"Status\":\"PENDING_VERIFICATION\"}", UserStatus.PendingVerification)]
    [InlineData("{\"Status\":\"pending_verification\"}", UserStatus.PendingVerification)]
    [InlineData("{\"Status\":\"Banned\"}", UserStatus.Banned)]
    [InlineData("{\"Status\":\"banned\"}", UserStatus.Banned)]
    [InlineData("{\"Status\":\"Deleted\"}", UserStatus.Deleted)]
    [InlineData("{\"Status\":\"deleted\"}", UserStatus.Deleted)]
    public void Inbound_VariousStringCasings_DeserializeSuccessfully(string json, UserStatus expected)
    {
        var result = JsonSerializer.Deserialize<DummyModel>(json, _jsonOptions);

        Assert.NotNull(result);
        Assert.Equal(expected, result.Status);
    }

    [Fact]
    public void Inbound_NullableEnum_DeserializesStringSuccessfully()
    {
        var json = "{\"Status\":\"Active\",\"Gender\":\"Male\"}";
        var result = JsonSerializer.Deserialize<DummyModel>(json, _jsonOptions);

        Assert.NotNull(result);
        Assert.Equal(Gender.Male, result.Gender);
    }

    [Fact]
    public void Inbound_RawIntegerNumber_ThrowsJsonException()
    {
        var json = "{\"Status\":1}";
        var ex = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<DummyModel>(json, _jsonOptions));

        Assert.Contains("Expected string value for enum 'UserStatus'", ex.Message);
    }

    [Fact]
    public void Inbound_NumericString_ThrowsJsonException()
    {
        var json = "{\"Status\":\"1\"}";
        var ex = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<DummyModel>(json, _jsonOptions));

        Assert.Contains("Invalid value '1' for enum 'UserStatus'", ex.Message);
        Assert.Contains("Valid values are:", ex.Message);
    }

    [Fact]
    public void Inbound_EmptyString_ThrowsJsonException()
    {
        var json = "{\"Status\":\"   \"}";
        var ex = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<DummyModel>(json, _jsonOptions));

        Assert.Contains("cannot be empty", ex.Message);
    }

    [Fact]
    public void Inbound_InvalidString_ThrowsJsonException_WithSentenceCaseEnglishMessage()
    {
        var json = "{\"Status\":\"NonExistentStatus\"}";
        var ex = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<DummyModel>(json, _jsonOptions));

        Assert.Contains("Invalid value 'NonExistentStatus' for enum 'UserStatus'", ex.Message);
        Assert.Contains("PendingVerification, Active, Banned, Deleted", ex.Message);
    }

    // ---------------------------------------------------------
    // 4. Application Extension Methods (EnumExtensions)
    // ---------------------------------------------------------
    [Fact]
    public void EnumExtensions_ToEnumString_ReturnsPascalCaseString()
    {
        Assert.Equal("Active", UserStatus.Active.ToEnumString());
        Assert.Equal("PendingVerification", UserStatus.PendingVerification.ToEnumString());
        Assert.Equal("Buyer", RoleType.Buyer.ToEnumString());
        Assert.Equal("SupportStaff", RoleType.SupportStaff.ToEnumString());

        UserStatus? nullable = UserStatus.Banned;
        Assert.Equal("Banned", nullable.ToEnumString());
        UserStatus? nullVal = null;
        Assert.Equal("None", nullVal.ToEnumString("None"));
    }

    [Fact]
    public void EnumExtensions_ToInt_ReturnsUnderlyingInt()
    {
        Assert.Equal(0, UserStatus.PendingVerification.ToInt());
        Assert.Equal(1, UserStatus.Active.ToInt());
        Assert.Equal(2, UserStatus.Banned.ToInt());
        Assert.Equal(3, UserStatus.Deleted.ToInt());

        Assert.Equal(0, RoleType.Buyer.ToInt());
        Assert.Equal(1, RoleType.Seller.ToInt());
        Assert.Equal(2, RoleType.SupportStaff.ToInt());
        Assert.Equal(3, RoleType.Admin.ToInt());
    }

    [Theory]
    [InlineData("Active", UserStatus.Active)]
    [InlineData("active", UserStatus.Active)]
    [InlineData("ACTIVE", UserStatus.Active)]
    [InlineData("PendingVerification", UserStatus.PendingVerification)]
    [InlineData("pendingVerification", UserStatus.PendingVerification)]
    [InlineData("PENDING_VERIFICATION", UserStatus.PendingVerification)]
    [InlineData("pending_verification", UserStatus.PendingVerification)]
    public void EnumExtensions_ToEnum_ParsesValidStrings(string input, UserStatus expected)
    {
        Assert.Equal(expected, input.ToEnum<UserStatus>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void EnumExtensions_ToEnum_NullOrEmpty_ThrowsArgumentException(string? input)
    {
        var ex = Assert.Throws<ArgumentException>(() => input.ToEnum<UserStatus>());
        Assert.Contains("cannot be empty", ex.Message);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("99")]
    [InlineData("UnknownStatus")]
    public void EnumExtensions_ToEnum_InvalidOrNumericString_ThrowsArgumentException(string input)
    {
        var ex = Assert.Throws<ArgumentException>(() => input.ToEnum<UserStatus>());
        Assert.Contains($"Invalid value '{input}' for enum 'UserStatus'", ex.Message);
        Assert.Contains("Valid values are:", ex.Message);
    }

    [Theory]
    [InlineData("Active", true, UserStatus.Active)]
    [InlineData("PENDING_VERIFICATION", true, UserStatus.PendingVerification)]
    [InlineData("1", false, UserStatus.PendingVerification)]
    [InlineData("invalid", false, UserStatus.PendingVerification)]
    [InlineData("", false, UserStatus.PendingVerification)]
    [InlineData(null, false, UserStatus.PendingVerification)]
    public void EnumExtensions_TryToEnum_WorksAsExpected(string? input, bool expectedSuccess, UserStatus expectedResult)
    {
        var success = input.TryToEnum<UserStatus>(out var result);
        Assert.Equal(expectedSuccess, success);
        if (expectedSuccess)
        {
            Assert.Equal(expectedResult, result);
        }
    }

    [Fact]
    public void EnumExtensions_ToEnumOrDefault_FallsBackWhenInvalidOrEmpty()
    {
        Assert.Equal(UserStatus.Active, "active".ToEnumOrDefault(UserStatus.Deleted));
        Assert.Equal(UserStatus.Deleted, "invalid".ToEnumOrDefault(UserStatus.Deleted));
        Assert.Equal(UserStatus.Deleted, "".ToEnumOrDefault(UserStatus.Deleted));
        Assert.Equal(UserStatus.Deleted, ((string?)null).ToEnumOrDefault(UserStatus.Deleted));
    }
}
