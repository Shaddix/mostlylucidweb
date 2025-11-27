using System.Reflection;
using Microsoft.AspNetCore.WebUtilities;

namespace Umami.Net.UmamiData.Helpers;

/// <summary>
/// Helper class for converting objects to query strings using attributes.
/// </summary>
public static class QueryStringHelper
{
    /// <summary>
    /// Converts an object to a URL query string based on <see cref="QueryStringParameterAttribute"/> decorations.
    /// </summary>
    /// <param name="obj">The object to convert to a query string.</param>
    /// <returns>A query string with all non-null properties included.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when obj is null.
    /// Suggestion: Ensure you pass a valid request object.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when a required parameter is null.
    /// Suggestion: Set all required properties before calling this method.
    /// </exception>
    public static string ToQueryString(this object obj)
    {
        if (obj == null)
        {
            throw new ArgumentNullException(nameof(obj),
                "Cannot convert null object to query string. " +
                "Suggestion: Ensure you create and populate a request object before calling ToQueryString().");
        }

        var queryParams = new Dictionary<string, string>();
        var objectType = obj.GetType();

        foreach (var property in objectType.GetProperties())
        {
            var attribute = property.GetCustomAttribute<QueryStringParameterAttribute>();

            // Skip properties without the attribute
            if (attribute == null) continue;

            var propertyValue = property.GetValue(obj);
            var propertyName = string.IsNullOrEmpty(attribute.Name) ? property.Name : attribute.Name;

            // Validate required parameters
            if (attribute.IsRequired)
            {
                if (propertyValue == null)
                {
                    throw new ArgumentException(
                        $"Required parameter '{propertyName}' (property '{property.Name}') cannot be null. " +
                        $"Suggestion: Set the {property.Name} property on your {objectType.Name} object before making the API call.",
                        property.Name);
                }

                // For strings, also check for empty/whitespace
                if (propertyValue is string strValue && string.IsNullOrWhiteSpace(strValue))
                {
                    throw new ArgumentException(
                        $"Required parameter '{propertyName}' (property '{property.Name}') cannot be empty or whitespace. " +
                        $"Suggestion: Set {property.Name} to a valid non-empty value.",
                        property.Name);
                }
            }

            // Add non-null values to query string
            if (propertyValue != null)
            {
                var stringValue = ConvertPropertyValue(propertyValue);
                if (!string.IsNullOrEmpty(stringValue))
                {
                    queryParams.Add(propertyName, stringValue);
                }
            }
        }

        return QueryHelpers.AddQueryString(string.Empty, queryParams);
    }

    /// <summary>
    /// Converts a property value to its string representation for query strings.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>String representation of the value.</returns>
    private static string ConvertPropertyValue(object value)
    {
        return value switch
        {
            null => string.Empty,
            bool b => b.ToString().ToLowerInvariant(), // "true" or "false"
            Enum e => e.ToString().ToLowerInvariant(),  // Enum values as lowercase
            DateTime dt => throw new InvalidOperationException(
                $"DateTime value {dt:O} cannot be converted directly. " +
                "Suggestion: Use a calculated property that converts DateTime to Unix milliseconds (see BaseRequest.StartAt/EndAt)."),
            _ => value.ToString() ?? string.Empty
        };
    }
}