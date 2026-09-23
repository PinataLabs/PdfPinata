using System;
using System.Linq;
using System.Reflection;
using PinataLayout.DocumentObjectModel.Internals;

namespace PinataLayout.DocumentObjectModel.Resources;

internal static class DomSR
{
    /// <summary>
    /// The generated resource properties are internal statics, so the lookup has to ask for
    /// non-public members: GetProperties() without flags looks for public instance members and
    /// finds none of them, which reported every message in the assembly as missing.
    /// </summary>
    private const BindingFlags ResourceProperties =
        BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

    internal static string GetString(DomMsgID id)
    {
        return (string)typeof(AppResources).GetProperties(ResourceProperties)
            .FirstOrDefault(x => x.Name == id.ToString() && x.PropertyType == typeof(string))
            ?.GetValue(null);
    }

    internal static string FormatMessage(DomMsgID id, params object[] args)
    {
        string message;
        try
        {
            message = GetString(id);
            if (message != null)
            {
                message = String.Format(message, args);
            }
            else
                message = "<<<error: message not found>>>";
            return message;
        }
        catch (Exception ex) when (!Unrecoverable.Is(ex))
        {
            // Formatting an error message must not itself throw, or the real error is lost behind
            // this one. A bad format string or a wrong argument count is reported in place of the
            // message rather than replacing the failure being described.
            message = "INTERNAL ERROR while formatting error message: " + ex;
        }
        return message;
    }

    internal static string InvalidValueName(string name)
    {
        return string.Format(AppResources.InvalidValueName, name);
    }

    internal static string ParentAlreadySet(DocumentObject value, DocumentObject docObject)
    {
        return
            $"Value of type '{value.GetType()}' must be cloned before set into '{docObject.GetType()}'.";
    }

    internal static string UndefinedBaseStyle(string baseStyle)
    {
        return string.Format(AppResources.UndefinedBaseStyle, baseStyle);
    }

    internal static string InvalidUnitValue(string value)
    {
        return string.Format(AppResources.InvalidUnitValue, value);
    }

    internal static string InvalidUnitType(string value)
    {
        return string.Format(AppResources.InvalidUnitType, value);
    }

    internal static string MissingObligatoryProperty(string v1, string v2)
    {
        return string.Format(AppResources.MissingObligatoryProperty, v1, v2);
    }

    internal static string InvalidInfoFieldName(string value)
    {
        return string.Format(AppResources.InvalidInfoFieldName, value);
    }

    internal static string InvalidFieldFormat(string value)
    {
        return string.Format(AppResources.InvalidFieldFormat, value);
    }

    internal static string NumberTooLargeForRoman(int number)
    {
        return string.Format(AppResources.NumberTooLargeForRoman, number);
    }

    internal static string NumberTooLargeForLetters(int number)
    {
        return string.Format(AppResources.NumberTooLargeForLetters, number);
    }

    internal static string InvalidColorString(string color)
    {
        return string.Format(AppResources.InvalidColorString, color);
    }
}
