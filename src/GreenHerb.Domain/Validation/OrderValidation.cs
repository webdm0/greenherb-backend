namespace GreenHerb.Domain.Validation;

public static class OrderValidation
{
    public const int CustomerNameMinLength = 2;
    public const int CustomerNameMaxLength = 120;
    public const int CustomerEmailMaxLength = 255;
    public const int CustomerPhoneMaxLength = 40;
    public const int ShippingAddressMaxLength = 255;
    public const int ShippingLocationMaxLength = 120;
    public const int ShippingPostalCodeMaxLength = 40;
    public const int NotesMaxLength = 1000;

    public const string PersonNamePattern = @"^\s*[\p{L}\p{M}0-9][\p{L}\p{M}0-9 .'\-]*\s*$";
    public const string PhonePattern = @"^\s*\+?[0-9][0-9(). \-]{6,19}\s*$";
    public const string AddressPattern = @"^\s*[\p{L}\p{M}0-9][\p{L}\p{M}0-9\s,.'#/\-]*\s*$";
    public const string PostalCodePattern = @"^\s*[A-Za-z0-9][A-Za-z0-9 \-]{1,19}\s*$";
    public const string LocationPattern = @"^\s*[\p{L}\p{M}0-9][\p{L}\p{M}0-9 .'\-]*\s*$";

    public const string CustomerNameMessage = "Enter a valid customer name using letters, numbers, spaces, apostrophes, periods, and hyphens only.";
    public const string CustomerPhoneMessage = "Enter a valid phone number using digits and standard separators only.";
    public const string ShippingAddressMessage = "Enter a valid address using letters, numbers, spaces, and standard address punctuation only.";
    public const string ShippingPostalCodeMessage = "Enter a valid postal code using letters, numbers, spaces, or hyphens only.";
    public const string ShippingLocationMessage = "Enter a valid value using letters, numbers, spaces, apostrophes, periods, and hyphens only.";
}
