using System.Text.RegularExpressions;
using User.Domain.Exceptions;

namespace User.Domain.ValueObjects;

public sealed partial record PhoneNumber
{
    public string Value { get; }
    [GeneratedRegex(@"^(03|05|07|08|09)\d{8}$")]
    private static partial Regex VnPhoneRegex();

    public PhoneNumber(string value)
    {
        // if (string.IsNullOrWhiteSpace(value))
        //     throw new DomainException("Số điện thoại không được để trống.");
        // tự động xóa kí tuje giữa số điện thoại
        var normalized = value.Trim().Replace(" ", "").Replace("-", "").Replace(".", "");
        // Tự động đổi +84 ở đầu thành số 0
        if (normalized.StartsWith("+84"))
            normalized = "0" + normalized[3..];
        // 3. Check Regex
        if (!VnPhoneRegex().IsMatch(normalized))
            throw new DomainException($"Số điện thoại '{value}' không đúng định dạng.");
        Value = normalized;
    }

    public static implicit operator PhoneNumber(string value) => new PhoneNumber(value);
    public static implicit operator string(PhoneNumber phone) => phone.Value;
    public override string ToString() => Value;
}