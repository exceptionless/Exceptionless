using Exceptionless.Core.Services;
using Xunit;

namespace Exceptionless.Tests.CustomFields;

public class EventCustomFieldServiceTests
{
    #region IsValidFieldName

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void IsValidFieldName_EmptyOrWhitespace_ReturnsFalse(string? name)
    {
        Assert.False(EventCustomFieldService.IsValidFieldName(name!));
    }

    [Fact]
    public void IsValidFieldName_Over100Chars_ReturnsFalse()
    {
        var name = new string('a', 101);
        Assert.False(EventCustomFieldService.IsValidFieldName(name));
    }

    [Fact]
    public void IsValidFieldName_Exactly100Chars_ReturnsTrue()
    {
        var name = new string('a', 100);
        Assert.True(EventCustomFieldService.IsValidFieldName(name));
    }

    [Theory]
    [InlineData("@error")]
    [InlineData("@request")]
    [InlineData("@environment")]
    [InlineData("@user")]
    [InlineData("@user_description")]
    [InlineData("@version")]
    [InlineData("@level")]
    [InlineData("@submission_method")]
    [InlineData("@submission_client")]
    [InlineData("@location")]
    [InlineData("@stack")]
    [InlineData("@simple_error")]
    public void IsValidFieldName_ReservedNames_ReturnsFalse(string name)
    {
        Assert.False(EventCustomFieldService.IsValidFieldName(name));
    }

    [Fact]
    public void IsValidFieldName_StartsWithAt_ReturnsFalse()
    {
        Assert.False(EventCustomFieldService.IsValidFieldName("@custom"));
    }

    [Theory]
    [InlineData("my_field")]
    [InlineData("response.time")]
    [InlineData("user-agent")]
    [InlineData("CamelCase")]
    [InlineData("x")]
    [InlineData("field123")]
    [InlineData("a.b.c")]
    [InlineData("my-custom-field")]
    public void IsValidFieldName_ValidNames_ReturnsTrue(string name)
    {
        Assert.True(EventCustomFieldService.IsValidFieldName(name));
    }

    [Theory]
    [InlineData("has space")]
    [InlineData("has!bang")]
    [InlineData("has@at")]
    [InlineData("has#hash")]
    [InlineData("has$dollar")]
    [InlineData("field[0]")]
    [InlineData("field{x}")]
    public void IsValidFieldName_SpecialChars_ReturnsFalse(string name)
    {
        Assert.False(EventCustomFieldService.IsValidFieldName(name));
    }

    #endregion

    #region ConvertValue

    [Fact]
    public void ConvertValue_Null_ReturnsNull()
    {
        Assert.Null(EventCustomFieldService.ConvertValue(null, "keyword"));
        Assert.Null(EventCustomFieldService.ConvertValue(null, "int"));
        Assert.Null(EventCustomFieldService.ConvertValue(null, "bool"));
    }

    [Theory]
    [InlineData("hello", "hello")]
    [InlineData(42, "42")]
    [InlineData(true, "True")]
    public void ConvertValue_Keyword_ConvertsToString(object value, string expected)
    {
        Assert.Equal(expected, EventCustomFieldService.ConvertValue(value, "keyword"));
    }

    [Fact]
    public void ConvertValue_Keyword_RejectsOverlyLong()
    {
        var longString = new string('a', 257);
        Assert.Null(EventCustomFieldService.ConvertValue(longString, "keyword"));
    }

    [Theory]
    [InlineData(42, 42)]
    [InlineData((short)5, 5)]
    [InlineData((byte)1, 1)]
    public void ConvertValue_Int_FromNumericTypes(object value, int expected)
    {
        Assert.Equal(expected, EventCustomFieldService.ConvertValue(value, "int"));
    }

    [Fact]
    public void ConvertValue_Int_FromString()
    {
        Assert.Equal(123, EventCustomFieldService.ConvertValue("123", "int"));
    }

    [Fact]
    public void ConvertValue_Int_RejectsInvalid()
    {
        Assert.Null(EventCustomFieldService.ConvertValue("not-a-number", "int"));
        Assert.Null(EventCustomFieldService.ConvertValue(Double.NaN, "int"));
        Assert.Null(EventCustomFieldService.ConvertValue(Double.PositiveInfinity, "int"));
    }

