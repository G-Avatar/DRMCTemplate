using Application.Common.Helpers;

namespace WebApi.Tests
{
    public class StringHelperTests
    {
        [Theory]
        [InlineData(null, "")]
        [InlineData("   ", "")]
        [InlineData("  Juan   Dela  Cruz ", "Juan Dela Cruz")]
        public void CleanStringTrimsAndSquashesSpaces(string? input, string expected) =>
            Assert.Equal(expected, StringHelper.CleanString(input));

        [Fact]
        public void BlankStringBecomesNull() => Assert.Null(StringHelper.ReturnStringValueOrNull("   "));

        [Theory]
        [InlineData("Juan", "santos", "Dela Cruz", "Jr.", "MD", "Juan s. Dela Cruz Jr., MD")]
        [InlineData(" Juan ", "   ", "  Dela   Cruz", null, null, "Juan Dela Cruz")]
        [InlineData("Juan", null, "Cruz", "", "  ", "Juan Cruz")]
        public void FormatFullNameSkipsBlankParts(string? first, string? middle, string? last, string? suffix, string? extension, string expected) =>
            Assert.Equal(expected, StringHelper.FormatFullName(first, middle, last, suffix, extension));
    }
}
