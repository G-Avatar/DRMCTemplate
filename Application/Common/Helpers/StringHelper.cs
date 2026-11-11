using System.Text.RegularExpressions;

namespace Application.Common.Helpers
{
    public static partial class StringHelper
    {
        [GeneratedRegex(@" {2,}")]
        private static partial Regex MultipleSpacesRegex();

        // Blank input becomes "", so a blank middle name never turns into " ." in FormatFullName.
        public static string CleanString(string? input)
        {
            return string.IsNullOrWhiteSpace(input)
                ? string.Empty
                : MultipleSpacesRegex().Replace(input.Trim(), " ");
        }

        public static string? ReturnStringValueOrNull(string? input)
        {
            return string.IsNullOrWhiteSpace(input)
                ? null
                : MultipleSpacesRegex().Replace(input.Trim(), " ");
        }

        public static string FormatFullName(
            string? firstName,
            string? middleName,
            string? lastName,
            string? suffix,
            string? extension)
        {
            firstName = CleanString(firstName);
            middleName = CleanString(middleName);
            lastName = CleanString(lastName);
            suffix = CleanString(suffix);
            extension = CleanString(extension);

            string middleInitial = string.IsNullOrEmpty(middleName)
                ? string.Empty
                : $"{middleName[0]}.";

            string fullName = string.Join(" ",
                new[] { firstName, middleInitial, lastName, suffix }
                    .Where(s => !string.IsNullOrWhiteSpace(s)));

            if (!string.IsNullOrWhiteSpace(extension))
            {
                fullName += $", {extension}";
            }

            return fullName;
        }
    }
}