    [Fact]
    public void ConvertValue_Long_FromNumericTypes()
    {
        Assert.Equal(42L, EventCustomFieldService.ConvertValue(42L, "long"));
        Assert.Equal(42L, EventCustomFieldService.ConvertValue(42, "long"));
    }

    [Fact]
    public void ConvertValue_Float_FromNumericTypes()
    {
        Assert.Equal(3.14f, EventCustomFieldService.ConvertValue(3.14f, "float"));
        Assert.Equal(42f, EventCustomFieldService.ConvertValue(42, "float"));
    }

    [Fact]
    public void ConvertValue_Float_RejectsNonFinite()
    {
        Assert.Null(EventCustomFieldService.ConvertValue(Single.NaN, "float"));
        Assert.Null(EventCustomFieldService.ConvertValue(Single.PositiveInfinity, "float"));
    }

    [Fact]
    public void ConvertValue_Double_FromNumericTypes()
    {
        Assert.Equal(3.14d, EventCustomFieldService.ConvertValue(3.14d, "double"));
        Assert.Equal(42d, EventCustomFieldService.ConvertValue(42, "double"));
    }

    [Fact]
    public void ConvertValue_Double_RejectsNonFinite()
    {
        Assert.Null(EventCustomFieldService.ConvertValue(Double.NaN, "double"));
        Assert.Null(EventCustomFieldService.ConvertValue(Double.NegativeInfinity, "double"));
    }

    [Fact]
    public void ConvertValue_Bool_FromBool()
    {
        Assert.Equal(true, EventCustomFieldService.ConvertValue(true, "bool"));
        Assert.Equal(false, EventCustomFieldService.ConvertValue(false, "bool"));
    }

    [Fact]
    public void ConvertValue_Bool_CoercesFromStringsAndNumbers()
    {
        Assert.Equal(true, EventCustomFieldService.ConvertValue("true", "bool"));
        Assert.Equal(true, EventCustomFieldService.ConvertValue("True", "bool"));
        Assert.Equal(true, EventCustomFieldService.ConvertValue("TRUE", "bool"));
        Assert.Equal(true, EventCustomFieldService.ConvertValue("1", "bool"));
        Assert.Equal(false, EventCustomFieldService.ConvertValue("false", "bool"));
        Assert.Equal(false, EventCustomFieldService.ConvertValue("False", "bool"));
        Assert.Equal(false, EventCustomFieldService.ConvertValue("0", "bool"));
        Assert.Equal(true, EventCustomFieldService.ConvertValue(1, "bool"));
        Assert.Equal(false, EventCustomFieldService.ConvertValue(0, "bool"));
    }

    [Fact]
    public void ConvertValue_Bool_RejectsUnrecognizedValues()
    {
        Assert.Null(EventCustomFieldService.ConvertValue("yes", "bool"));
        Assert.Null(EventCustomFieldService.ConvertValue("no", "bool"));
        Assert.Null(EventCustomFieldService.ConvertValue(3.14d, "bool"));
    }

    [Fact]
    public void ConvertValue_Date_FromDateTime()
    {
        var dt = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        Assert.Equal(dt, EventCustomFieldService.ConvertValue(dt, "date"));
    }

    [Fact]
    public void ConvertValue_Date_FromString()
    {
        var result = EventCustomFieldService.ConvertValue("2024-01-15T10:30:00Z", "date");
        Assert.NotNull(result);
        Assert.IsType<DateTime>(result);
    }

    [Fact]
    public void ConvertValue_Date_RejectsInvalid()
    {
        Assert.Null(EventCustomFieldService.ConvertValue("not-a-date", "date"));
        Assert.Null(EventCustomFieldService.ConvertValue(42, "date"));
    }

    [Fact]
    public void ConvertValue_UnknownType_ReturnsNull()
    {
        Assert.Null(EventCustomFieldService.ConvertValue("hello", "unknown"));
    }

    #endregion
}
